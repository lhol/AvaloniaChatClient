using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avaiu.ViewModels;

namespace Avaiu.Views;

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
                // Use tunnel routing so we intercept Enter BEFORE TextBox processes it
                inputBox.RemoveHandler(KeyDownEvent, OnInputKeyDown);
                inputBox.AddHandler(KeyDownEvent, OnInputKeyDown, RoutingStrategies.Tunnel);
            }
        }
    }

    // G-06: Shift+Enter sends; in singleline mode Enter also sends
    private void OnInputKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not ChatSessionViewModel vm) return;
        if (e.Key != Key.Enter) return;

        bool shiftHeld = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        bool shouldSend = vm.MultilineInput ? shiftHeld : !shiftHeld || true;

        // In multiline mode: Shift+Enter sends; plain Enter inserts newline (don't intercept)
        // In singleline mode: Enter (with or without Shift) sends
        if (vm.MultilineInput && !shiftHeld) return;

        if (vm.SendCommand.CanExecute(null))
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
