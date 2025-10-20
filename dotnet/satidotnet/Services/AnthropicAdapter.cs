// Services/AnthropicAdapter.cs
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using satidotnet.Models;

namespace satidotnet.Services;

public class AnthropicAdapter : ILLMAdapter
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<AnthropicAdapter> _logger;
    private readonly AnthropicConfig _config;

    public AnthropicAdapter(HttpClient httpClient, ILogger<AnthropicAdapter> logger, AnthropicConfig config)
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
            max_tokens = _config.MaxTokens > 0 ? _config.MaxTokens : 4096,
            messages = new[]
            {
                new
                {
                    role = "user",
                    content = jsonify
                }
            }
        };

        try
        {
            var httpRequest = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = JsonContent.Create(requestBody)
            };

            // Match Node.js headers exactly
            httpRequest.Headers.Add("x-api-key", _config.ApiKey);
            httpRequest.Headers.Add("anthropic-version", "2023-06-01");
            // Content-Type is automatically set by JsonContent.Create

            var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<AnthropicResponse>(cancellationToken);
            
            stopwatch.Stop();

            // console.log(response.data)
            _logger.LogInformation("{ResponseData}", JsonSerializer.Serialize(result));

            if (result?.Content == null || result.Content.Length == 0)
            {
                throw new LLMAdapterException("Anthropic returned empty response");
            }

            // const content = response.data.content[0]
            var content = result.Content[0];
            
            // const raw = content.text;
            var raw = content.Text;
            
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
            _logger.LogError(ex, "Anthropic request failed after {Duration}ms", stopwatch.ElapsedMilliseconds);

            throw new LLMAdapterException("Failed to communicate with Anthropic service", ex)
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

// Anthropic API Models
public class AnthropicResponse
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("role")]
    public string Role { get; set; } = string.Empty;

    [JsonPropertyName("content")]
    public AnthropicContent[] Content { get; set; } = Array.Empty<AnthropicContent>();

    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;

    [JsonPropertyName("stop_reason")]
    public string? StopReason { get; set; }

    [JsonPropertyName("usage")]
    public AnthropicUsage? Usage { get; set; }
}

public class AnthropicContent
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;
}

public class AnthropicUsage
{
    [JsonPropertyName("input_tokens")]
    public int InputTokens { get; set; }

    [JsonPropertyName("output_tokens")]
    public int OutputTokens { get; set; }
}