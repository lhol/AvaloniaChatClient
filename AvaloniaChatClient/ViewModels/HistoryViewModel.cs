using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AvaloniaChatClient.Models;
using AvaloniaChatClient.Services;

namespace AvaloniaChatClient.ViewModels;

public partial class HistoryViewModel : ViewModelBase
{
    private readonly BackendApiClient _api;
    public event Action<ChatSessionViewModel>? OpenSessionRequested;

    // F-02: TopLevel needed for SaveFilePicker – set from code-behind
    public TopLevel? TopLevel { get; set; }

    // F-01: set by MainViewModel so HistoryView can bind to it
    public System.Collections.ObjectModel.ObservableCollection<Guid> OpenSessionIds { get; set; } = [];

    [ObservableProperty] private ObservableCollection<SessionSummary> _sessions = [];
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
            Sessions = new ObservableCollection<SessionSummary>(sessions ?? []);
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
    private async Task OpenSessionAsync(SessionSummary summary)
    {
        try
        {
            var session = await _api.GetSessionAsync(summary.Id);
            if (session is null) return;
            var vm = new ChatSessionViewModel(_api, summary.Id, summary.Title);
            await vm.LoadHistoryAsync();
            OpenSessionRequested?.Invoke(vm);
        }
        catch (Exception ex)
        {
            AppErrorService.Instance.Report("HistoryViewModel.OpenSession", ex);
        }
    }

    [RelayCommand]
    private async Task DeleteSessionAsync(SessionSummary summary)
    {
        try
        {
            if (await _api.DeleteSessionAsync(summary.Id))
            {
                Sessions.Remove(summary);
                StatusText = $"Session '{summary.Title}' gelöscht.";
            }
        }
        catch (Exception ex)
        {
            AppErrorService.Instance.Report("HistoryViewModel.DeleteSession", ex);
        }
    }

    [RelayCommand]
    private async Task ExportSkillAsync(SessionSummary summary)
    {
        try
        {
            var result = await _api.ExportSessionAsSkillAsync(summary.Id);
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

    // F-02: Download as JSON
    [RelayCommand]
    private async Task DownloadJsonAsync(SessionSummary summary)
    {
        try
        {
            var session = await _api.GetSessionAsync(summary.Id);
            if (session is null) return;

            var path = await PickSavePathAsync($"{Sanitize(summary.Title)}_{summary.CreatedAt:yyyyMMdd}.json", "JSON", "json");
            if (path is null) return;

            await ChatExporter.ExportToJsonAsync(session, path);
            StatusText = $"Exportiert: {Path.GetFileName(path)}";
        }
        catch (Exception ex)
        {
            AppErrorService.Instance.Report("HistoryViewModel.DownloadJson", ex);
        }
    }

    // F-02: Download as Markdown
    [RelayCommand]
    private async Task DownloadMarkdownAsync(SessionSummary summary)
    {
        try
        {
            var session = await _api.GetSessionAsync(summary.Id);
            if (session is null) return;

            var path = await PickSavePathAsync($"{Sanitize(summary.Title)}_{summary.CreatedAt:yyyyMMdd}.md", "Markdown", "md");
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
