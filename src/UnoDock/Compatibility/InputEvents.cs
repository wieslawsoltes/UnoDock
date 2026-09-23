using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Input;

namespace UnoDock.Compatibility;

public enum DockMouseButton { Left, Middle, Right, XButton1, XButton2 }
public enum DockMouseButtonState { Released, Pressed }

/// <summary>
/// A control-local compatibility input stage over a real Uno pointer event. Handled
/// suppresses subsequent docking stages, without unhandling an already-handled native event.
/// Coordinates and pointer identity always come from the original native event.
/// </summary>
public class DockMouseEventArgs : EventArgs
{
    internal DockMouseEventArgs(PointerRoutedEventArgs nativeEvent, UIElement owner)
    { NativeEvent = nativeEvent; Owner = owner; }
    internal UIElement Owner { get; }
    public PointerRoutedEventArgs NativeEvent { get; }
    public object OriginalSource => NativeEvent.OriginalSource;
    public bool Handled { get; set; }
    public uint PointerId => NativeEvent.Pointer.PointerId;
    public PointerDeviceType PointerDeviceType => NativeEvent.Pointer.PointerDeviceType;
    public Point GetPosition(UIElement? relativeTo) => NativeEvent.GetCurrentPoint(relativeTo).Position;
    public DockMouseButtonState LeftButton => State(NativeEvent.GetCurrentPoint(Owner).Properties.IsLeftButtonPressed);
    public DockMouseButtonState RightButton => State(NativeEvent.GetCurrentPoint(Owner).Properties.IsRightButtonPressed);
    public DockMouseButtonState MiddleButton => State(NativeEvent.GetCurrentPoint(Owner).Properties.IsMiddleButtonPressed);
    private static DockMouseButtonState State(bool pressed) => pressed ? DockMouseButtonState.Pressed : DockMouseButtonState.Released;
    internal void Complete() { if (Handled) NativeEvent.Handled = true; }
}

public sealed class DockMouseButtonEventArgs : DockMouseEventArgs
{
    internal DockMouseButtonEventArgs(PointerRoutedEventArgs nativeEvent, UIElement owner, DockMouseButton button, bool pressed)
        : base(nativeEvent, owner) { ChangedButton = button; ButtonState = pressed ? DockMouseButtonState.Pressed : DockMouseButtonState.Released; }
    public DockMouseButton ChangedButton { get; }
    public DockMouseButtonState ButtonState { get; }
}

/// <summary>Pre/post focus observation; cancellation is available only during native GettingFocus.</summary>
public sealed class DockKeyboardFocusChangedEventArgs : EventArgs
{
    internal DockKeyboardFocusChangedEventArgs(RoutedEventArgs nativeEvent, DependencyObject? oldFocus, DependencyObject? newFocus)
    { NativeEvent = nativeEvent; OldFocus = oldFocus; NewFocus = newFocus; }
    public RoutedEventArgs NativeEvent { get; }
    public object OriginalSource => NativeEvent.OriginalSource;
    public DependencyObject? OldFocus { get; }
    public DependencyObject? NewFocus { get; }
    public bool Handled { get; set; }
    public bool CanCancel => NativeEvent is GettingFocusEventArgs;
    public bool Cancel
    {
        get => NativeEvent is GettingFocusEventArgs e && e.Cancel;
        set
        {
            if (NativeEvent is not GettingFocusEventArgs e)
            { if (value) throw new InvalidOperationException("A completed focus transition cannot be cancelled."); return; }
            e.Cancel = value;
        }
    }
    internal void Complete() { if (Handled && NativeEvent is GettingFocusEventArgs e) e.Handled = true; }
}
