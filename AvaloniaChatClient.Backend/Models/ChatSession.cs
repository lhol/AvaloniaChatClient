namespace AvaloniaChatClient.Backend.Models;

public record ChatMessage
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Role { get; init; } = string.Empty;  // "user" | "assistant" | "system"
    public string Content { get; init; } = string.Empty;
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    public long? TtftMs { get; init; }
    public long? TotalMs { get; init; }
    public int? InputTokens { get; init; }   // F-03: tokens/words of triggering user message
    public int? OutputTokens { get; init; }  // F-03: tokens/words of this assistant message
}

public record ChatSession
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Title { get; init; } = "Neue Session";
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
    Guid ServerId,
    string ModelId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    int MessageCount);

public record CreateSessionRequest(
    Guid ServerId,
    string ModelId,
    string? Title = null,
    List<Guid>? SkillIds = null);

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
    int? OutputTokens = null);

// Sent as a special SSE data event when streaming fails
public record SseErrorEvent(string ErrorMessage, Guid ErrorId);

