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
    [ObservableProperty] private string? _serverName;   // G-09
    [ObservableProperty] private string? _modelName;    // G-09
    [ObservableProperty] private bool _showMetadata;    // driven by parent ShowMetadata

    public bool IsUser => Role == "user";
    public bool IsAssistant => Role == "assistant";

    public string MetadataText
    {
        get
        {
            var parts = new System.Collections.Generic.List<string>();
            if (!string.IsNullOrEmpty(ServerName)) parts.Add($"Server: {ServerName}");
            if (!string.IsNullOrEmpty(ModelName)) parts.Add($"Modell: {ModelName}");
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
    partial void OnServerNameChanged(string? value) => OnPropertyChanged(nameof(MetadataText));
    partial void OnModelNameChanged(string? value) => OnPropertyChanged(nameof(MetadataText));
}

public partial class ChatSessionViewModel : ViewModelBase
{
    private readonly BackendApiClient _api;
    private CancellationTokenSource? _streamCts;

    [ObservableProperty] private Guid _sessionId;
    [ObservableProperty] private string _title = "Neue Session";
    [ObservableProperty] private string? _comment;                    // G-03
    [ObservableProperty] private bool _isActive;                      // G-02
    [ObservableProperty] private string _inputText = string.Empty;
    [ObservableProperty] private bool _isStreaming;
    [ObservableProperty] private long? _ttftMs;
    [ObservableProperty] private long? _totalMs;
    [ObservableProperty] private string _statusText = string.Empty;
    [ObservableProperty] private ObservableCollection<ChatMessageViewModel> _messages = [];
    [ObservableProperty] private bool _showMetadata = false;
    [ObservableProperty] private bool _multilineInput = false;        // G-05

    // G-04: inline meta editing
    [ObservableProperty] private bool _isEditingMeta = false;
    [ObservableProperty] private string _editTitle = string.Empty;
    [ObservableProperty] private string _editComment = string.Empty;

    // G-07: server/model selection
    [ObservableProperty] private ObservableCollection<ServerProfile> _availableServers = [];
    [ObservableProperty] private ServerProfile? _selectedServer;
    [ObservableProperty] private ObservableCollection<string> _availableModels = [];
    [ObservableProperty] private string? _selectedModel;
    [ObservableProperty] private bool _isLoadingModels;
    [ObservableProperty] private bool _showServerChooser = false;  // new: toggle row visibility

    public event Action? ScrollToBottomRequested;

    public ChatSessionViewModel(BackendApiClient api, Guid sessionId, string title, string? comment = null)
    {
        _api = api;
        _sessionId = sessionId;
        _title = title;
        _comment = comment;
    }

    // G-03: tooltip text for session tab
    public string TabTooltip =>
        string.IsNullOrWhiteSpace(Comment)
            ? $"ID: {SessionId}"
            : $"ID: {SessionId}\n{Comment}";

    partial void OnCommentChanged(string? value) => OnPropertyChanged(nameof(TabTooltip));
    partial void OnSessionIdChanged(Guid value) => OnPropertyChanged(nameof(TabTooltip));

    [RelayCommand]
    public async Task LoadHistoryAsync()
    {
        try
        {
            var session = await _api.GetSessionAsync(SessionId);
            if (session is null) return;
            Comment = session.Comment;
            Messages.Clear();
            foreach (var msg in session.Messages.Where(m => m.Role != "system"))
                Messages.Add(new ChatMessageViewModel(msg.Role, msg.Content)
                {
                    TtftMs = msg.TtftMs,
                    TotalMs = msg.TotalMs,
                    InputTokens = msg.InputTokens,
                    OutputTokens = msg.OutputTokens,
                    ServerName = msg.ServerName,
                    ModelName = msg.ModelName,
                    ShowMetadata = ShowMetadata
                });
        }
        catch (Exception ex)
        {
            AvaloniaChatClient.Services.AppErrorService.Instance.Report("ChatSessionViewModel.LoadHistory", ex);
        }
    }

    // G-07: load servers for dropdown
    public async Task LoadServersAsync()
    {
        try
        {
            var servers = await _api.GetServersAsync();
            AvailableServers = new ObservableCollection<ServerProfile>(servers ?? []);
        }
        catch (Exception ex)
        {
            AppErrorService.Instance.Report("ChatSessionViewModel.LoadServers", ex);
        }
    }

    // G-07/G-08: load models for selected server
    [RelayCommand]
    public async Task LoadModelsAsync()
    {
        if (SelectedServer is null) return;
        IsLoadingModels = true;
        try
        {
            var models = await _api.GetModelsAsync(SelectedServer.Id);
            AvailableModels = new ObservableCollection<string>(models);
            if (SelectedModel is null || !AvailableModels.Contains(SelectedModel))
                SelectedModel = AvailableModels.FirstOrDefault() ?? SelectedServer.DefaultModel;
        }
        catch (Exception ex)
        {
            AppErrorService.Instance.Report("ChatSessionViewModel.LoadModels", ex);
        }
        finally { IsLoadingModels = false; }
    }

    partial void OnSelectedServerChanged(ServerProfile? value)
    {
        if (value is not null)
            _ = LoadModelsAsync();
    }

    partial void OnSelectedModelChanged(string? value)
    {
        if (value is not null && SelectedServer is not null)
            _ = SaveServerModelAsync();
    }

    private async Task SaveServerModelAsync()
    {
        try
        {
            await _api.UpdateSessionMetaAsync(SessionId, modelId: SelectedModel,
                serverId: SelectedServer?.Id);
        }
        catch (Exception ex)
        {
            AppErrorService.Instance.Report("ChatSessionViewModel.SaveServerModel", ex);
        }
    }

    // G-04: start/save/cancel meta edit
    [RelayCommand]
    private void StartEditMeta()
    {
        EditTitle = Title;
        EditComment = Comment ?? string.Empty;
        IsEditingMeta = true;
    }

    [RelayCommand]
    private async Task SaveMetaAsync()
    {
        try
        {
            var updated = await _api.UpdateSessionMetaAsync(SessionId,
                title: EditTitle, comment: EditComment);
            if (updated is not null)
            {
                Title = updated.Title;
                Comment = updated.Comment;
            }
        }
        catch (Exception ex)
        {
            AppErrorService.Instance.Report("ChatSessionViewModel.SaveMeta", ex);
        }
        finally { IsEditingMeta = false; }
    }

    [RelayCommand]
    private void CancelEditMeta() => IsEditingMeta = false;

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
        var assistantMsg = new ChatMessageViewModel("assistant", string.Empty) { ShowMetadata = ShowMetadata };
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
                        assistantMsg.ServerName = chunk.ServerName;  // G-09
                        assistantMsg.ModelName = chunk.ModelName;    // G-09
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
    private void ToggleMetadata()
    {
        ShowMetadata = !ShowMetadata;
        foreach (var m in Messages) m.ShowMetadata = ShowMetadata;
    }

    [RelayCommand]
    private void ToggleMultiline() => MultilineInput = !MultilineInput;  // G-05

    [RelayCommand]
    private void ToggleServerChooser() => ShowServerChooser = !ShowServerChooser;

    partial void OnIsStreamingChanged(bool value) => SendCommand.NotifyCanExecuteChanged();
}
