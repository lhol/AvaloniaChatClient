using Avalonia.Controls;
using Avaiu.ViewModels;

namespace Avaiu.Views;

public partial class ErrorLogWindow : Window
{
    public ErrorLogWindow(ErrorLogViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
        Opened += async (_, _) => await vm.RefreshAsync();
    }
}
