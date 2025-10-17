using System.Text.Json;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.FileProviders;
using OllamaSharp.Models.Chat;
using satidotnet.Services;
using satidotnet.Models;

var builder = WebApplication.CreateBuilder(args);

// Configure services
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader();
    });
});

// Register HttpClient for OllamaService
builder.Services.AddHttpClient<OllamaService>(client =>
{
    client.BaseAddress = new Uri("http://localhost:11434");
    client.Timeout = TimeSpan.FromMinutes(5);
});

builder.Services.AddSingleton<PromptService>();
builder.Services.AddSingleton<ConversationService>();

var app = builder.Build();

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

// Test endpoint for OllamaService
app.MapPost("/test-Ollama", async (OllamaService OllamaService, TestPromptRequest request) =>
{
    try
    {
        var response = await OllamaService.GenerateAsync(request.Prompt, formatJson: request.FormatJson);
        return Results.Ok(new
        {
            response = response.Response,
            trace = response.Trace
        });
    }
    catch (OllamaServiceException ex)
    {
        return Results.Problem(
            title: "LLM Service Error",
            detail: ex.Message,
            statusCode: 500
        );
    }
});

// Main chat endpoint - orchestrates everything
app.MapPost("/chat", async (
    ChatRequest request,
    OllamaService llamaService,
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

        // Assemble the full prompt with instructions and history
        var fullPrompt = await promptService.AssemblePromptAsync(
            request.Prompt,
            conversationHistory);

        // Call LLM
        var llmResult = await llamaService.GenerateAsync(fullPrompt, formatJson: true);

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
            fullPrompt: fullPrompt,
            llmResponse: llmResult.Response,
            machineState: llmResult.Response, // Store the full JSON as machine state
            ragContext: null
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
    catch (OllamaServiceException ex)
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

// Request model for testing
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