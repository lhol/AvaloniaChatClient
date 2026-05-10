using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AvaloniaChatClient.Models;
using AvaloniaChatClient.Services;

namespace AvaloniaChatClient.ViewModels;

public partial class ServerProfilesViewModel : ViewModelBase
{
    private readonly BackendApiClient _api;

    [ObservableProperty] private ObservableCollection<ServerProfile> _profiles = [];
    [ObservableProperty] private ServerProfile? _selectedProfile;
    [ObservableProperty] private bool _isEditing;
    [ObservableProperty] private string _editName = string.Empty;
    [ObservableProperty] private string _editUrl = "http://localhost";
    [ObservableProperty] private int _editPort = 11434;
    [ObservableProperty] private string _editToken = string.Empty;
    [ObservableProperty] private LlmProtocol _editProtocol = LlmProtocol.OpenAI;
    [ObservableProperty] private string _testStatus = string.Empty;
    [ObservableProperty] private bool _isTesting;
    [ObservableProperty] private bool _isLoading;

    public LlmProtocol[] Protocols { get; } = Enum.GetValues<LlmProtocol>();

    public ServerProfilesViewModel(BackendApiClient api)
    {
        _api = api;
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            var list = await _api.GetServersAsync();
            Profiles = new ObservableCollection<ServerProfile>(list ?? []);
        }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private void StartCreate()
    {
        SelectedProfile = null;
        EditName = string.Empty;
        EditUrl = "http://localhost";
        EditPort = 11434;
        EditToken = string.Empty;
        EditProtocol = LlmProtocol.OpenAI;
        TestStatus = string.Empty;
        IsEditing = true;
    }

    [RelayCommand]
    private void StartEdit(ServerProfile profile)
    {
        SelectedProfile = profile;
        EditName = profile.Name;
        EditUrl = profile.Url;
        EditPort = profile.Port;
        EditToken = profile.Token ?? string.Empty;
        EditProtocol = profile.Protocol;
        TestStatus = string.Empty;
        IsEditing = true;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        var token = string.IsNullOrWhiteSpace(EditToken) ? null : EditToken;
        if (SelectedProfile is null)
        {
            var created = await _api.CreateServerAsync(EditName, EditUrl, EditPort, token, EditProtocol);
            if (created is not null) Profiles.Add(created);
        }
        else
        {
            var updated = await _api.UpdateServerAsync(SelectedProfile.Id, EditName, EditUrl, EditPort, token, EditProtocol);
            if (updated is not null)
            {
                var idx = Profiles.IndexOf(SelectedProfile);
                if (idx >= 0) Profiles[idx] = updated;
            }
        }
        IsEditing = false;
    }

    [RelayCommand]
    private async Task DeleteAsync(ServerProfile profile)
    {
        if (await _api.DeleteServerAsync(profile.Id))
            Profiles.Remove(profile);
    }

    [RelayCommand]
    private async Task TestConnectionAsync(ServerProfile profile)
    {
        IsTesting = true;
        TestStatus = "Teste…";
        try
        {
            var result = await _api.TestServerAsync(profile.Id);
            TestStatus = result is null
                ? "Fehler: keine Antwort"
                : result.Success
                    ? $"✓ Verbunden ({result.LatencyMs} ms)"
                    : $"✗ Fehler: {result.Error}";
        }
        finally { IsTesting = false; }
    }

    [RelayCommand]
    private void CancelEdit() => IsEditing = false;
}
