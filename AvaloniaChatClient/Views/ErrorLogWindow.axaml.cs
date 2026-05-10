using Avalonia.Controls;
using AvaloniaChatClient.ViewModels;

namespace AvaloniaChatClient.Views;

public partial class ErrorLogWindow : Window
{
    public ErrorLogWindow(ErrorLogViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
        Opened += async (_, _) => await vm.RefreshAsync();
    }
}
