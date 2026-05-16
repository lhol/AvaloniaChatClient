using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Avaiu.Models;

namespace Avaiu.Services;

/// <summary>F-02: Exports a ChatSession to JSON or Markdown format.</summary>
public static class ChatExporter
{
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    public static async Task ExportToJsonAsync(ChatSession session, string filePath)
    {
        var json = JsonSerializer.Serialize(session, JsonOpts);
        await File.WriteAllTextAsync(filePath, json, Encoding.UTF8);
    }

    public static async Task ExportToMarkdownAsync(ChatSession session, string filePath)
    {
        var md = BuildMarkdown(session);
        await File.WriteAllTextAsync(filePath, md, Encoding.UTF8);
    }

    private static string BuildMarkdown(ChatSession session)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# {session.Title}");
        sb.AppendLine();
        if (!string.IsNullOrWhiteSpace(session.Comment))
        {
            sb.AppendLine($"> {session.Comment}");
            sb.AppendLine();
        }
        sb.AppendLine($"**Erstellt:** {session.CreatedAt:yyyy-MM-dd HH:mm}  ");
        sb.AppendLine($"**Modell:** {session.ModelId}  ");
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();

        foreach (var msg in session.Messages)
        {
            if (msg.Role == "system") continue;

            var role = msg.Role == "user" ? "**User**" : "**Assistant**";
            var timestamp = msg.Timestamp.ToLocalTime().ToString("HH:mm:ss");

            sb.Append($"{role} · {timestamp}");

            if (msg.Role == "assistant")
            {
                var meta = new StringBuilder();
                if (!string.IsNullOrEmpty(msg.ServerName)) meta.Append($" · Server: {msg.ServerName}");
                if (!string.IsNullOrEmpty(msg.ModelName)) meta.Append($" | Modell: {msg.ModelName}");
                if (msg.TtftMs.HasValue) meta.Append($" · TTFT: {msg.TtftMs} ms");
                if (msg.TotalMs.HasValue) meta.Append($" | Gesamt: {msg.TotalMs} ms");
                if (msg.OutputTokens.HasValue) meta.Append($" | Tokens: {msg.OutputTokens}");
                if (meta.Length > 0) sb.Append(meta);
            }

            sb.AppendLine();
            sb.AppendLine();
            sb.AppendLine(msg.Content);
            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine();
        }

        return sb.ToString();
    }
}
