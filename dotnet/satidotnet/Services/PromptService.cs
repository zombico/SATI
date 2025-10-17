using System.Text;
using satidotnet.Models;

namespace satidotnet.Services;

public class PromptService
{
    private readonly ILogger<PromptService> _logger;
    private readonly string _configPath;
    private readonly string _documentsPath;
    private readonly string _solutionRoot;
    private string? _cachedInstructions;

    public PromptService(ILogger<PromptService> logger, IConfiguration configuration)
    {
        _logger = logger;
        
        // Get paths relative to the solution root (one level up from dotnet project)
        var projectRoot = Directory.GetCurrentDirectory();
        _logger.LogInformation("Project root: {ProjectRoot}", projectRoot);
        
        _solutionRoot = Path.GetFullPath(Path.Combine(projectRoot, ".."));
        _logger.LogInformation("Solution root: {SolutionRoot}", _solutionRoot);
        
        _configPath = Path.Combine(_solutionRoot, "config");
        _documentsPath = Path.Combine(_solutionRoot, "documents");
        
        _logger.LogInformation("Config path: {ConfigPath}", _configPath);
        _logger.LogInformation("Documents path: {DocumentsPath}", _documentsPath);
        
        // Check if paths exist
        _logger.LogInformation("Config path exists: {Exists}", Directory.Exists(_configPath));
        _logger.LogInformation("Documents path exists: {Exists}", Directory.Exists(_documentsPath));
    }

    public async Task<string> AssemblePromptAsync(
        string userPrompt, 
        List<ConversationTurn>? conversationHistory = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("AssemblePromptAsync called with prompt: {Prompt}", userPrompt.Substring(0, Math.Min(50, userPrompt.Length)));
        
        // Load instructions (cached after first load)
        var instructions = await GetInstructionsAsync(cancellationToken);
        _logger.LogInformation("Instructions loaded: {HasInstructions}, Length: {Length}", 
            !string.IsNullOrEmpty(instructions), instructions?.Length ?? 0);

        // Build conversation context if history exists
        var conversationContext = BuildConversationContext(conversationHistory);
        _logger.LogInformation("Conversation context built: {HasContext}, Length: {Length}", 
            !string.IsNullOrEmpty(conversationContext), conversationContext?.Length ?? 0);

        // Assemble full prompt
        var promptParts = new List<string>();
        
        if (!string.IsNullOrWhiteSpace(instructions))
        {
            promptParts.Add(instructions);
        }

        if (!string.IsNullOrWhiteSpace(conversationContext))
        {
            promptParts.Add(conversationContext);
        }

        promptParts.Add(userPrompt);

        var fullPrompt = string.Join("\n\n", promptParts);
        
        _logger.LogInformation("Assembled prompt with {PartCount} parts, total length: {Length}", 
            promptParts.Count, fullPrompt.Length);

        return fullPrompt;
    }

    private async Task<string> GetInstructionsAsync(CancellationToken cancellationToken)
    {
        // Return cached if available
        if (_cachedInstructions != null)
        {
            _logger.LogInformation("Returning cached instructions");
            return _cachedInstructions;
        }

        try
        {
            // Read config.json to get instructions path
            var configFilePath = Path.Combine(_configPath, "config.json");
            _logger.LogInformation("Looking for config file at: {Path}", configFilePath);
            
            if (!File.Exists(configFilePath))
            {
                _logger.LogWarning("config.json not found at {Path}", configFilePath);
                _logger.LogWarning("Files in config directory: {Files}", 
                    Directory.Exists(_configPath) ? string.Join(", ", Directory.GetFiles(_configPath)) : "Directory not found");
                return string.Empty;
            }

            var configJson = await File.ReadAllTextAsync(configFilePath, cancellationToken);
            _logger.LogInformation("Config JSON content (first 200 chars): {Content}", 
                configJson.Substring(0, Math.Min(200, configJson.Length)));
            
            var config = System.Text.Json.JsonSerializer.Deserialize<ConfigFile>(configJson);

            if (config?.Config?.Instructions == null)
            {
                _logger.LogWarning("Instructions path not found in config.json. Config object: {Config}", 
                    System.Text.Json.JsonSerializer.Serialize(config));
                return string.Empty;
            }

            // Load instructions file (path is relative to solution root)
            var instructionsPath = Path.GetFullPath(Path.Combine(_solutionRoot, config.Config.Instructions));
            _logger.LogInformation("Looking for instructions file at: {Path}", instructionsPath);
            
            if (!File.Exists(instructionsPath))
            {
                _logger.LogWarning("Instructions file not found at {Path}", instructionsPath);
                return string.Empty;
            }

            _cachedInstructions = await File.ReadAllTextAsync(instructionsPath, cancellationToken);
            _logger.LogInformation("Loaded instructions from {Path} ({Length} chars)", 
                instructionsPath, _cachedInstructions.Length);

            return _cachedInstructions;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading instructions");
            return string.Empty;
        }
    }

    private string BuildConversationContext(List<ConversationTurn>? conversationHistory)
    {
        if (conversationHistory == null || conversationHistory.Count == 0)
        {
            return string.Empty;
        }

        var sb = new StringBuilder();
        sb.AppendLine("\n\nPrevious conversation:");

        foreach (var turn in conversationHistory)
        {
            sb.AppendLine($"Turn {turn.Turn}:");
            sb.AppendLine($"User: {turn.UserPrompt}");
            sb.AppendLine($"Assistant: {turn.LlmResponse}");
            sb.AppendLine();
        }

        return sb.ToString().TrimEnd();
    }

    public string GetDocumentsPath() => _documentsPath;
    
    public void ClearInstructionsCache()
    {
        _cachedInstructions = null;
        _logger.LogInformation("Instructions cache cleared");
    }
}