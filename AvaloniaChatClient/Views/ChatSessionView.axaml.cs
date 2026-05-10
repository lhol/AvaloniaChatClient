using Avalonia.Controls;
using Avalonia.Threading;
using AvaloniaChatClient.ViewModels;

namespace AvaloniaChatClient.Views;

public partial class ChatSessionView : UserControl
{
    public ChatSessionView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => AttachViewModel();
    }

    private void AttachViewModel()
    {
        if (DataContext is ChatSessionViewModel vm)
            vm.ScrollToBottomRequested += ScrollToBottom;
    }

    private void ScrollToBottom()
    {
        Dispatcher.UIThread.Post(() =>
        {
            var scroller = this.FindControl<ScrollViewer>("MessageScroller");
            scroller?.ScrollToEnd();
        }, DispatcherPriority.Background);
    }
}
