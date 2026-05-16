using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Avaiu.Models;
using Avaiu.Services;

namespace Avaiu.ViewModels;

// G-04: wraps SessionSummary for inline editing in HistoryView
public partial class SessionSummaryViewModel : ObservableObject
{
    private readonly BackendApiClient _api;
    private readonly HistoryViewModel _parent;

    public SessionSummary Summary { get; }

    [ObservableProperty] private string _title;
    [ObservableProperty] private string? _comment;
    [ObservableProperty] private string? _topic;
    [ObservableProperty] private bool _isEditing;
    [ObservableProperty] private string _editTitle = string.Empty;
    [ObservableProperty] private string _editComment = string.Empty;
    [ObservableProperty] private string _editTopic = string.Empty;

    public string TooltipText =>
        string.IsNullOrWhiteSpace(Comment)
            ? $"ID: {Summary.Id}"
            : $"ID: {Summary.Id}\n{Comment}";

    partial void OnCommentChanged(string? value) => OnPropertyChanged(nameof(TooltipText));

    public SessionSummaryViewModel(SessionSummary summary, BackendApiClient api, HistoryViewModel parent)
    {
        Summary = summary;
        _api = api;
        _parent = parent;
        _title = summary.Title;
        _comment = summary.Comment;
        _topic = summary.Topic;
    }

    [RelayCommand]
    private void StartEdit()
    {
        EditTitle = Title;
        EditComment = Comment ?? string.Empty;
        EditTopic = Topic ?? string.Empty;
        IsEditing = true;
    }

    [RelayCommand]
    private async Task SaveEditAsync()
    {
        try
        {
            await _api.UpdateSessionMetaAsync(Summary.Id, title: EditTitle, comment: EditComment,
                topic: string.IsNullOrWhiteSpace(EditTopic) ? "" : EditTopic);
            Title = EditTitle;
            Comment = string.IsNullOrWhiteSpace(EditComment) ? null : EditComment;
            Topic = string.IsNullOrWhiteSpace(EditTopic) ? null : EditTopic;
        }
        catch (Exception ex)
        {
            AppErrorService.Instance.Report("SessionSummaryViewModel.SaveEdit", ex);
        }
        finally { IsEditing = false; }
    }

    [RelayCommand]
    private void CancelEdit() => IsEditing = false;
}

public partial class HistoryViewModel : ViewModelBase
{
    private readonly BackendApiClient _api;
    public event Action<ChatSessionViewModel>? OpenSessionRequested;

    public TopLevel? TopLevel { get; set; }

    public System.Collections.ObjectModel.ObservableCollection<Guid> OpenSessionIds { get; set; } = [];

    [ObservableProperty] private ObservableCollection<SessionSummaryViewModel> _sessions = [];
    [ObservableProperty] private ObservableCollection<SkillSummary> _skills = [];
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string _statusText = string.Empty;
    [ObservableProperty] private SkillSummary? _selectedSkillItem;
    [ObservableProperty] private SkillContent? _selectedSkillContent;
    [ObservableProperty] private SessionSummaryViewModel? _selectedSession;

    public HistoryViewModel(BackendApiClient api)
    {
        _api = api;
    }

    partial void OnSelectedSkillItemChanged(SkillSummary? value)
    {
        SelectedSkillContent = null;
        if (value is not null)
            _ = LoadSelectedSkillContentAsync(value.Id);
    }

    private async Task LoadSelectedSkillContentAsync(Guid id)
    {
        try
        {
            SelectedSkillContent = await _api.GetSkillAsync(id);
        }
        catch (Exception ex)
        {
            AppErrorService.Instance.Report("HistoryViewModel.LoadSkillContent", ex);
        }
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            var sessions = await _api.GetSessionsAsync();
            Sessions = new ObservableCollection<SessionSummaryViewModel>(
                (sessions ?? []).Select(s => new SessionSummaryViewModel(s, _api, this)));
            var skills = await _api.GetSkillsAsync();
            Skills = new ObservableCollection<SkillSummary>(skills ?? []);
        }
        catch (Exception ex)
        {
            AppErrorService.Instance.Report("HistoryViewModel.Load", ex);
        }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private async Task OpenSessionAsync(SessionSummaryViewModel vm)
    {
        try
        {
            var session = await _api.GetSessionAsync(vm.Summary.Id);
            if (session is null) return;
            var sessionVm = new ChatSessionViewModel(_api, vm.Summary.Id, vm.Title, vm.Comment);
            sessionVm.Topic = vm.Topic;
            await sessionVm.LoadHistoryAsync();
            await sessionVm.LoadServersAsync();

            // Determine server/model: prefer session meta, then last assistant message metadata, then default/first
            var servers = sessionVm.AvailableServers;
            ServerProfile? server = servers.FirstOrDefault(s => s.Id == vm.Summary.ServerId);

            if (server is null)
            {
                // Try to find server name from last assistant message metadata
                var lastServerName = session.Messages
                    .Where(m => m.Role == "assistant" && !string.IsNullOrEmpty(m.ServerName))
                    .Select(m => m.ServerName)
                    .LastOrDefault();
                if (!string.IsNullOrEmpty(lastServerName))
                    server = servers.FirstOrDefault(s => s.Name == lastServerName);
            }

            // Fallback: server starting with "default"/"Default", then first
            if (server is null)
                server = servers.FirstOrDefault(s => s.Name.StartsWith("default", StringComparison.OrdinalIgnoreCase))
                      ?? servers.FirstOrDefault();

            if (server is not null)
            {
                sessionVm.SelectedServer = server;  // triggers LoadModelsAsync via OnSelectedServerChanged

                // Determine model: prefer session meta, then last message metadata
                string? modelId = string.IsNullOrEmpty(vm.Summary.ModelId) ? null : vm.Summary.ModelId;
                if (modelId is null)
                    modelId = session.Messages
                        .Where(m => m.Role == "assistant" && !string.IsNullOrEmpty(m.ModelName))
                        .Select(m => m.ModelName)
                        .LastOrDefault();

                if (!string.IsNullOrEmpty(modelId))
                    sessionVm.SelectedModel = modelId;
            }

            OpenSessionRequested?.Invoke(sessionVm);
        }
        catch (Exception ex)
        {
            AppErrorService.Instance.Report("HistoryViewModel.OpenSession", ex);
        }
    }

    [RelayCommand]
    private async Task DeleteSessionAsync(SessionSummaryViewModel vm)
    {
        try
        {
            if (await _api.DeleteSessionAsync(vm.Summary.Id))
            {
                Sessions.Remove(vm);
                StatusText = $"Session '{vm.Title}' gelöscht.";
            }
        }
        catch (Exception ex)
        {
            AppErrorService.Instance.Report("HistoryViewModel.DeleteSession", ex);
        }
    }

    [RelayCommand]
    private async Task ExportSkillAsync(SessionSummaryViewModel vm)
    {
        try
        {
            var result = await _api.ExportSessionAsSkillAsync(vm.Summary.Id);
            if (result is not null)
            {
                StatusText = $"Skill '{result.Title}' exportiert.";
                var skills = await _api.GetSkillsAsync();
                Skills = new ObservableCollection<SkillSummary>(skills ?? []);
            }
        }
        catch (Exception ex)
        {
            AppErrorService.Instance.Report("HistoryViewModel.ExportSkill", ex);
        }
    }

    [RelayCommand]
    private async Task DeleteSkillAsync(SkillSummary skill)
    {
        try
        {
            if (await _api.DeleteSkillAsync(skill.Id))
            {
                Skills.Remove(skill);
                StatusText = $"Skill '{skill.Title}' gelöscht.";
            }
        }
        catch (Exception ex)
        {
            AppErrorService.Instance.Report("HistoryViewModel.DeleteSkill", ex);
        }
    }

    [RelayCommand]
    private async Task DownloadJsonAsync(SessionSummaryViewModel vm)
    {
        try
        {
            var session = await _api.GetSessionAsync(vm.Summary.Id);
            if (session is null) return;

            var path = await PickSavePathAsync($"{Sanitize(vm.Title)}_{vm.Summary.CreatedAt:yyyyMMdd}.json", "JSON", "json");
            if (path is null) return;

            await ChatExporter.ExportToJsonAsync(session, path);
            StatusText = $"Exportiert: {Path.GetFileName(path)}";
        }
        catch (Exception ex)
        {
            AppErrorService.Instance.Report("HistoryViewModel.DownloadJson", ex);
        }
    }

    [RelayCommand]
    private async Task DownloadMarkdownAsync(SessionSummaryViewModel vm)
    {
        try
        {
            var session = await _api.GetSessionAsync(vm.Summary.Id);
            if (session is null) return;

            var path = await PickSavePathAsync($"{Sanitize(vm.Title)}_{vm.Summary.CreatedAt:yyyyMMdd}.md", "Markdown", "md");
            if (path is null) return;

            await ChatExporter.ExportToMarkdownAsync(session, path);
            StatusText = $"Exportiert: {Path.GetFileName(path)}";
        }
        catch (Exception ex)
        {
            AppErrorService.Instance.Report("HistoryViewModel.DownloadMarkdown", ex);
        }
    }

    private async Task<string?> PickSavePathAsync(string suggestedName, string typeName, string ext)
    {
        if (TopLevel is null) return null;
        var file = await TopLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = $"Session als {typeName} speichern",
            SuggestedFileName = suggestedName,
            FileTypeChoices =
            [
                new FilePickerFileType(typeName) { Patterns = [$"*.{ext}"] }
            ]
        });
        return file?.TryGetLocalPath();
    }

    private static string Sanitize(string name)
        => string.Concat(name.Split(Path.GetInvalidFileNameChars())).Replace(' ', '_');
}
