using satidotnet.Services;

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

var app = builder.Build();

// Configure middleware pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();

// Serve static files from wwwroot (equivalent to your client folder)
app.UseStaticFiles();

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

app.Run();

// Request model for testing
record TestPromptRequest(string Prompt, bool FormatJson = false);