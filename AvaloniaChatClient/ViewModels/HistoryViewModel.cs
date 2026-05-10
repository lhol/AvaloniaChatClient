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
using AvaloniaChatClient.Models;
using AvaloniaChatClient.Services;

namespace AvaloniaChatClient.ViewModels;

// G-04: wraps SessionSummary for inline editing in HistoryView
public partial class SessionSummaryViewModel : ObservableObject
{
    private readonly BackendApiClient _api;
    private readonly HistoryViewModel _parent;

    public SessionSummary Summary { get; }

    [ObservableProperty] private string _title;
    [ObservableProperty] private string? _comment;
    [ObservableProperty] private bool _isEditing;
    [ObservableProperty] private string _editTitle = string.Empty;
    [ObservableProperty] private string _editComment = string.Empty;

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
    }

    [RelayCommand]
    private void StartEdit()
    {
        EditTitle = Title;
        EditComment = Comment ?? string.Empty;
        IsEditing = true;
    }

    [RelayCommand]
    private async Task SaveEditAsync()
    {
        try
        {
            await _api.UpdateSessionMetaAsync(Summary.Id, title: EditTitle, comment: EditComment);
            Title = EditTitle;
            Comment = string.IsNullOrWhiteSpace(EditComment) ? null : EditComment;
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

    public HistoryViewModel(BackendApiClient api)
    {
        _api = api;
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
            await sessionVm.LoadHistoryAsync();
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
