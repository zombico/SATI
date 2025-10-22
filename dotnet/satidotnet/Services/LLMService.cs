// Services/LLMService.cs
using System.Text.Json;
using System.Text.RegularExpressions;
using satidotnet.Models;

namespace satidotnet.Services;

public class LLMService
{
    private readonly ILogger<LLMService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly string _configPath;
    private ConfigFile? _config;
    private ILLMAdapter? _adapter;

    public LLMService(ILogger<LLMService> logger, IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        
        var projectRoot = Directory.GetCurrentDirectory();
        var solutionRoot = Path.GetFullPath(Path.Combine(projectRoot, "..", ".."));
        _configPath = Path.Combine(solutionRoot, "config", "config.json");
    }

    private async Task<ConfigFile> LoadConfigAsync()
    {
        if (_config != null) return _config;

        var configJson = await File.ReadAllTextAsync(_configPath);
        _config = JsonSerializer.Deserialize<ConfigFile>(configJson, 
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (_config?.LLM == null)
        {
            throw new InvalidOperationException("LLM configuration not found in config.json");
        }

        return _config;
    }

    private async Task<ILLMAdapter> GetAdapterAsync()
    {
        if (_adapter != null) return _adapter;

        var config = await LoadConfigAsync();
        var provider = config.LLM!.Provider.ToLowerInvariant();

        _logger.LogInformation("Creating LLM adapter for provider: {Provider}", provider);

        _adapter = provider switch
        {
            "ollama" => await CreateOllamaAdapterAsync(config.LLM.Ollama!),
            "anthropic" => await CreateAnthropicAdapterAsync(config.LLM.Anthropic!),
            "openai" => await CreateOpenAIAdapterAsync(config.LLM.OpenAI!),
            _ => throw new InvalidOperationException($"Unsupported LLM provider: {provider}")
        };

        return _adapter;
    }

    public async Task<LLMResponse> GenerateAsync(string prompt, CancellationToken cancellationToken = default)
    {
        var adapter = await GetAdapterAsync();
        return await adapter.GenerateAsync(prompt, cancellationToken);
    }

    private async Task<ILLMAdapter> CreateOllamaAdapterAsync(OllamaConfig config)
    {
        config = ResolveEnvironmentVariables(config);
        
        var httpClient = _serviceProvider.GetRequiredService<IHttpClientFactory>()
            .CreateClient("Ollama");
        
        httpClient.BaseAddress = new Uri(config.Host);
        httpClient.Timeout = TimeSpan.FromMilliseconds(config.Timeout);

        var logger = _serviceProvider.GetRequiredService<ILogger<OllamaAdapter>>();
        
        return new OllamaAdapter(httpClient, logger, config);
    }

    private async Task<ILLMAdapter> CreateAnthropicAdapterAsync(AnthropicConfig config)
    {
        config = ResolveEnvironmentVariables(config);
        
        var httpClient = _serviceProvider.GetRequiredService<IHttpClientFactory>()
            .CreateClient("Anthropic");
        
        httpClient.BaseAddress = new Uri(config.BaseURL);
        httpClient.Timeout = TimeSpan.FromMilliseconds(config.Timeout);

        var logger = _serviceProvider.GetRequiredService<ILogger<AnthropicAdapter>>();
        
        return new AnthropicAdapter(httpClient, logger, config);
    }
    
    private async Task<ILLMAdapter> CreateOpenAIAdapterAsync(OpenAIConfig config)
    {
        config = ResolveEnvironmentVariables(config);
        
        var httpClient = _serviceProvider.GetRequiredService<IHttpClientFactory>()
            .CreateClient("OpenAI");
        
        httpClient.BaseAddress = new Uri(config.BaseURL);
        httpClient.Timeout = TimeSpan.FromMilliseconds(config.Timeout);

        var logger = _serviceProvider.GetRequiredService<ILogger<OpenAIAdapter>>();
        
        return new OpenAIAdapter(httpClient, logger, config);
    }

    private static T ResolveEnvironmentVariables<T>(T config) where T : class
    {
        var json = JsonSerializer.Serialize(config);
        var resolved = Regex.Replace(json, @"\$\{([^}]+)\}", match =>
        {
            var envVar = match.Groups[1].Value;
            var value = Environment.GetEnvironmentVariable(envVar);
            if (string.IsNullOrEmpty(value))
            {
                throw new InvalidOperationException($"Environment variable {envVar} is not set");
            }
            return value;
        });
        return JsonSerializer.Deserialize<T>(resolved)!;
    }
}