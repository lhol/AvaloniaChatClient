namespace AvaloniaChatClient.Backend.Models;

public record ChatMessage
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Role { get; init; } = string.Empty;  // "user" | "assistant" | "system"
    public string Content { get; init; } = string.Empty;
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    public long? TtftMs { get; init; }
    public long? TotalMs { get; init; }
    public int? InputTokens { get; init; }
    public int? OutputTokens { get; init; }
    public string? ServerName { get; init; }   // G-09
    public string? ModelName { get; init; }    // G-09
}

public record ChatSession
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Title { get; init; } = "Neue Session";
    public string? Comment { get; init; }       // G-03
    public string? Topic { get; init; }          // v0.3
    public Guid ServerId { get; init; }
    public string ModelId { get; init; } = string.Empty;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public List<Guid> SkillIds { get; init; } = [];
    public List<ChatMessage> Messages { get; init; } = [];
}

public record SessionSummary(
    Guid Id,
    string Title,
    string? Comment,       // G-03
    string? Topic,         // v0.3
    Guid ServerId,
    string ModelId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    int MessageCount);

public record CreateSessionRequest(
    Guid ServerId,
    string ModelId,
    string? Title = null,
    string? Comment = null,   // G-03
    string? Topic = null,     // v0.3
    List<Guid>? SkillIds = null);

// G-04: PATCH /sessions/{id}/meta
public record UpdateSessionMetaRequest(
    string? Title = null,
    string? Comment = null,
    string? Topic = null,     // v0.3
    Guid? ServerId = null,
    string? ModelId = null);

public record SendMessageRequest(
    string Content,
    List<FileAttachmentDto>? Files = null);

public record FileAttachmentDto(
    string FileName,
    string MimeType,
    string Base64Data);

public record ExportSkillResponse(
    Guid SkillId,
    string Title);

public record SseChunk(
    string? Delta,
    bool IsDone,
    long? TtftMs,
    long? TotalMs,
    int? InputTokens = null,
    int? OutputTokens = null,
    string? ServerName = null,   // G-09
    string? ModelName = null);   // G-09

// Sent as a special SSE data event when streaming fails
public record SseErrorEvent(string ErrorMessage, Guid ErrorId);

