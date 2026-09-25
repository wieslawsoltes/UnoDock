using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Input;

namespace UnoDock.Compatibility;
/// <summary>
/// A control-local compatibility input stage over a real Uno pointer event. Handled
/// suppresses subsequent docking stages, without unhandling an already-handled native event.
/// Coordinates and pointer identity always come from the original native event.
/// </summary>
public class DockMouseEventArgs : EventArgs
{
    internal DockMouseEventArgs(PointerRoutedEventArgs nativeEvent, UIElement owner)
    {
        NativeEvent = nativeEvent;
        Owner = owner;
    }

    internal UIElement Owner
    {
        get;
    }
    public PointerRoutedEventArgs NativeEvent
    {
        get;
    }
    public object OriginalSource => NativeEvent.OriginalSource;
    public bool Handled
    {
        get;
        set;
    }
    public uint PointerId => NativeEvent.Pointer.PointerId;
    public PointerDeviceType PointerDeviceType => NativeEvent.Pointer.PointerDeviceType;

    public Point GetPosition(UIElement? relativeTo) => NativeEvent.GetCurrentPoint(relativeTo).Position;
    public DockMouseButtonState LeftButton => State(NativeEvent.GetCurrentPoint(Owner).Properties.IsLeftButtonPressed);
    public DockMouseButtonState RightButton => State(NativeEvent.GetCurrentPoint(Owner).Properties.IsRightButtonPressed);
    public DockMouseButtonState MiddleButton => State(NativeEvent.GetCurrentPoint(Owner).Properties.IsMiddleButtonPressed);

    private static DockMouseButtonState State(bool pressed) => pressed ? DockMouseButtonState.Pressed : DockMouseButtonState.Released;
    internal void Complete()
    {
        if (Handled)
            NativeEvent.Handled = true;
    }
}
