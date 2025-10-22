// Services/ILLMAdapter.cs
namespace satidotnet.Services;

public interface ILLMAdapter
{
    Task<LLMResponse> GenerateAsync(string prompt, CancellationToken cancellationToken = default);
}

public class LLMResponse
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
    public string Model { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public bool Error { get; set; }
}

public class LLMAdapterException : Exception
{
    public RequestTrace? Trace { get; set; }
    public LLMAdapterException(string message) : base(message) { }
    public LLMAdapterException(string message, Exception innerException) : base(message, innerException) { }
}