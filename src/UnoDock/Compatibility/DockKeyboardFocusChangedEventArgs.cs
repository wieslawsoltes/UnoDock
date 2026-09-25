using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Input;

namespace UnoDock.Compatibility;
/// <summary>Pre/post focus observation; cancellation is available only during native GettingFocus.</summary>
public sealed class DockKeyboardFocusChangedEventArgs : EventArgs
{
    internal DockKeyboardFocusChangedEventArgs(RoutedEventArgs nativeEvent, DependencyObject? oldFocus, DependencyObject? newFocus)
    {
        NativeEvent = nativeEvent;
        OldFocus = oldFocus;
        NewFocus = newFocus;
    }

    public RoutedEventArgs NativeEvent
    {
        get;
    }
    public object OriginalSource => NativeEvent.OriginalSource;
    public DependencyObject? OldFocus
    {
        get;
    }
    public DependencyObject? NewFocus
    {
        get;
    }
    public bool Handled
    {
        get; set;
    }
    public bool CanCancel => NativeEvent is GettingFocusEventArgs;

    public bool Cancel
    {
        get => NativeEvent is GettingFocusEventArgs e && e.Cancel;
        set
        {
            if (NativeEvent is not GettingFocusEventArgs e)
            {
                if (value)
                    throw new InvalidOperationException("A completed focus transition cannot be cancelled.");
                return;
            }

            e.Cancel = value;
        }
    }

    internal void Complete()
    {
        if (Handled && NativeEvent is GettingFocusEventArgs e)
            e.Handled = true;
    }
}
