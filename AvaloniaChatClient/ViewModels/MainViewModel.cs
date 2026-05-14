using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AvaloniaChatClient.Models;
using AvaloniaChatClient.Services;

namespace AvaloniaChatClient.ViewModels;

/// <summary>Represents a collapsible topic group in the session sidebar (supports 1 or 2 levels via slash).</summary>
public partial class SessionGroupViewModel : ObservableObject
{
    [ObservableProperty] private string _topic;
    [ObservableProperty] private bool _isExpanded = true;

    /// <summary>Direct sessions (only populated for leaf groups).</summary>
    public ObservableCollection<ChatSessionViewModel> Sessions { get; } = [];

    /// <summary>Sub-groups (only populated when this is a top-level group with sub-topics).</summary>
    public ObservableCollection<SessionGroupViewModel> SubGroups { get; } = [];

    /// <summary>True when this group contains sub-groups instead of sessions directly.</summary>
    public bool HasSubGroups => SubGroups.Count > 0;

    public SessionGroupViewModel(string topic) => _topic = topic;

    [RelayCommand]
    private void ToggleExpanded() => IsExpanded = !IsExpanded;
}

public partial class MainViewModel : ViewModelBase
{
    private readonly BackendApiClient _api;

    [ObservableProperty] private ObservableCollection<ChatSessionViewModel> _sessions = [];
    [ObservableProperty] private ChatSessionViewModel? _activeSession;
    [ObservableProperty] private int _selectedTabIndex;
    [ObservableProperty] private bool _backendReachable;
    [ObservableProperty] private string _backendStatus = "Verbinde…";
    [ObservableProperty] private int _errorCount;

    /// <summary>Grouped view of open sessions for the left sidebar tree.</summary>
    public ObservableCollection<SessionGroupViewModel> SessionGroups { get; } = [];

    /// <summary>All distinct topics currently in use (for dropdown in dialogs).</summary>
    public ObservableCollection<string> ExistingTopics { get; } = [];

    // F-01: track which session IDs are currently open as tabs
    public ObservableCollection<Guid> OpenSessionIds { get; } = [];

    public ServerProfilesViewModel ServerProfiles { get; }
    public HistoryViewModel History { get; }
    public ErrorLogViewModel ErrorLog { get; }

    [ObservableProperty] private ObservableCollection<ServerProfile> _availableServers = [];
    [ObservableProperty] private ServerProfile? _newSessionServer;
    [ObservableProperty] private string _newSessionModel = "llama3";
    [ObservableProperty] private string _newSessionTitle = string.Empty;
    [ObservableProperty] private string _newSessionComment = string.Empty;
    [ObservableProperty] private string _newSessionTopic = string.Empty;
    [ObservableProperty] private bool _isNewSessionDialogOpen;

    public MainViewModel()
    {
        _api = new BackendApiClient();
        ServerProfiles = new ServerProfilesViewModel(_api);
        History = new HistoryViewModel(_api);
        History.OpenSessionIds = OpenSessionIds;  // F-01: share the same collection
        ErrorLog = new ErrorLogViewModel(_api);
        History.OpenSessionRequested += OpenExistingSession;

        // Keep SessionGroups in sync with Sessions
        Sessions.CollectionChanged += OnSessionsChanged;

        // Track frontend error count for badge
        AppErrorService.Instance.Errors.CollectionChanged +=
            (_, _) => ErrorCount = AppErrorService.Instance.Errors.Count;
    }

    private void OnSessionsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems is not null)
            foreach (ChatSessionViewModel vm in e.NewItems)
                AddToGroup(vm);

        if (e.OldItems is not null)
            foreach (ChatSessionViewModel vm in e.OldItems)
                RemoveFromGroup(vm);
    }

    private SessionGroupViewModel GetOrCreateGroup(string topic)
    {
        var group = SessionGroups.FirstOrDefault(g => g.Topic == topic);
        if (group is null)
        {
            group = new SessionGroupViewModel(topic);
            SessionGroups.Add(group);
        }
        return group;
    }

    private void AddToGroup(ChatSessionViewModel vm)
    {
        var raw = string.IsNullOrWhiteSpace(vm.Topic) ? "Allgemein" : vm.Topic;
        var slashIdx = raw.IndexOf('/');

        if (slashIdx > 0)
        {
            // 2-level: "Parent/Child"
            var parentTopic = raw[..slashIdx].Trim();
            var childTopic  = raw[(slashIdx + 1)..].Trim();

            var parent = GetOrCreateGroup(parentTopic);
            var child  = parent.SubGroups.FirstOrDefault(g => g.Topic == childTopic);
            if (child is null)
            {
                child = new SessionGroupViewModel(childTopic);
                parent.SubGroups.Add(child);
            }
            child.Sessions.Add(vm);
        }
        else
        {
            GetOrCreateGroup(raw).Sessions.Add(vm);
        }

        // Keep ExistingTopics in sync
        if (!string.IsNullOrWhiteSpace(vm.Topic) && !ExistingTopics.Contains(vm.Topic))
            ExistingTopics.Add(vm.Topic);

        vm.AvailableTopics = ExistingTopics;

        // v0.3: activate session when edit is triggered from sidebar context menu
        vm.ActivateRequested += () => ActivateSessionAndSwitchToChat(vm);

        vm.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(ChatSessionViewModel.Topic))
            {
                RemoveFromGroup(vm);
                AddToGroup(vm);
            }
        };
    }

    private void RemoveFromGroup(ChatSessionViewModel vm)
    {
        foreach (var group in SessionGroups.ToList())
        {
            group.Sessions.Remove(vm);
            foreach (var sub in group.SubGroups.ToList())
            {
                sub.Sessions.Remove(vm);
                if (sub.Sessions.Count == 0)
                    group.SubGroups.Remove(sub);
            }
            if (group.Sessions.Count == 0 && group.SubGroups.Count == 0)
                SessionGroups.Remove(group);
        }
        RebuildExistingTopics();
    }

    private void RebuildExistingTopics()
    {
        ExistingTopics.Clear();
        foreach (var s in Sessions)
            if (!string.IsNullOrWhiteSpace(s.Topic) && !ExistingTopics.Contains(s.Topic))
                ExistingTopics.Add(s.Topic);
    }

    private void ActivateSessionAndSwitchToChat(ChatSessionViewModel vm)
    {
        foreach (var s in Sessions) s.IsActive = false;
        vm.IsActive = true;
        ActiveSession = vm;
        SelectedTabIndex = 1;
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
            NewSessionModel = NewSessionServer?.DefaultModel ?? "llama3";
            NewSessionTitle = string.Empty;
            NewSessionComment = string.Empty;
            NewSessionTopic = string.Empty;
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
                string.IsNullOrWhiteSpace(NewSessionTitle) ? null : NewSessionTitle,
                string.IsNullOrWhiteSpace(NewSessionComment) ? null : NewSessionComment,
                string.IsNullOrWhiteSpace(NewSessionTopic) ? null : NewSessionTopic);

            if (session is null) return;
            var vm = new ChatSessionViewModel(_api, session.Id, session.Title, session.Comment);
            vm.Topic = session.Topic;
            // G-07: set current server/model on the new VM — use full profile list
            vm.AvailableServers = new System.Collections.ObjectModel.ObservableCollection<ServerProfile>(ServerProfiles.Profiles);
            vm.SelectedServer = ServerProfiles.Profiles.FirstOrDefault(s => s.Id == NewSessionServer.Id)
                             ?? AvailableServers.FirstOrDefault(s => s.Id == NewSessionServer.Id);
            vm.SelectedModel = NewSessionModel;

            foreach (var s in Sessions) s.IsActive = false;   // G-02
            vm.IsActive = true;
            Sessions.Add(vm);
            OpenSessionIds.Add(vm.SessionId);
            ActiveSession = vm;
            SelectedTabIndex = 1;

            var summary = new SessionSummary(
                session.Id, session.Title, session.Comment, session.Topic, session.ServerId, session.ModelId,
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
    private void SwitchToServerTab() => SelectedTabIndex = 0;

    [RelayCommand]
    private void SwitchToHistoryTab() => SelectedTabIndex = 2;

    [RelayCommand]
    private void ActivateSession(ChatSessionViewModel vm)
    {
        ActivateSessionAndSwitchToChat(vm);
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
        // Ensure the session has the full server list from the already-loaded profiles
        if (vm.AvailableServers.Count == 0 && ServerProfiles.Profiles.Count > 0)
            vm.AvailableServers = new System.Collections.ObjectModel.ObservableCollection<ServerProfile>(ServerProfiles.Profiles);

        foreach (var s in Sessions) s.IsActive = false;   // G-02
        vm.IsActive = true;
        Sessions.Add(vm);
        OpenSessionIds.Add(vm.SessionId);
        ActiveSession = vm;
        SelectedTabIndex = 1;
    }
}
