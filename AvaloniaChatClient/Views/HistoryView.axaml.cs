using Avalonia.Controls;
using AvaloniaChatClient.ViewModels;

namespace AvaloniaChatClient.Views;

public partial class HistoryView : UserControl
{
    public HistoryView()
    {
        InitializeComponent();
    }

    protected override void OnAttachedToVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (DataContext is HistoryViewModel vm)
            vm.TopLevel = TopLevel.GetTopLevel(this);
    }
}
