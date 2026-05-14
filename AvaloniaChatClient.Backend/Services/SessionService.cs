using System.Text.Json;
using AvaloniaChatClient.Backend.Models;

namespace AvaloniaChatClient.Backend.Services;

public class SessionService
{
    private readonly string _sessionsDir;
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    public SessionService(IConfiguration config)
    {
        var dataDir = config["DataDirectory"];
        if (string.IsNullOrWhiteSpace(dataDir))
            dataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AvaloniaChatClient");
        _sessionsDir = Path.Combine(dataDir, "sessions");
        Directory.CreateDirectory(_sessionsDir);
    }

    public async Task<IReadOnlyList<SessionSummary>> GetAllAsync()
    {
        var summaries = new List<SessionSummary>();
        foreach (var file in Directory.EnumerateFiles(_sessionsDir, "*.json"))
        {
            var session = await LoadFileAsync(file);
            if (session is not null)
                summaries.Add(ToSummary(session));
        }
        return summaries.OrderByDescending(s => s.UpdatedAt).ToList().AsReadOnly();
    }

    public async Task<ChatSession?> GetByIdAsync(Guid id)
        => await LoadFileAsync(FilePath(id));

    public async Task<ChatSession> CreateAsync(CreateSessionRequest request)
    {
        var session = new ChatSession
        {
            Id = Guid.NewGuid(),
            Title = request.Title ?? "Neue Session",
            Comment = request.Comment,
            Topic = request.Topic,
            ServerId = request.ServerId,
            ModelId = request.ModelId,
            SkillIds = request.SkillIds ?? []
        };
        await SaveAsync(session);
        return session;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var path = FilePath(id);
        if (!File.Exists(path)) return false;
        File.Delete(path);
        return true;
    }

    // G-04: update title, comment, serverId, modelId
    public async Task<ChatSession?> UpdateMetaAsync(Guid id, UpdateSessionMetaRequest request)
    {
        var session = await LoadFileAsync(FilePath(id));
        if (session is null) return null;
        session = session with
        {
            Title = request.Title ?? session.Title,
            Comment = request.Comment != null ? (request.Comment == "" ? null : request.Comment) : session.Comment,
            Topic = request.Topic != null ? (request.Topic == "" ? null : request.Topic) : session.Topic,
            ServerId = request.ServerId ?? session.ServerId,
            ModelId = request.ModelId ?? session.ModelId,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        await SaveAsync(session);
        return session;
    }

    public async Task<ChatMessage> AddMessageAsync(Guid sessionId, ChatMessage message)
    {
        var session = await LoadFileAsync(FilePath(sessionId))
            ?? throw new KeyNotFoundException($"Session {sessionId} not found");
        session.Messages.Add(message);
        session = session with { UpdatedAt = DateTimeOffset.UtcNow };
        await SaveAsync(session);
        return message;
    }

    public async Task UpdateLastAssistantMessageAsync(
        Guid sessionId, string fullContent, long ttftMs, long totalMs,
        int? inputTokens = null, int? outputTokens = null,
        string? serverName = null, string? modelName = null)
    {
        var session = await LoadFileAsync(FilePath(sessionId))
            ?? throw new KeyNotFoundException($"Session {sessionId} not found");

        var last = session.Messages.LastOrDefault(m => m.Role == "assistant");
        if (last is not null)
        {
            var index = session.Messages.IndexOf(last);
            session.Messages[index] = last with
            {
                Content = fullContent,
                TtftMs = ttftMs,
                TotalMs = totalMs,
                InputTokens = inputTokens,
                OutputTokens = outputTokens,
                ServerName = serverName,
                ModelName = modelName
            };
        }
        session = session with { UpdatedAt = DateTimeOffset.UtcNow };
        await SaveAsync(session);
    }

    private async Task<ChatSession?> LoadFileAsync(string path)
    {
        if (!File.Exists(path)) return null;
        var json = await File.ReadAllTextAsync(path);
        return JsonSerializer.Deserialize<ChatSession>(json, _jsonOptions);
    }

    private async Task SaveAsync(ChatSession session)
    {
        var json = JsonSerializer.Serialize(session, _jsonOptions);
        await File.WriteAllTextAsync(FilePath(session.Id), json);
    }

    private string FilePath(Guid id) => Path.Combine(_sessionsDir, $"{id}.json");

    private static SessionSummary ToSummary(ChatSession s) =>
        new(s.Id, s.Title, s.Comment, s.Topic, s.ServerId, s.ModelId, s.CreatedAt, s.UpdatedAt, s.Messages.Count);
}
