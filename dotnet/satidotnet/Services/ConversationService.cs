using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using satidotnet.Models;

namespace satidotnet.Services;

public class ConversationService : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ILogger<ConversationService> _logger;

    public ConversationService(ILogger<ConversationService> logger, IConfiguration configuration)
    {
        _logger = logger;
        
        var dbPath = configuration["Database:Path"] ?? "./conversation.db";
        _connection = new SqliteConnection($"Data Source={dbPath}");
        _connection.Open();
        
        InitializeDatabase();
        _logger.LogInformation("Database initialized: {DbPath}", dbPath);
    }

    private void InitializeDatabase()
    {
        var createTableSql = @"
            CREATE TABLE IF NOT EXISTS turns (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                conversation_id TEXT NOT NULL,
                turn INTEGER NOT NULL,
                timestamp DATETIME DEFAULT CURRENT_TIMESTAMP,
                user_prompt TEXT NOT NULL,
                full_prompt TEXT,
                llm_response TEXT,
                machine_state TEXT,
                rag_context TEXT,
                content_hash TEXT NOT NULL,
                chain_hash TEXT NOT NULL
            )";

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = createTableSql;
        cmd.ExecuteNonQuery();
    }

    public void InsertTurn(
        string conversationId,
        int turn,
        string userPrompt,
        string? fullPrompt,
        string llmResponse,
        string? machineState,
        string? ragContext)
    {
        // Generate hashes
        var contentHash = HashTurnContent(turn, userPrompt, llmResponse, machineState);
        var previousChainHash = GetLastChainHash(conversationId);
        var chainHash = CreateChainHash(contentHash, previousChainHash);

        var sql = @"
            INSERT INTO turns (
                conversation_id, turn, user_prompt, full_prompt, llm_response, 
                machine_state, rag_context, content_hash, chain_hash
            ) VALUES (
                @conversationId, @turn, @userPrompt, @fullPrompt, @llmResponse,
                @machineState, @ragContext, @contentHash, @chainHash
            )";

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@conversationId", conversationId);
        cmd.Parameters.AddWithValue("@turn", turn);
        cmd.Parameters.AddWithValue("@userPrompt", userPrompt);
        cmd.Parameters.AddWithValue("@fullPrompt", fullPrompt ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@llmResponse", llmResponse);
        cmd.Parameters.AddWithValue("@machineState", machineState ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@ragContext", ragContext ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@contentHash", contentHash);
        cmd.Parameters.AddWithValue("@chainHash", chainHash);

        cmd.ExecuteNonQuery();

        _logger.LogInformation(
            "Inserted turn {Turn} for conversation {ConversationId}, chain hash: {ChainHash}",
            turn, conversationId, chainHash);
    }

    public List<ConversationTurn> GetConversationHistory(string conversationId, int? maxTurns = null)
    {
        var sql = maxTurns.HasValue
            ? "SELECT turn, user_prompt, llm_response FROM turns WHERE conversation_id = @conversationId ORDER BY turn ASC LIMIT @maxTurns"
            : "SELECT turn, user_prompt, llm_response FROM turns WHERE conversation_id = @conversationId ORDER BY turn ASC";

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@conversationId", conversationId);
        if (maxTurns.HasValue)
        {
            cmd.Parameters.AddWithValue("@maxTurns", maxTurns.Value);
        }

        var history = new List<ConversationTurn>();
        using var reader = cmd.ExecuteReader();
        
        while (reader.Read())
        {
            history.Add(new ConversationTurn
            {
                Turn = reader.GetInt32(0),
                UserPrompt = reader.GetString(1),
                LlmResponse = reader.GetString(2)
            });
        }

        return history;
    }
    
    public string? GetChainHashForTurn(string conversationId, int turn)
{
    var sql = "SELECT chain_hash FROM turns WHERE conversation_id = @conversationId AND turn = @turn LIMIT 1";
    
    using var cmd = _connection.CreateCommand();
    cmd.CommandText = sql;
    cmd.Parameters.AddWithValue("@conversationId", conversationId);
    cmd.Parameters.AddWithValue("@turn", turn);
    
    var result = cmd.ExecuteScalar();
    return result?.ToString();
}

    public VerificationResult VerifyChain(string? conversationId = null)
    {
        var sql = conversationId != null
            ? @"SELECT id, turn, conversation_id, user_prompt, llm_response, machine_state, 
                       content_hash, chain_hash 
                FROM turns 
                WHERE conversation_id = @conversationId 
                ORDER BY turn ASC"
            : @"SELECT id, turn, conversation_id, user_prompt, llm_response, machine_state, 
                       content_hash, chain_hash 
                FROM turns 
                ORDER BY conversation_id, turn ASC";

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = sql;
        if (conversationId != null)
        {
            cmd.Parameters.AddWithValue("@conversationId", conversationId);
        }

        var turns = new List<TurnRecord>();
        using (var reader = cmd.ExecuteReader())
        {
            while (reader.Read())
            {
                turns.Add(new TurnRecord
                {
                    Id = reader.GetInt32(0),
                    Turn = reader.GetInt32(1),
                    ConversationId = reader.GetString(2),
                    UserPrompt = reader.GetString(3),
                    LlmResponse = reader.GetString(4),
                    MachineState = reader.IsDBNull(5) ? null : reader.GetString(5),
                    ContentHash = reader.GetString(6),
                    ChainHash = reader.GetString(7)
                });
            }
        }

        if (turns.Count == 0)
        {
            return new VerificationResult
            {
                Valid = true,
                TotalTurns = 0,
                InvalidTurns = 0,
                ConversationsVerified = 0,
                Message = "No turns to verify"
            };
        }

        // Track chain per conversation
        var chainMap = new Dictionary<string, string>();
        var isValid = true;
        var invalidCount = 0;

        foreach (var turn in turns)
        {
            var prevHash = chainMap.GetValueOrDefault(turn.ConversationId);

            // Verify content hash
            var expectedContentHash = HashTurnContent(
                turn.Turn,
                turn.UserPrompt,
                turn.LlmResponse,
                turn.MachineState
            );
            var contentValid = expectedContentHash == turn.ContentHash;

            // Verify chain hash
            var expectedChainHash = CreateChainHash(turn.ContentHash, prevHash);
            var chainValid = expectedChainHash == turn.ChainHash;

            if (!contentValid || !chainValid)
            {
                isValid = false;
                invalidCount++;
                _logger.LogWarning(
                    "Invalid turn detected - ID: {Id}, Turn: {Turn}, Conversation: {ConversationId}",
                    turn.Id, turn.Turn, turn.ConversationId);
            }

            chainMap[turn.ConversationId] = turn.ChainHash;
        }

        return new VerificationResult
        {
            Valid = isValid,
            TotalTurns = turns.Count,
            InvalidTurns = invalidCount,
            ConversationsVerified = chainMap.Count
        };
    }

    private string? GetLastChainHash(string conversationId)
    {
        var sql = "SELECT chain_hash FROM turns WHERE conversation_id = @conversationId ORDER BY turn DESC LIMIT 1";
        
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@conversationId", conversationId);
        
        var result = cmd.ExecuteScalar();
        return result?.ToString();
    }

    private string HashTurnContent(int turn, string userPrompt, string llmResponse, string? machineState)
    {
        var content = JsonSerializer.Serialize(new
        {
            turn,
            userPrompt,
            llmResponse,
            machineState
        });

        return ComputeSha256(content);
    }

    private string CreateChainHash(string contentHash, string? previousChainHash)
    {
        var combined = contentHash + (previousChainHash ?? "0");
        return ComputeSha256(combined);
    }

    private string ComputeSha256(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public void Dispose()
    {
        _connection?.Dispose();
    }
}

// Models
public class TurnRecord
{
    public int Id { get; set; }
    public int Turn { get; set; }
    public string ConversationId { get; set; } = string.Empty;
    public string UserPrompt { get; set; } = string.Empty;
    public string LlmResponse { get; set; } = string.Empty;
    public string? MachineState { get; set; }
    public string ContentHash { get; set; } = string.Empty;
    public string ChainHash { get; set; } = string.Empty;
}

public class VerificationResult
{
    public bool Valid { get; set; }
    public int TotalTurns { get; set; }
    public int InvalidTurns { get; set; }
    public int ConversationsVerified { get; set; }
    public string? Message { get; set; }
}