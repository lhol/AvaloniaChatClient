using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AvaloniaChatClient.Models;
using AvaloniaChatClient.Services;

namespace AvaloniaChatClient.ViewModels;

public partial class ChatMessageViewModel : ObservableObject
{
    [ObservableProperty] private string _role;
    [ObservableProperty] private string _content;
    [ObservableProperty] private long? _ttftMs;
    [ObservableProperty] private long? _totalMs;
    [ObservableProperty] private int? _inputTokens;
    [ObservableProperty] private int? _outputTokens;

    public bool IsUser => Role == "user";
    public bool IsAssistant => Role == "assistant";

    public string MetadataText
    {
        get
        {
            var parts = new System.Collections.Generic.List<string>();
            if (TtftMs.HasValue) parts.Add($"TTFT: {TtftMs} ms");
            if (TotalMs.HasValue) parts.Add($"Gesamt: {TotalMs} ms");
            if (InputTokens.HasValue) parts.Add($"In: {InputTokens}");
            if (OutputTokens.HasValue) parts.Add($"Out: {OutputTokens}");
            return string.Join("  |  ", parts);
        }
    }

    public ChatMessageViewModel(string role, string content)
    {
        _role = role;
        _content = content;
    }

    partial void OnTtftMsChanged(long? value) => OnPropertyChanged(nameof(MetadataText));
    partial void OnTotalMsChanged(long? value) => OnPropertyChanged(nameof(MetadataText));
    partial void OnInputTokensChanged(int? value) => OnPropertyChanged(nameof(MetadataText));
    partial void OnOutputTokensChanged(int? value) => OnPropertyChanged(nameof(MetadataText));
}

public partial class ChatSessionViewModel : ViewModelBase
{
    private readonly BackendApiClient _api;
    private CancellationTokenSource? _streamCts;

    [ObservableProperty] private Guid _sessionId;
    [ObservableProperty] private string _title = "Neue Session";
    [ObservableProperty] private string _inputText = string.Empty;
    [ObservableProperty] private bool _isStreaming;
    [ObservableProperty] private long? _ttftMs;
    [ObservableProperty] private long? _totalMs;
    [ObservableProperty] private string _statusText = string.Empty;
    [ObservableProperty] private ObservableCollection<ChatMessageViewModel> _messages = [];
    [ObservableProperty] private bool _showMetadata = false;  // F-04

    public event Action? ScrollToBottomRequested;

    public ChatSessionViewModel(BackendApiClient api, Guid sessionId, string title)
    {
        _api = api;
        _sessionId = sessionId;
        _title = title;
    }

    [RelayCommand]
    public async Task LoadHistoryAsync()
    {
        try
        {
            var session = await _api.GetSessionAsync(SessionId);
            if (session is null) return;
            Messages.Clear();
            foreach (var msg in session.Messages.Where(m => m.Role != "system"))
                Messages.Add(new ChatMessageViewModel(msg.Role, msg.Content)
                {
                    TtftMs = msg.TtftMs,
                    TotalMs = msg.TotalMs,
                    InputTokens = msg.InputTokens,
                    OutputTokens = msg.OutputTokens
                });
        }
        catch (Exception ex)
        {
            AvaloniaChatClient.Services.AppErrorService.Instance.Report("ChatSessionViewModel.LoadHistory", ex);
        }
    }

    [RelayCommand(CanExecute = nameof(CanSend))]
    private async Task SendAsync()
    {
        var text = InputText.Trim();
        if (string.IsNullOrEmpty(text)) return;

        InputText = string.Empty;
        TtftMs = null;
        TotalMs = null;
        StatusText = string.Empty;

        Messages.Add(new ChatMessageViewModel("user", text));
        var assistantMsg = new ChatMessageViewModel("assistant", string.Empty);
        Messages.Add(assistantMsg);
        ScrollToBottomRequested?.Invoke();

        IsStreaming = true;
        _streamCts = new CancellationTokenSource();
        var sw = Stopwatch.StartNew();

        try
        {
            await foreach (var chunk in _api.SendMessageAsync(SessionId, text, _streamCts.Token))
            {
                if (chunk.IsDone)
                {
                    // If Delta carries an error message from the backend
                    if (chunk.Delta is not null && chunk.Delta.StartsWith("⚠"))
                    {
                        assistantMsg.Content = chunk.Delta;
                        StatusText = chunk.Delta;
                    }
                    else
                    {
                        TtftMs = chunk.TtftMs;
                        TotalMs = chunk.TotalMs;
                        assistantMsg.TtftMs = chunk.TtftMs;
                        assistantMsg.TotalMs = chunk.TotalMs;
                        assistantMsg.InputTokens = chunk.InputTokens;
                        assistantMsg.OutputTokens = chunk.OutputTokens;
                        StatusText = $"TTFT: {chunk.TtftMs} ms  |  Gesamt: {chunk.TotalMs} ms";
                    }
                    break;
                }
                if (chunk.Delta is not null)
                {
                    assistantMsg.Content += chunk.Delta;
                    if (TtftMs is null && chunk.TtftMs is not null)
                        TtftMs = chunk.TtftMs;
                    ScrollToBottomRequested?.Invoke();
                }
            }
        }
        catch (OperationCanceledException)
        {
            StatusText = "Abgebrochen.";
        }
        catch (Exception ex)
        {
            AvaloniaChatClient.Services.AppErrorService.Instance.Report("ChatSessionViewModel.Send", ex);
            StatusText = $"⚠ {ex.Message}";
            if (assistantMsg.Content == string.Empty)
                assistantMsg.Content = "⚠ Fehler – Details im Fehlerprotokoll.";
        }
        finally
        {
            IsStreaming = false;
            _streamCts?.Dispose();
            _streamCts = null;
        }
    }

    private bool CanSend() => !IsStreaming;

    [RelayCommand]
    private void StopStreaming() => _streamCts?.Cancel();

    [RelayCommand]
    private void ToggleMetadata() => ShowMetadata = !ShowMetadata;  // F-04

    partial void OnIsStreamingChanged(bool value) => SendCommand.NotifyCanExecuteChanged();
}
