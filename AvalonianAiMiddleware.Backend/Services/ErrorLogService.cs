using System.Collections.Concurrent;
using Avaimi.Backend.Models;

namespace Avaimi.Backend.Services;

/// <summary>
/// In-memory log for backend exceptions.
/// Accessible via GET /errors. Kept in RAM only (no persistence).
/// </summary>
public class ErrorLogService
{
    private readonly ConcurrentQueue<ErrorLogEntry> _entries = new();
    private const int MaxEntries = 500;

    public Guid Log(string context, Exception ex, string? userMessage = null)
    {
        var id = Guid.NewGuid();
        var entry = new ErrorLogEntry(
            id,
            DateTimeOffset.UtcNow,
            context,
            userMessage ?? ToUserMessage(ex),
            BuildDetails(ex));

        _entries.Enqueue(entry);

        // Keep bounded
        while (_entries.Count > MaxEntries)
            _entries.TryDequeue(out _);

        return id;
    }

    public IReadOnlyList<ErrorLogEntry> GetAll() => [.. _entries.Reverse()];

    public void Clear() => _entries.Clear();

    // ------------------------------------------------------------------

    public static string ToUserMessage(Exception ex) => ex switch
    {
        HttpRequestException => "Verbindung zum LLM-Server fehlgeschlagen.",
        TaskCanceledException => "Anfrage abgebrochen.",
        NotSupportedException => ex.Message,
        _ => "Interner Fehler – Details im Fehlerprotokoll."
    };

    private static string BuildDetails(Exception ex)
    {
        var sb = new System.Text.StringBuilder();
        var current = ex;
        while (current is not null)
        {
            sb.AppendLine($"{current.GetType().FullName}: {current.Message}");
            if (current.StackTrace is not null)
            {
                sb.AppendLine(current.StackTrace);
            }
            current = current.InnerException;
            if (current is not null) sb.AppendLine("--- Inner Exception ---");
        }
        return sb.ToString();
    }
}
