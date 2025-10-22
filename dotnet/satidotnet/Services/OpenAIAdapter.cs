// Services/OpenAIAdapter.cs
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using satidotnet.Models;
using System.Net.Http.Headers;

namespace satidotnet.Services;

public class OpenAIAdapter : ILLMAdapter
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OpenAIAdapter> _logger;
    private readonly OpenAIConfig _config;

    public OpenAIAdapter(HttpClient httpClient, ILogger<OpenAIAdapter> logger, OpenAIConfig config)
    {
        _httpClient = httpClient;
        _logger = logger;
        _config = config;
    }

    public async Task<LLMResponse> GenerateAsync(string prompt, CancellationToken cancellationToken = default)
    {
        var requestTimestamp = DateTime.UtcNow;
        var stopwatch = Stopwatch.StartNew();

        var url = $"{_config.BaseURL}{_config.Endpoint}";
        
        _logger.LogInformation(
            "[LLM CALL] POST {Url} | Timestamp: {Timestamp}",
            url,
            requestTimestamp.ToString("o")
        );

        // Match Node.js: JSON.stringify({ text: prompt })
        var jsonify = JsonSerializer.Serialize(new { text = prompt });
        _logger.LogInformation("{Jsonify}", jsonify);

        var requestBody = new
        {
            model = _config.Model,
            input = prompt
        };

        try
        {
            var httpRequest = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = JsonContent.Create(requestBody)
            };

            // Match Node.js headers exactly
            httpRequest.Headers.Add("OpenAI-Organization", _config.Organization);
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _config.ApiKey);
            // Content-Type is automatically set by JsonContent.Create

            var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<OpenAIResponse>(cancellationToken);
            
            stopwatch.Stop();
            
            _logger.LogInformation("{ResponseData}", JsonSerializer.Serialize(result));

            if (result?.Output[0]?.Content == null || result.Output[0]?.Content.Length == 0)
            {
                throw new LLMAdapterException("OpenAI returned empty response");
            }

            var content = result.Output[0].Content;
            
            // const raw = content.text;
            var raw = content[0].Text;
            
            // const jsonString = raw.replace(/```json|```/g, '').trim();
            var jsonString = Regex.Replace(raw, @"```json|```", "").Trim();
            
            // let parsed; try { parsed = JSON.parse(jsonString); console.log(parsed); }
            try
            {
                using var parsed = JsonDocument.Parse(jsonString);
                _logger.LogInformation("{Parsed}", JsonSerializer.Serialize(parsed));
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Invalid JSON: {JsonString}", jsonString);
            }

            var trace = new RequestTrace
            {
                RequestTimestamp = requestTimestamp,
                ResponseTimestamp = DateTime.UtcNow,
                DurationMs = stopwatch.ElapsedMilliseconds,
                Method = "POST",
                Model = _config.Model,
                Url = url
            };

            // return { response: jsonString, trace: response.trace };
            return new LLMResponse
            {
                Response = jsonString,
                Trace = trace
            };
        }
        catch (HttpRequestException ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "OpenAI request failed after {Duration}ms", stopwatch.ElapsedMilliseconds);

            throw new LLMAdapterException("Failed to communicate with OpenAI service", ex)
            {
                Trace = new RequestTrace
                {
                    RequestTimestamp = requestTimestamp,
                    ResponseTimestamp = DateTime.UtcNow,
                    DurationMs = stopwatch.ElapsedMilliseconds,
                    Method = "POST",
                    Url = url,
                    Error = true
                }
            };
        }
    }
}
public class OpenAIResponse
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("object")]
    public string Object { get; set; } = string.Empty;

    [JsonPropertyName("created_at")]
    public long CreatedAt { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;

    [JsonPropertyName("output")]
    public OpenAIOutput[] Output { get; set; } = Array.Empty<OpenAIOutput>();

    [JsonPropertyName("usage")]
    public OpenAIUsage? Usage { get; set; }
}

public class OpenAIOutput
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("role")]
    public string Role { get; set; } = string.Empty;

    [JsonPropertyName("content")]
    public OpenAIContent[] Content { get; set; } = Array.Empty<OpenAIContent>();
}

public class OpenAIContent
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;

    [JsonPropertyName("annotations")]
    public object[] Annotations { get; set; } = Array.Empty<object>();

    [JsonPropertyName("logprobs")]
    public object[] Logprobs { get; set; } = Array.Empty<object>();
}

public class OpenAIUsage
{
    [JsonPropertyName("input_tokens")]
    public int InputTokens { get; set; }

    [JsonPropertyName("output_tokens")]
    public int OutputTokens { get; set; }

    [JsonPropertyName("total_tokens")]
    public int TotalTokens { get; set; }

    [JsonPropertyName("input_tokens_details")]
    public InputTokensDetails? InputTokensDetails { get; set; }

    [JsonPropertyName("output_tokens_details")]
    public OutputTokensDetails? OutputTokensDetails { get; set; }
}

public class InputTokensDetails
{
    [JsonPropertyName("cached_tokens")]
    public int CachedTokens { get; set; }
}

public class OutputTokensDetails
{
    [JsonPropertyName("reasoning_tokens")]
    public int ReasoningTokens { get; set; }
}