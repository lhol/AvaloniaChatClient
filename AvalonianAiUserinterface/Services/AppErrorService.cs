using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Avalonia.Threading;

namespace Avaiu.Services;

/// <summary>
/// Frontend-seitiger In-Memory Fehlerspeicher.
/// Wird von allen ViewModels verwendet um Exceptions zu melden, ohne das Frontend zum Absturz zu bringen.
/// </summary>
public class AppErrorService
{
    public static readonly AppErrorService Instance = new();

    private AppErrorService() { }

    public ObservableCollection<AppError> Errors { get; } = [];

    public void Report(string context, Exception ex)
    {
        var entry = new AppError(
            Guid.NewGuid(),
            DateTimeOffset.Now,
            context,
            ToUserMessage(ex),
            BuildDetails(ex));

        Dispatcher.UIThread.Post(() => Errors.Add(entry));
    }

    public void Clear() => Dispatcher.UIThread.Post(() => Errors.Clear());

    private static string ToUserMessage(Exception ex) => ex switch
    {
        System.Net.Http.HttpRequestException => "Verbindung zum Backend fehlgeschlagen.",
        TaskCanceledException => "Anfrage abgebrochen.",
        OperationCanceledException => "Anfrage abgebrochen.",
        _ => "Unerwarteter Fehler – Details im Fehlerprotokoll."
    };

    private static string BuildDetails(Exception ex)
    {
        var sb = new System.Text.StringBuilder();
        var cur = ex;
        while (cur is not null)
        {
            sb.AppendLine($"{cur.GetType().FullName}: {cur.Message}");
            if (cur.StackTrace is not null) sb.AppendLine(cur.StackTrace);
            cur = cur.InnerException;
            if (cur is not null) sb.AppendLine("--- Inner Exception ---");
        }
        return sb.ToString();
    }
}

public record AppError(Guid Id, DateTimeOffset Timestamp, string Context, string UserMessage, string FullDetails);
