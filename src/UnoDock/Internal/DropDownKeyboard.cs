using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Input;
using Windows.System;
using Windows.UI.Core;

namespace UnoDock.Internal;

internal static class DropDownKeyboard
{
    internal static bool IsContextRequest(Control owner, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Application) return true;
        if (e.Key == VirtualKey.F10 &&
            (IsDown(VirtualKey.Shift) || IsDown(VirtualKey.LeftShift) || IsDown(VirtualKey.RightShift))) return true;
#if !WINDOWS
        // The pinned Uno X11 host maps XK_Menu to VirtualKey.Menu, while actual
        // Alt keys map to LeftMenu/RightMenu. Do not extend that interpretation
        // to WinUI, Win32, browser, macOS, or another Linux host.
        if (e.Key == VirtualKey.Menu && OperatingSystem.IsLinux() && owner.XamlRoot is { } root)
        {
            foreach (var window in Uno.UI.ApplicationHelper.Windows)
                if (ReferenceEquals(window.Content?.XamlRoot, root))
                    return Uno.UI.Xaml.WindowHelper.GetNativeWindow(window) is Uno.UI.NativeElementHosting.X11NativeWindow;
        }
#endif
        return false;
    }

    private static bool IsDown(VirtualKey key) =>
        (InputKeyboardSource.GetKeyStateForCurrentThread(key) & CoreVirtualKeyStates.Down) != 0;
}
