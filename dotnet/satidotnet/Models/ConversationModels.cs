namespace satidotnet.Models;

// Conversation turn for history
public class ConversationTurn
{
    public int Turn { get; set; }
    public string UserPrompt { get; set; } = string.Empty;
    public string LlmResponse { get; set; } = string.Empty;
}

// Full turn record from database
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

// Verification result
public class VerificationResult
{
    public bool Valid { get; set; }
    public int TotalTurns { get; set; }
    public int InvalidTurns { get; set; }
    public int ConversationsVerified { get; set; }
    public string? Message { get; set; }
}

// Config file structure
public class ConfigFile
{
    public ConfigSection? Config { get; set; }
}

public class ConfigSection
{
    public string? Instructions { get; set; }
    public string? DocumentsPath { get; set; }
}

