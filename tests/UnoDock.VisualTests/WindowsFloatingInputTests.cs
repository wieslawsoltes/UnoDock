using System.ComponentModel;
using System.Runtime.InteropServices;
using UnoDock.Controls;
using UnoDock.Layout;
using UnoDock.Themes;
using Windows.Foundation;

namespace UnoDock.Testing;

/// <summary>Opt-in SendInput acceptance on a dedicated Windows desktop. No model
/// operation, private drag entry point or synthetic routed event replaces input.</summary>
internal static class WindowsFloatingInputTests
{
    internal static async Task<int> Run(string output)
    {
        if (!OperatingSystem.IsWindows() || Environment.GetEnvironmentVariable("UNODOCK_NATIVE_INPUT_TESTS") != "1") return 0;
        var tests = new TestRunner();
        foreach (var tools in new[] { false, true })
        {
            foreach (var outcome in new[] { "dock", "cancel", "suppress" })
                tests.Test($"SendInput: {(tools ? "tool group" : "document")} client caption {outcome}", async () =>
                {
                    using var f = new Fixture(tools); await f.Show(); using var input = new NativeInput();
                    var handle = f.Control.FindVisualChildren<Border>().Single(b => b.Name == "PART_FloatingDragHandle");
                    var window = f.Control.NativeWindow!; var origin = window.AppWindow.Position;
                    var editors = f.Source.Select(c => c.Content).ToArray();
                    input.Focus(window);
                    input.MoveTo(handle, new(Math.Min(100, handle.ActualWidth / 2), handle.ActualHeight / 2));
                    await Task.Delay(60); input.Press(); await Task.Delay(60);
                    if (outcome == "suppress") input.KeyDown(0x11);
                    input.MoveTo(f.Target, new(f.Target.ActualWidth / 2, f.Target.ActualHeight / 2));
                    await Wait(() => f.Control.IsDragging && (window.AppWindow.Position.X != origin.X || window.AppWindow.Position.Y != origin.Y));
                    if (outcome == "cancel")
                    {
                        input.KeyDown(0x1b); await Wait(() => !f.Control.IsDragging); input.KeyUp(0x1b); input.Release();
                        await Task.Delay(80); Check.Equal(origin.X, window.AppWindow.Position.X); Check.Equal(origin.Y, window.AppWindow.Position.Y);
                    }
                    else { input.Release(); await Wait(() => !f.Control.IsDragging); }
                    if (outcome == "suppress") input.KeyUp(0x11);
                    if (outcome == "dock") await Wait(() => f.Source.All(c => ReferenceEquals(c.Parent, f.Documents)));
                    else Check.True(f.Source.All(c => ReferenceEquals(c.FindParent<LayoutFloatingWindow>(), f.Floating)));
                    for (var i = 0; i < editors.Length; i++) Check.Same(editors[i], f.Source[i].Content);
                    Check.Equal(0, f.Failures.Count);
                });
            tests.Test($"SendInput: {(tools ? "tool group" : "document")} actual OS title bar docks after WM_EXITSIZEMOVE", async () =>
            {
                using var f = new Fixture(tools); await f.Show(); using var input = new NativeInput();
                var native = f.Control.NativeWindow!; input.Focus(native);
                var start = input.CaptionCenter(native);
                var finish = input.ScreenPoint(f.Target, new(f.Target.ActualWidth / 2, f.Target.ActualHeight / 2));
                var started = 0;
                var token = f.Control.RegisterPropertyChangedCallback(LayoutFloatingWindowControl.IsDraggingProperty, (_, _) => { if (f.Control.IsDragging) started++; });
                try
                {
                    // Native title dragging can enter an OS modal loop. The driver
                    // uses its own thread; the application still receives real input.
                    await Task.Run(async () =>
                    {
                        input.Move(start); await Task.Delay(80); input.Press(); await Task.Delay(100);
                        input.Move(new(start.X + 24, start.Y + 8)); await Task.Delay(100);
                        input.Move(finish); await Task.Delay(200); input.Release();
                    });
                    await Wait(() => f.Source.All(c => ReferenceEquals(c.Parent, f.Documents)));
                    Check.True(started > 0, "The actual native move did not enter the library drag lifecycle.");
                    Check.False(f.Control.IsDragging); Check.Equal(0, f.Failures.Count);
                }
                finally { f.Control.UnregisterPropertyChangedCallback(LayoutFloatingWindowControl.IsDraggingProperty, token); }
            });
        }
        return await tests.Run(output, "windows-floating-input");
    }
    private sealed class Fixture : IDisposable
    {
        internal readonly DockingManager Manager = new() { Width = 1000, Height = 620, FloatingWindowMode = FloatingWindowMode.Native, Theme = new FluentTheme(ElementTheme.Light) };
        internal readonly LayoutDocumentPane Documents = new(new LayoutDocument { Title = "Destination", ContentId = "destination", Content = new TextBox { Text = "Destination editor" } });
        internal readonly LayoutFloatingWindow Floating;
        internal readonly LayoutContent[] Source;
        internal readonly List<Exception> Failures = [];
        internal LayoutFloatingWindowControl Control = null!;
        internal FrameworkElement Target = null!;
        private readonly Window _window;
        private readonly IDisposable _registration;
        internal Fixture(bool tools)
        {
            Manager.Layout = new() { RootPanel = new LayoutPanel(Documents) };
            if (tools)
            {
                Source = Enumerable.Range(0, 3).Select(i => (LayoutContent)new LayoutAnchorable
                {
                    Title = "Floating tool " + i, ContentId = "tool" + i, CanDockAsTabbedDocument = true,
                    Content = new TextBox { Text = "Retained draft " + i }, FloatingLeft = 150, FloatingTop = 140, FloatingWidth = 480, FloatingHeight = 320
                }).ToArray();
                var first = new LayoutAnchorablePane((LayoutAnchorable)Source[0]); first.Children.Add((LayoutAnchorable)Source[1]);
                var group = new LayoutAnchorablePaneGroup { Orientation = Orientation.Vertical };
                group.Children.Add(first); group.Children.Add(new LayoutAnchorablePane((LayoutAnchorable)Source[2]));
                Floating = new LayoutAnchorableFloatingWindow { RootPanel = group };
            }
            else
            {
                var document = new LayoutDocument { Title = "Floating document", ContentId = "floating", Content = new TextBox { Text = "Retained draft" },
                    FloatingLeft = 150, FloatingTop = 140, FloatingWidth = 480, FloatingHeight = 320 };
                Source = [document]; Floating = new LayoutDocumentFloatingWindow { RootDocument = document };
            }
            Manager.Layout.FloatingWindows.Add(Floating); Source[0].IsActive = true;
            _window = new() { Content = Manager, Title = "UnoDock dedicated native input acceptance" };
            _registration = Microsoft.Windows.Shell.SystemCommands.RegisterWindow(_window);
            _window.AppWindow.Move(new() { X = 20, Y = 20 }); _window.AppWindow.Resize(new() { Width = 1050, Height = 700 }); _window.Activate();
        }
        internal async Task Show()
        {
            await Wait(() => Manager.IsLoaded && Manager.ActualWidth > 0); Manager.Refresh();
            await Wait(() => Manager.FloatingWindows.Count() == 1);
            Control = Manager.FloatingWindows.Single(); Control.MessageFilterFailed += (_, e) => Failures.Add(e);
            await Wait(() => Control.IsLoaded && Control.ActualWidth > 0 && Control.NativeWindow != null);
            Target = Manager.FindVisualChildren<LayoutDocumentPaneControl>().Single(c => ReferenceEquals(c.Model, Documents));
            await Wait(() => Target.ActualWidth > 0 && Target.ActualHeight > 0); await Task.Delay(100);
        }
        public void Dispose()
        {
            try { Manager.Dispose(); }
            finally { _window.Content = null; _window.Close(); _registration.Dispose(); }
        }
    }
    private sealed class NativeInput : IDisposable
    {
        private bool _pressed;
        private readonly HashSet<ushort> _keys = [];
        private readonly POINT _initial;
        internal NativeInput()
        {
            if (!OperatingSystem.IsWindows() || Environment.GetEnvironmentVariable("UNODOCK_NATIVE_INPUT_TESTS") != "1") throw new InvalidOperationException("Dedicated Windows input must be explicitly enabled.");
            if (!GetCursorPos(out _initial)) throw new Win32Exception(Marshal.GetLastPInvokeError());
        }
        internal void Focus(Window window)
        {
            window.Activate(); var handle = Handle(window); SetForegroundWindow(handle);
            Check.Equal(handle, GetForegroundWindow());
        }
        internal Point ScreenPoint(FrameworkElement element, Point point)
        {
            var root = element.XamlRoot ?? throw new InvalidOperationException("Detached test target.");
            var window = Uno.UI.ApplicationHelper.Windows.Single(w => ReferenceEquals(w.Content?.XamlRoot, root));
            var origin = new POINT();
            if (!ClientToScreen(Handle(window), ref origin)) throw new Win32Exception(Marshal.GetLastPInvokeError());
            var local = element.TransformToVisual(null).TransformPoint(point);
            return new(origin.X + local.X * root.RasterizationScale, origin.Y + local.Y * root.RasterizationScale);
        }
        internal Point CaptionCenter(Window window)
        {
            var handle = Handle(window); var origin = new POINT();
            Check.True(GetWindowRect(handle, out var frame) && ClientToScreen(handle, ref origin));
            Check.True(origin.Y > frame.Top + 4, "No real native title-bar region was available.");
            return new((frame.Left + frame.Right) / 2d, (frame.Top + origin.Y) / 2d);
        }
        internal void MoveTo(FrameworkElement element, Point point) => Move(ScreenPoint(element, point));
        internal void Move(Point point)
        { if (!SetCursorPos(checked((int)Math.Round(point.X)), checked((int)Math.Round(point.Y)))) throw new Win32Exception(Marshal.GetLastPInvokeError()); }
        internal void Press() { Send(new() { Type = 0, Data = new() { Mouse = new() { Flags = 2 } } }); _pressed = true; }
        internal void Release() { if (!_pressed) return; Send(new() { Type = 0, Data = new() { Mouse = new() { Flags = 4 } } }); _pressed = false; }
        internal void KeyDown(ushort key) { Send(new() { Type = 1, Data = new() { Keyboard = new() { Key = key } } }); _keys.Add(key); }
        internal void KeyUp(ushort key) { if (!_keys.Remove(key)) return; Send(new() { Type = 1, Data = new() { Keyboard = new() { Key = key, Flags = 2 } } }); }
        private static void Send(INPUT input)
        { if (SendInput(1, [input], Marshal.SizeOf<INPUT>()) != 1) throw new Win32Exception(Marshal.GetLastPInvokeError(), "Dedicated SendInput injection failed."); }
        private static nint Handle(Window window) => Uno.UI.Xaml.WindowHelper.GetNativeWindow(window) is Uno.UI.NativeElementHosting.Win32NativeWindow native
            ? native.Hwnd : throw new InvalidOperationException("A real Win32 Uno host is required.");
        public void Dispose()
        {
            try { Release(); }
            finally { foreach (var key in _keys.ToArray()) KeyUp(key); SetCursorPos(_initial.X, _initial.Y); }
        }
        [StructLayout(LayoutKind.Sequential)] private struct POINT { internal int X, Y; }
        [StructLayout(LayoutKind.Sequential)] private struct RECT { internal int Left, Top, Right, Bottom; }
        [StructLayout(LayoutKind.Sequential)] private struct INPUT { internal uint Type; internal UNION Data; }
        [StructLayout(LayoutKind.Explicit)] private struct UNION
        { [FieldOffset(0)] internal MOUSE Mouse; [FieldOffset(0)] internal KEYBOARD Keyboard; }
        [StructLayout(LayoutKind.Sequential)] private struct MOUSE
        { internal int X, Y; internal uint Data, Flags, Time; internal nuint Extra; }
        [StructLayout(LayoutKind.Sequential)] private struct KEYBOARD
        { internal ushort Key, Scan; internal uint Flags, Time; internal nuint Extra; }
        [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetCursorPos(int x, int y);
        [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetCursorPos(out POINT point);
        [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool ClientToScreen(nint window, ref POINT point);
        [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetWindowRect(nint window, out RECT rect);
        [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetForegroundWindow(nint window);
        [DllImport("user32.dll")] private static extern nint GetForegroundWindow();
        [DllImport("user32.dll", SetLastError = true)] private static extern uint SendInput(uint count, [In] INPUT[] inputs, int size);
    }
    private static async Task Wait(Func<bool> ready)
    { for (var i = 0; i < 120 && !ready(); i++) await Task.Delay(25); Check.True(ready(), "The native input transition did not settle."); }
}
