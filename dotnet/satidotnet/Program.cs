using System.Text.Json;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.FileProviders;
using OllamaSharp.Models.Chat;
using satidotnet.Services;
using satidotnet.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader();
    });
});

// Just add HttpClientFactory - adapters are created on-demand
builder.Services.AddHttpClient();

// Register services - simple!
builder.Services.AddSingleton<LLMService>();
builder.Services.AddSingleton<PromptService>();
builder.Services.AddSingleton<ConversationService>();
builder.Services.AddSingleton<RagService>();

var app = builder.Build();

// get rag on startup
var ragServiceStartup = app.Services.GetRequiredService<RagService>();
await ragServiceStartup.InitializeAsync();

// Configure middleware pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();

// Serve static files from external client folder
var solutionRoot = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", ".."));
var clientPath = Path.Combine(solutionRoot, "client");

app.UseDefaultFiles(new DefaultFilesOptions
{
    FileProvider = new PhysicalFileProvider(clientPath),
    RequestPath = ""
});

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(clientPath),
    RequestPath = ""
});

// Simple health check endpoint
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }))
    .WithName("HealthCheck")
    .WithOpenApi();

// Main chat endpoint - orchestrates everything
app.MapPost("/chat", async (
    ChatRequest request,
    LLMService llmService,  // ← Changed from OllamaAdapter
    RagService ragService,
    PromptService promptService,
    ConversationService conversationService,
    ILogger<Program> logger) =>
{
    try
    {
        // Generate conversationId if new conversation
        var conversationId = request.ConversationId ?? Guid.NewGuid().ToString();
        
        // Load conversation history if continuing
        List<ConversationTurn>? conversationHistory = null;
        if (!string.IsNullOrEmpty(request.ConversationId) && request.IncludeHistory)
        {
            conversationHistory = conversationService.GetConversationHistory(conversationId);
            logger.LogInformation(
                "Loaded {Count} previous turns for conversation {ConversationId}",
                conversationHistory.Count, conversationId);
        }
        
        // Search for relevant documents using RAG
        string? ragContext = null;
        DocumentSearchResult? searchResults = null;
        
        try
        {
            searchResults = await ragService.SearchDocumentsAsync(
                query: request.Prompt,
                maxResults: 3,
                minRelevance: 0.3);

            if (searchResults != null)
            {
                ragContext = searchResults.FormatAsContext();
                logger.LogInformation(
                    "Found {Count} relevant documents for RAG context",
                    searchResults.TotalResults);
            }
            else
            {
                logger.LogInformation("No relevant documents found for query");
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "RAG search failed, continuing without context");
        }

        // Assemble the full prompt with instructions and history
        var fullPrompt = await promptService.AssemblePromptAsync(
            request.Prompt,
            conversationHistory,
            ragContext);

        // Call LLM - now using LLMService which handles provider selection
        var llmResult = await llmService.GenerateAsync(fullPrompt.FullPrompt);  // ← Removed formatJson parameter

        // Parse the JSON response
        JsonDocument? satiJson = null;
        try
        {
            satiJson = JsonDocument.Parse(llmResult.Response.Trim());
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "Failed to parse LLM response as JSON");
            return Results.Problem(
                title: "Invalid LLM Response",
                detail: "The LLM did not return valid JSON",
                statusCode: 500);
        }

        var nextTurn = request.Turn + 1;

        // Save to database
        conversationService.InsertTurn(
            conversationId: conversationId,
            turn: nextTurn,
            userPrompt: request.Prompt,
            fullPrompt: fullPrompt.FullPrompt,
            llmResponse: llmResult.Response,
            machineState: llmResult.Response,
            ragContext: ragContext  // ← Store RAG context if you want
        );

        // Get the chain hash for this turn
        var chainHash = conversationService.GetChainHashForTurn(conversationId, nextTurn);
        var hashMsg = $"Chain: {chainHash}";
        logger.LogInformation(hashMsg);

        return Results.Ok(new ChatResponse
        {
            Response = satiJson.RootElement,
            ConversationId = conversationId,
            Turn = nextTurn,
            Trace = llmResult.Trace,
            HashMsg = hashMsg
        });
    }
    catch (LLMAdapterException ex)  // ← Changed exception type
    {
        logger.LogError(ex, "LLM service error");
        return Results.Problem(
            title: "LLM Service Error",
            detail: ex.Message,
            statusCode: 500);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Unexpected error in /chat endpoint");
        return Results.Problem(
            title: "Internal Server Error",
            detail: "An unexpected error occurred",
            statusCode: 500);
    }
})
.WithName("Chat")
.WithOpenApi();

app.MapGet("/context", () =>
{
    try
    {
        var relativePath = Path.Combine(
            Directory.GetCurrentDirectory(), 
            "..", 
            "..", 
            "config",
            "config.json"
        );
        
        var configPath = Path.GetFullPath(relativePath);
        var jsonString = File.ReadAllText(configPath);
        
        // Parse the full JSON
        var fullConfig = JsonSerializer.Deserialize<JsonElement>(jsonString);
        
        // Extract just the "config" property
        if (fullConfig.TryGetProperty("config", out var configProperty))
        {
            return Results.Ok(configProperty);
        }
        
        return Results.NotFound("Config property not found in JSON");
    }
    catch (Exception ex)
    {
        return Results.Problem($"Error reading config: {ex.Message}");
    }
    //var relativePath = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "config", "config.json");
    //var configPath = Path.GetFullPath(relativePath);
    //var jsonString = File.ReadAllText(configPath);
    //var root = JsonSerializer.Deserialize<RootConfig>(jsonString);
    //Console.WriteLine(root.Config);
    //return Results.Ok(root.Config);
});

// Verify chain integrity
app.MapGet("/verify/{conversationId?}", (string? conversationId, ConversationService conversationService) =>
{
    var result = conversationService.VerifyChain(conversationId);
    return Results.Ok(result);
})
.WithName("VerifyChain")
.WithOpenApi();

// Handle graceful shutdown
var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
lifetime.ApplicationStopping.Register(() =>
{
    var conversationService = app.Services.GetRequiredService<ConversationService>();
    conversationService.Dispose();
    app.Logger.LogInformation("Database connection closed");
});

app.Run();

// Request models
record TestPromptRequest(string Prompt, bool FormatJson = false);

record ChatRequest
{
    public string Prompt { get; init; } = string.Empty;
    public int Turn { get; init; } = 0;
    public string? ConversationId { get; init; }
    public bool IncludeHistory { get; init; } = true;
}

record ChatResponse
{
    public JsonElement Response { get; set; }
    public string ConversationId { get; set; } = string.Empty;
    public int Turn { get; set; }
    public RequestTrace Trace { get; set; } = new();
    public string HashMsg { get; set; } = string.Empty;
};

public class ConfigResponse
{
    public string DocumentsPath { get; set; }
    public string Instructions { get; set; }
    public string Name { get; set; }
    public string ChatDisplayName { get; set; }
    public string ButtonText { get; set; }
    public string Placeholder { get; set; }
    public string FirstMessage { get; set; }
}

public class RootConfig
{
    public ConfigResponse Config { get; set; }
}