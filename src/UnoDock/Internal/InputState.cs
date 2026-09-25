using Microsoft.UI.Xaml.Input;

namespace UnoDock.Internal;
internal static class InputState
{
    public static bool ControlDown => Down(Windows.System.VirtualKey.Control);
    public static bool ShiftDown => Down(Windows.System.VirtualKey.Shift);

    private static bool Down(Windows.System.VirtualKey key) => (Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(key) & Windows.UI.Core.CoreVirtualKeyStates.Down) != 0;
}
