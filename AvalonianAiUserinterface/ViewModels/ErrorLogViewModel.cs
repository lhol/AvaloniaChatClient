using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Avaiu.Models;
using Avaiu.Services;

namespace Avaiu.ViewModels;

public partial class ErrorLogViewModel : ViewModelBase
{
    private readonly BackendApiClient _api;

    [ObservableProperty] private System.Collections.ObjectModel.ObservableCollection<AppError> _frontendErrors;
    [ObservableProperty] private List<ErrorLogEntry> _backendErrors = [];
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private AppError? _selectedFrontendError;
    [ObservableProperty] private ErrorLogEntry? _selectedBackendError;

    public ErrorLogViewModel(BackendApiClient api)
    {
        _api = api;
        _frontendErrors = AppErrorService.Instance.Errors;
    }

    [RelayCommand]
    public async Task RefreshAsync()
    {
        IsLoading = true;
        try
        {
            var entries = await _api.GetErrorsAsync();
            BackendErrors = entries ?? [];
        }
        catch (Exception ex)
        {
            AppErrorService.Instance.Report("ErrorLog.Refresh", ex);
        }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private async Task ClearBackendAsync()
    {
        try
        {
            await _api.ClearErrorsAsync();
            BackendErrors = [];
        }
        catch (Exception ex)
        {
            AppErrorService.Instance.Report("ErrorLog.ClearBackend", ex);
        }
    }

    [RelayCommand]
    private void ClearFrontend() => AppErrorService.Instance.Clear();

    public string? SelectedFrontendDetails =>
        SelectedFrontendError is not null
            ? $"[{SelectedFrontendError.Timestamp:HH:mm:ss}] {SelectedFrontendError.Context}\n\n{SelectedFrontendError.UserMessage}\n\n{SelectedFrontendError.FullDetails}"
            : null;

    public string? SelectedBackendDetails =>
        SelectedBackendError is not null
            ? $"[{SelectedBackendError.Timestamp:HH:mm:ss}] {SelectedBackendError.Context}\n\n{SelectedBackendError.UserMessage}\n\n{SelectedBackendError.FullDetails}"
            : null;

    partial void OnSelectedFrontendErrorChanged(AppError? value) => OnPropertyChanged(nameof(SelectedFrontendDetails));
    partial void OnSelectedBackendErrorChanged(ErrorLogEntry? value) => OnPropertyChanged(nameof(SelectedBackendDetails));
}
