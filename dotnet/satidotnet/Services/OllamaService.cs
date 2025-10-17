using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace satidotnet.Services;

public class OllamaService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OllamaService> _logger;
    private readonly string _modelName;

    public OllamaService(HttpClient httpClient, ILogger<OllamaService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _modelName = "mistral"; // Will be configured in Program.cs
    }

    public async Task<OllamaResponse> GenerateAsync(string prompt, bool formatJson = true, CancellationToken cancellationToken = default)
    {
        var requestTimestamp = DateTime.UtcNow;
        var stopwatch = Stopwatch.StartNew();

        _logger.LogInformation(
            "[LOCAL LLM CALL] POST {BaseUrl}/api/generate | Timestamp: {Timestamp}",
            _httpClient.BaseAddress,
            requestTimestamp.ToString("o")
        );

        var request = new OllamaGenerateRequest
        {
            Model = _modelName,
            Prompt = prompt,
            Format = formatJson ? "json" : null,
            Stream = false
        };

        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/generate", request, cancellationToken);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<OllamaGenerateResponse>(cancellationToken);
            
            stopwatch.Stop();

            var trace = new RequestTrace
            {
                RequestTimestamp = requestTimestamp,
                ResponseTimestamp = DateTime.UtcNow,
                DurationMs = stopwatch.ElapsedMilliseconds,
                Method = "POST",
                Url = $"{_httpClient.BaseAddress}api/generate"
            };

            _logger.LogInformation(
                "LLM response received in {Duration}ms",
                stopwatch.ElapsedMilliseconds
            );

            return new OllamaResponse
            {
                Response = result?.Response ?? string.Empty,
                Trace = trace
            };
        }
        catch (HttpRequestException ex)
        {
            stopwatch.Stop();
            
            _logger.LogError(
                ex,
                "LLM request failed after {Duration}ms",
                stopwatch.ElapsedMilliseconds
            );

            throw new OllamaServiceException("Failed to communicate with Ollama service", ex)
            {
                Trace = new RequestTrace
                {
                    RequestTimestamp = requestTimestamp,
                    ResponseTimestamp = DateTime.UtcNow,
                    DurationMs = stopwatch.ElapsedMilliseconds,
                    Method = "POST",
                    Url = $"{_httpClient.BaseAddress}api/generate",
                    Error = true
                }
            };
        }
    }
}

// Request/Response Models
public class OllamaGenerateRequest
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;

    [JsonPropertyName("prompt")]
    public string Prompt { get; set; } = string.Empty;

    [JsonPropertyName("format")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Format { get; set; }

    [JsonPropertyName("stream")]
    public bool Stream { get; set; }
}

public class OllamaGenerateResponse
{
    [JsonPropertyName("response")]
    public string Response { get; set; } = string.Empty;

    [JsonPropertyName("model")]
    public string? Model { get; set; }

    [JsonPropertyName("done")]
    public bool Done { get; set; }
}

public class OllamaResponse
{
    public string Response { get; set; } = string.Empty;
    public RequestTrace Trace { get; set; } = new();
}

public class RequestTrace
{
    public DateTime RequestTimestamp { get; set; }
    public DateTime ResponseTimestamp { get; set; }
    public long DurationMs { get; set; }
    public string Method { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public bool Error { get; set; }
}

public class OllamaServiceException : Exception
{
    public RequestTrace? Trace { get; set; }

    public OllamaServiceException(string message) : base(message) { }
    public OllamaServiceException(string message, Exception innerException) : base(message, innerException) { }
}