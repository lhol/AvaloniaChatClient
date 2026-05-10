using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AvaloniaChatClient.Models;
using AvaloniaChatClient.Services;

namespace AvaloniaChatClient.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly BackendApiClient _api;

    [ObservableProperty] private ObservableCollection<ChatSessionViewModel> _sessions = [];
    [ObservableProperty] private ChatSessionViewModel? _activeSession;
    [ObservableProperty] private int _selectedTabIndex;
    [ObservableProperty] private bool _backendReachable;
    [ObservableProperty] private string _backendStatus = "Verbinde…";
    [ObservableProperty] private int _errorCount;

    // F-01: track which session IDs are currently open as tabs
    public ObservableCollection<Guid> OpenSessionIds { get; } = [];

    public ServerProfilesViewModel ServerProfiles { get; }
    public HistoryViewModel History { get; }
    public ErrorLogViewModel ErrorLog { get; }

    [ObservableProperty] private ObservableCollection<ServerProfile> _availableServers = [];
    [ObservableProperty] private ServerProfile? _newSessionServer;
    [ObservableProperty] private string _newSessionModel = "llama3";
    [ObservableProperty] private string _newSessionTitle = string.Empty;
    [ObservableProperty] private bool _isNewSessionDialogOpen;

    public MainViewModel()
    {
        _api = new BackendApiClient();
        ServerProfiles = new ServerProfilesViewModel(_api);
        History = new HistoryViewModel(_api);
        History.OpenSessionIds = OpenSessionIds;  // F-01: share the same collection
        ErrorLog = new ErrorLogViewModel(_api);
        History.OpenSessionRequested += OpenExistingSession;

        // Track frontend error count for badge
        AppErrorService.Instance.Errors.CollectionChanged +=
            (_, _) => ErrorCount = AppErrorService.Instance.Errors.Count;
    }

    public async Task InitializeAsync()
    {
        try
        {
            BackendReachable = await _api.IsHealthyAsync();
            BackendStatus = BackendReachable ? "Backend verbunden ✓" : "Backend nicht erreichbar ✗";
            if (BackendReachable)
            {
                await ServerProfiles.LoadAsync();
                await History.LoadAsync();
            }
        }
        catch (Exception ex)
        {
            AppErrorService.Instance.Report("MainViewModel.Initialize", ex);
            BackendStatus = "Fehler beim Initialisieren ✗";
        }
    }

    public event Action? OpenErrorLogRequested;

    [RelayCommand]
    private void OpenErrorLog() => OpenErrorLogRequested?.Invoke();

    [RelayCommand]
    private async Task OpenNewSessionDialogAsync()
    {
        try
        {
            var servers = await _api.GetServersAsync();
            AvailableServers = new ObservableCollection<ServerProfile>(servers ?? []);
            NewSessionServer = AvailableServers.FirstOrDefault();
            NewSessionModel = NewSessionServer?.DefaultModel ?? "llama3";  // G-10 default model
            NewSessionTitle = string.Empty;
            IsNewSessionDialogOpen = true;
        }
        catch (Exception ex)
        {
            AppErrorService.Instance.Report("MainViewModel.OpenNewSessionDialog", ex);
        }
    }

    [RelayCommand]
    private async Task CreateSessionAsync()
    {
        if (NewSessionServer is null) return;
        IsNewSessionDialogOpen = false;
        try
        {
            var session = await _api.CreateSessionAsync(
                NewSessionServer.Id,
                NewSessionModel,
                string.IsNullOrWhiteSpace(NewSessionTitle) ? null : NewSessionTitle);

            if (session is null) return;
            var vm = new ChatSessionViewModel(_api, session.Id, session.Title, session.Comment);
            // G-07: set current server/model on the new VM
            vm.AvailableServers = new System.Collections.ObjectModel.ObservableCollection<ServerProfile>(AvailableServers);
            vm.SelectedServer = AvailableServers.FirstOrDefault(s => s.Id == NewSessionServer.Id);
            vm.SelectedModel = NewSessionModel;

            foreach (var s in Sessions) s.IsActive = false;   // G-02
            vm.IsActive = true;
            Sessions.Add(vm);
            OpenSessionIds.Add(vm.SessionId);
            ActiveSession = vm;
            SelectedTabIndex = 1;

            var summary = new SessionSummary(
                session.Id, session.Title, session.Comment, session.ServerId, session.ModelId,
                session.CreatedAt, session.UpdatedAt, 0);
            History.Sessions.Insert(0, new SessionSummaryViewModel(summary, _api, History));
        }
        catch (Exception ex)
        {
            AppErrorService.Instance.Report("MainViewModel.CreateSession", ex);
        }
    }

    [RelayCommand]
    private void CancelNewSession() => IsNewSessionDialogOpen = false;

    [RelayCommand]
    private void ActivateSession(ChatSessionViewModel vm)
    {
        foreach (var s in Sessions) s.IsActive = false;   // G-02
        vm.IsActive = true;
        ActiveSession = vm;
    }

    [RelayCommand]
    private void CloseSession(ChatSessionViewModel vm)
    {
        Sessions.Remove(vm);
        OpenSessionIds.Remove(vm.SessionId);  // F-01/F-06: re-enables history button
        ActiveSession = Sessions.LastOrDefault();
    }

    private void OpenExistingSession(ChatSessionViewModel vm)
    {
        foreach (var s in Sessions) s.IsActive = false;   // G-02
        vm.IsActive = true;
        Sessions.Add(vm);
        OpenSessionIds.Add(vm.SessionId);
        ActiveSession = vm;
        SelectedTabIndex = 1;
    }
}
