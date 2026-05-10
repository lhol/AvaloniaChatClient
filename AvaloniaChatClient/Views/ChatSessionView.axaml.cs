using Avalonia.Controls;
using Avalonia.Input;
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
        {
            vm.ScrollToBottomRequested += ScrollToBottom;
            var inputBox = this.FindControl<TextBox>("InputBox");
            if (inputBox is not null)
            {
                inputBox.KeyDown -= OnInputKeyDown;
                inputBox.KeyDown += OnInputKeyDown;
            }
        }
    }

    // G-06: Shift+Enter sends; in singleline mode Enter also sends
    private void OnInputKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not ChatSessionViewModel vm) return;
        if (e.Key != Key.Enter) return;

        bool shiftHeld = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        bool shouldSend = vm.MultilineInput ? shiftHeld : true;

        if (shouldSend && vm.SendCommand.CanExecute(null))
        {
            vm.SendCommand.Execute(null);
            e.Handled = true;
        }
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
