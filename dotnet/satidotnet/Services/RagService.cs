using Microsoft.KernelMemory;
using System.Collections.Concurrent;

namespace satidotnet.Services;

public class RagService
{
    private readonly IKernelMemory _memory;
    private readonly ILogger<RagService> _logger;
    private readonly string _documentsPath;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private volatile bool _initialized = false;

    // Track indexed documents to avoid re-indexing
    private readonly ConcurrentDictionary<string, bool> _indexedDocuments = new();

    public RagService(ILogger<RagService> logger, PromptService promptService)
    {
        _logger = logger;
        _documentsPath = promptService.GetDocumentsPath();

        _memory = new KernelMemoryBuilder()
            .WithOllamaTextEmbeddingGeneration(
                modelName: "mistral",
                endpoint: "http://localhost:11434")
            .WithOllamaTextGeneration(
                modelName: "mistral",
                endpoint: "http://localhost:11434") // Add your preferred model
            .WithSimpleVectorDb(directory: "./rag-storage")
            .WithSimpleFileStorage(directory: "./rag-documents") 
            .Build<MemoryServerless>();

        _logger.LogInformation("RAG Service initialized with documents path: {Path}", _documentsPath);
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_initialized)
        {
            _logger.LogInformation("RAG already initialized, skipping");
            return;
        }

        await _initLock.WaitAsync(cancellationToken);
        try
        {
            // Double-check after acquiring lock
            if (_initialized)
                return;

            _logger.LogInformation("Starting RAG initialization...");

            if (!Directory.Exists(_documentsPath))
            {
                _logger.LogWarning("Documents directory not found: {Path}", _documentsPath);
                return;
            }

            var pdfFiles = Directory.GetFiles(_documentsPath, "*.pdf");
            _logger.LogInformation("Found {Count} PDF files to index", pdfFiles.Length);

            var tasks = pdfFiles.Select(file => IndexDocumentAsync(file, cancellationToken));
            await Task.WhenAll(tasks);

            _initialized = true;
            _logger.LogInformation("RAG initialization complete. Indexed {Count} documents", _indexedDocuments.Count);
        }
        finally
        {
            _initLock.Release();
        }
    }

    private async Task IndexDocumentAsync(string pdfFile, CancellationToken cancellationToken)
    {
        var fileName = Path.GetFileName(pdfFile);
        
        if (_indexedDocuments.ContainsKey(fileName))
        {
            _logger.LogDebug("Document already indexed: {FileName}", fileName);
            return;
        }

        try
        {
            _logger.LogInformation("Importing document: {FileName}", fileName);

            await _memory.ImportDocumentAsync(
                pdfFile,
                documentId: fileName,
                tags: new TagCollection { { "type", "tax-document" }, { "format", "pdf" } },
                cancellationToken: cancellationToken);

            _indexedDocuments.TryAdd(fileName, true);
            _logger.LogInformation("Successfully imported: {FileName}", fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to import document: {File}", pdfFile);
        }
    }

    public async Task<DocumentSearchResult?> SearchDocumentsAsync(
        string query, 
        int maxResults = 3,
        double minRelevance = 0.5,
        CancellationToken cancellationToken = default)
    {
        EnsureInitialized();

        try
        {
            var queryPreview = query.Length > 50 ? query[..50] + "..." : query;
            _logger.LogInformation("Searching documents for: {Query}", queryPreview);

            var searchResult = await _memory.SearchAsync(
                query: query,
                limit: maxResults,
                minRelevance: minRelevance,
                cancellationToken: cancellationToken);

            if (searchResult.Results.Count == 0)
            {
                _logger.LogInformation("No relevant documents found");
                return null;
            }

            _logger.LogInformation("Found {Count} relevant results", searchResult.Results.Count);
            return new DocumentSearchResult(searchResult);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching documents");
            return null;
        }
    }

    public async Task<string?> AskAsync(
        string question, 
        CancellationToken cancellationToken = default)
    {
        EnsureInitialized();

        try
        {
            _logger.LogInformation("Processing question: {Question}", 
                question.Length > 100 ? question[..100] + "..." : question);

            var answer = await _memory.AskAsync(
                question, 
                cancellationToken: cancellationToken);

            if (string.IsNullOrEmpty(answer.Result))
            {
                _logger.LogWarning("Received empty answer from RAG");
                return null;
            }

            _logger.LogInformation("Generated answer with {Sources} sources", 
                answer.RelevantSources?.Count ?? 0);
            
            return answer.Result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error asking RAG");
            return null;
        }
    }

    private void EnsureInitialized()
    {
        if (!_initialized)
        {
            throw new InvalidOperationException(
                "RAG service not initialized. Call InitializeAsync() first.");
        }
    }
}

// Helper class to encapsulate search results
public class DocumentSearchResult
{
    public IReadOnlyList<ResultItem> Results { get; }
    public int TotalResults => Results.Count;

    public DocumentSearchResult(SearchResult kernelMemoryResult)
    {
        Results = kernelMemoryResult.Results
            .SelectMany(
                r => r.Partitions,
                (r, p) => new ResultItem
                {
                    SourceName = r.SourceName,
                    Relevance = p.Relevance,
                    Text = p.Text,
                    DocumentId = r.DocumentId
                })
            .ToList();
    }

    public string FormatAsContext()
    {
        var context = new System.Text.StringBuilder();
        context.AppendLine("Relevant document excerpts:");
        context.AppendLine();

        foreach (var result in Results)
        {
            context.AppendLine($"Source: {result.SourceName}");
            context.AppendLine($"Relevance: {result.Relevance:F2}");
            context.AppendLine($"Content: {result.Text}");
            context.AppendLine("---");
        }

        return context.ToString();
    }
}

public class ResultItem
{
    public string SourceName { get; init; } = string.Empty;
    public double Relevance { get; init; }
    public string Text { get; init; } = string.Empty;
    public string DocumentId { get; init; } = string.Empty;
}