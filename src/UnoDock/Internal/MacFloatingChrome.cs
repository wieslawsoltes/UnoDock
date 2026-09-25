#if !WINDOWS
using System.Runtime.InteropServices;

namespace UnoDock.Internal;
/// <summary>Use AppKit's full-size content contract instead of changing the host's
/// class or replacing its content view. Keeping the titled style preserves keyboard,
/// accessibility, zoom and miniaturization semantics even on older Uno native hosts.</summary>
internal sealed class MacFloatingChrome(Window window) : NativeFloatingChrome(window)
{
    private const string ObjC = "/usr/lib/libobjc.A.dylib";
    private const long FullSizeContentView = 1L << 15;
    private nint _handle;
    private long _style, _titleVisibility;
    private bool _transparent, _movable, _backgroundMovable, _changed;
    private readonly List<(nint Button, bool Hidden)> _buttons = [];
    protected override void EnableCore()
    {
        _handle = Retain(MacDesktopInterop.Handle(Window));
        var frame = ReadRect(_handle, "frame");
        _style = Integer(_handle, Sel("styleMask"));
        _titleVisibility = Integer(_handle, Sel("titleVisibility"));
        _transparent = Boolean(_handle, "titlebarAppearsTransparent");
        _movable = Boolean(_handle, "isMovable");
        _backgroundMovable = Boolean(_handle, "isMovableByWindowBackground");
        for (var i = 0; i < 3; i++)
        {
            var button = ObjectWithInteger(_handle, Sel("standardWindowButton:"), i);
            if (button != 0)
                _buttons.Add((Retain(button), Boolean(button, "isHidden")));
        }

        _changed = true;
        SetInteger(_handle, Sel("setStyleMask:"), _style | FullSizeContentView);
        SetInteger(_handle, Sel("setTitleVisibility:"), 1); // NSWindowTitleHidden
        SetBoolean(_handle, Sel("setTitlebarAppearsTransparent:"), true);
        // Only the XAML caption owns move/dock input; AppKit must not start a
        // competing background/title tracking loop over the custom controls.
        SetBoolean(_handle, Sel("setMovable:"), false);
        SetBoolean(_handle, Sel("setMovableByWindowBackground:"), false);
        foreach (var(button, _)in _buttons)
            SetBoolean(button, Sel("setHidden:"), true);
        SetFrame(_handle, Sel("setFrame:display:"), frame, true);
    }

    internal override DockRect ReadBounds()
    {
        Verify();
        var r = ReadRect(_handle, "frame");
        return new(r.Origin.X, -r.Origin.Y - r.Size.Height, r.Size.Width, r.Size.Height);
    }

    internal override void WriteBounds(DockRect bounds)
    {
        Verify();
        SetFrame(_handle, Sel("setFrame:display:"), new NativeRect { Origin = new() { X = bounds.X, Y = -bounds.Y - bounds.Height }, Size = new() { Width = bounds.Width, Height = bounds.Height } }, true);
    }

    protected override void RestoreCore()
    {
        if (!_changed || _handle == 0)
            return;
        var frame = ReadRect(_handle, "frame");
        var current = Integer(_handle, Sel("styleMask"));
        // Restore only our bit. Later resizable/fullscreen policy belongs to the host.
        if ((current & FullSizeContentView) != 0)
            SetInteger(_handle, Sel("setStyleMask:"), (current & ~FullSizeContentView) | (_style & FullSizeContentView));
        if (Integer(_handle, Sel("titleVisibility")) == 1)
            SetInteger(_handle, Sel("setTitleVisibility:"), _titleVisibility);
        if (Boolean(_handle, "titlebarAppearsTransparent"))
            SetBoolean(_handle, Sel("setTitlebarAppearsTransparent:"), _transparent);
        if (!Boolean(_handle, "isMovable"))
            SetBoolean(_handle, Sel("setMovable:"), _movable);
        if (!Boolean(_handle, "isMovableByWindowBackground"))
            SetBoolean(_handle, Sel("setMovableByWindowBackground:"), _backgroundMovable);
        foreach (var(button, hidden)in _buttons)
            if (Boolean(button, "isHidden"))
                SetBoolean(button, Sel("setHidden:"), hidden);
        SetFrame(_handle, Sel("setFrame:display:"), frame, true);
    }

    protected override void ReleaseCore()
    {
        foreach (var(button, _)in _buttons)
            ReleaseObject(button);
        _buttons.Clear();
        var handle = _handle;
        _handle = 0;
        if (handle != 0)
            ReleaseObject(handle);
    }

    private static bool Boolean(nint value, string selector) => Integer(value, Sel(selector)) != 0;
    private static NativeRect ReadRect(nint value, string selector)
    {
        if (RuntimeInformation.ProcessArchitecture == Architecture.X64)
        {
            RectStret(out var rect, value, Sel(selector));
            return rect;
        }

        return Rect(value, Sel(selector));
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        internal double X, Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeSize
    {
        internal double Width, Height;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        internal NativePoint Origin;
        internal NativeSize Size;
    }

    [DllImport(ObjC, EntryPoint = "objc_retain")]
    private static extern nint Retain(nint value);
    [DllImport(ObjC, EntryPoint = "objc_release")]
    private static extern void ReleaseObject(nint value);
    [DllImport(ObjC, EntryPoint = "sel_registerName")]
    private static extern nint Sel(string name);
    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    private static extern long Integer(nint value, nint selector);
    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    private static extern nint ObjectWithInteger(nint value, nint selector, long argument);
    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    private static extern void SetInteger(nint value, nint selector, long argument);
    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    private static extern void SetBoolean(nint value, nint selector, [MarshalAs(UnmanagedType.I1)] bool argument);
    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    private static extern NativeRect Rect(nint value, nint selector);
    [DllImport(ObjC, EntryPoint = "objc_msgSend_stret")]
    private static extern void RectStret(out NativeRect result, nint value, nint selector);
    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    private static extern void SetFrame(nint value, nint selector, NativeRect frame, [MarshalAs(UnmanagedType.I1)] bool display);
}
#endif
