using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Avaiu.Services;
using Avaiu.ViewModels;
using Avaiu.Views;

namespace Avaiu;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // Global unhandled exception guards – never let them crash the process
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            AppErrorService.Instance.Report("AppDomain.UnhandledException", (Exception)e.ExceptionObject);

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            AppErrorService.Instance.Report("TaskScheduler.UnobservedTaskException", e.Exception);
            e.SetObserved();
        };

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var vm = new MainViewModel();
            var window = new MainWindow { DataContext = vm };
            desktop.MainWindow = window;

            // Wire the error-log window opener
            vm.OpenErrorLogRequested += () =>
            {
                var w = new ErrorLogWindow(vm.ErrorLog) { ShowInTaskbar = false };
                w.ShowDialog(window);
            };

            _ = vm.InitializeAsync();
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
        {
            var vm = new MainViewModel();
            singleViewPlatform.MainView = new MainView { DataContext = vm };
            _ = vm.InitializeAsync();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
