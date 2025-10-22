// Services/OllamaAdapter.cs
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using satidotnet.Models;

namespace satidotnet.Services;

public class OllamaAdapter : ILLMAdapter
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OllamaAdapter> _logger;
    private readonly OllamaConfig _config;
    
    public OllamaAdapter(HttpClient httpClient, ILogger<OllamaAdapter> logger, OllamaConfig config)
    {
        _httpClient = httpClient;
        _logger = logger;
        _config = config;
    }

    public async Task<LLMResponse> GenerateAsync(string prompt, CancellationToken cancellationToken = default)
    {
        var requestTimestamp = DateTime.UtcNow;
        var stopwatch = Stopwatch.StartNew();

        _logger.LogInformation(
            "[LLM CALL] POST {BaseUrl}{Endpoint} | Timestamp: {Timestamp}",
            _httpClient.BaseAddress,
            _config.Endpoint,
            requestTimestamp.ToString("o")
        );

        var request = new
        {
            model = _config.Model,
            prompt = prompt,
            format = _config.Format,
            stream = _config.Stream
        };

        try
        {
            var response = await _httpClient.PostAsJsonAsync(_config.Endpoint, request, cancellationToken);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<OllamaGenerateResponse>(cancellationToken);
            
            stopwatch.Stop();

            var trace = new RequestTrace
            {
                RequestTimestamp = requestTimestamp,
                ResponseTimestamp = DateTime.UtcNow,
                DurationMs = stopwatch.ElapsedMilliseconds,
                Model = _config.Model,
                Method = "POST",
                Url = $"{_httpClient.BaseAddress}{_config.Endpoint}"
            };

            return new LLMResponse
            {
                Response = result?.Response ?? string.Empty,
                Trace = trace
            };
        }
        catch (HttpRequestException ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "LLM request failed after {Duration}ms", stopwatch.ElapsedMilliseconds);

            throw new LLMAdapterException("Failed to communicate with Ollama service", ex)
            {
                Trace = new RequestTrace
                {
                    RequestTimestamp = requestTimestamp,
                    ResponseTimestamp = DateTime.UtcNow,
                    DurationMs = stopwatch.ElapsedMilliseconds,
                    Method = "POST",
                    Model = _config.Model,
                    Url = $"{_httpClient.BaseAddress}{_config.Endpoint}",
                    Error = true
                }
            };
        }
    }
}

public class OllamaGenerateResponse
{
    [JsonPropertyName("response")]
    public string Response { get; set; } = string.Empty;
}