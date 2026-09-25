using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Input;

namespace UnoDock.Compatibility;

public sealed class DockMouseButtonEventArgs : DockMouseEventArgs
{
    internal DockMouseButtonEventArgs(PointerRoutedEventArgs nativeEvent, UIElement owner, DockMouseButton button, bool pressed) : base(nativeEvent, owner)
    {
        ChangedButton = button;
        ButtonState = pressed ? DockMouseButtonState.Pressed : DockMouseButtonState.Released;
    }

    public DockMouseButton ChangedButton
    {
        get;
    }
    public DockMouseButtonState ButtonState
    {
        get;
    }
}
