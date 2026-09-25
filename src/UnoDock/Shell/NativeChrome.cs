using System.Runtime.InteropServices;

namespace Microsoft.Windows.Shell;
/// <summary>Native APIs are reached only on Windows and only for the owning, live HWND.</summary>
internal static class NativeChrome
{
    internal static nint Handle(Window window)
    {
        if (!OperatingSystem.IsWindows())
            return 0;
#if WINDOWS
        return WinRT.Interop.WindowNative.GetWindowHandle(window);
#else
        return Uno.UI.Xaml.WindowHelper.GetNativeWindow(window)is Uno.UI.NativeElementHosting.Win32NativeWindow native ? native.Hwnd : 0;
#endif
    }

    internal static bool TryShowSystemMenu(Window window, Point screenLocation)
    {
        var hwnd = Handle(window);
        if (hwnd == 0 || !IsWindow(hwnd))
            return false;
        var menu = GetSystemMenu(hwnd, false);
        if (menu == 0)
            return false;
        SetForegroundWindow(hwnd);
        var command = TrackPopupMenuEx(menu, 0x0100 | 0x0002, checked((int)Math.Round(screenLocation.X)), checked((int)Math.Round(screenLocation.Y)), hwnd, 0);
        if (command != 0 && !PostMessage(hwnd, 0x0112, (nuint)command, 0))
            throw new Win32Exception(Marshal.GetLastWin32Error());
        return true;
    }

    internal static bool BeginOperation(Window window, ChromeHit hit, Point screen)
    {
        var hwnd = Handle(window);
        if (hwnd == 0 || !IsWindow(hwnd))
            return false;
        // WM_NCLBUTTONDOWN packs signed 16-bit physical screen coordinates.
        var sx = Math.Round(screen.X);
        var sy = Math.Round(screen.Y);
        if (sx < short.MinValue || sx > short.MaxValue || sy < short.MinValue || sy > short.MaxValue)
            return false;
        var x = (short)sx;
        var y = (short)sy;
        ReleaseCapture();
        if (!PostMessage(hwnd, 0x00a1, (nuint)hit, (nint)((ushort)x | ((uint)(ushort)y << 16))))
            throw new Win32Exception(Marshal.GetLastWin32Error());
        return true;
    }

    [DllImport("user32.dll", ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ReleaseCapture();
    internal static void SetGlass(Window window, Thickness thickness, double scale)
    {
        var hwnd = Handle(window);
        if (hwnd == 0 || !IsWindow(hwnd))
            return;
        var margins = new Margins
        {
            Left = Pixels(thickness.Left),
            Top = Pixels(thickness.Top),
            Right = Pixels(thickness.Right),
            Bottom = Pixels(thickness.Bottom)
        };
        // Composition can be disabled by the system. No effect is claimed in that case.
        if (DwmIsCompositionEnabled(out var enabled) >= 0 && enabled)
            Marshal.ThrowExceptionForHR(DwmExtendFrameIntoClientArea(hwnd, ref margins));
        int Pixels(double value) => value < 0 ? -1 : checked((int)Math.Ceiling(value * scale));
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Margins
    {
        public int Left, Right, Top, Bottom;
    }

    [DllImport("user32.dll", ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindow(nint hwnd);
    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern nint GetSystemMenu(nint hwnd, [MarshalAs(UnmanagedType.Bool)] bool revert);
    [DllImport("user32.dll", ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(nint hwnd);
    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern uint TrackPopupMenuEx(nint menu, uint flags, int x, int y, nint hwnd, nint parameters);
    [DllImport("user32.dll", EntryPoint = "PostMessageW", ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostMessage(nint hwnd, uint message, nuint wParam, nint lParam);
    [DllImport("dwmapi.dll", ExactSpelling = true)]
    private static extern int DwmExtendFrameIntoClientArea(nint hwnd, ref Margins margins);
    [DllImport("dwmapi.dll", ExactSpelling = true)]
    private static extern int DwmIsCompositionEnabled([MarshalAs(UnmanagedType.Bool)] out bool enabled);
}
