using System.ComponentModel;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Xml.Linq;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media;
using Microsoft.Windows.Shell;
using UnoDock;
using UnoDock.Controls;
using UnoDock.Layout;
using UnoDock.Layout.Serialization;
using UnoDock.Core;

namespace UnoDock.Testing;

/// <summary>Window/persistence and navigator contracts exercised in an actual Uno UI thread.</summary>
public static class WindowLifecycleTests
{
    public static async Task<int> Run(DockingManager host, string output)
    {
        var tests = new TestRunner();
        tests.Test("window initialization waits for derived model and runs once", () =>
        {
            using var manager = Floating(out var model); var window = new Probe(model);
            Check.Equal(0, window.Initializations); Check.True(window.Model != null);
            var events = 0; window.Initialized += (_, _) => { events++; window.Initialize(); };
            window.Initialize(); window.Initialize(); Check.Equal(1, window.Initializations); Check.Equal(1, events);
            Call(window, "CloseHost"); window.Initialize(); Check.Equal(1, events);
        });
        tests.Test("closing callback can veto without losing window or content", () =>
        {
            using var manager = Floating(out var model); var window = new Probe(model) { Veto = true };
            var document = model.RootDocument; window.Close();
            Check.Same(document, model.RootDocument); Check.True(model.Root != null); Check.Equal(0, window.Closures);
            window.Veto = false; window.Close(); Check.Equal(1, window.Closures);
        });
        tests.Test("public Closing event participates in cancellation", () =>
        {
            using var manager = Floating(out var model); var window = new Probe(model); var count = 0;
            window.Closing += (_, e) => { e.Cancel = true; count++; }; window.Close();
            Check.Equal(1, count); Check.True(model.Root != null); Check.Equal(0, window.Closures); Call(window, "CloseHost");
        });
        tests.Test("recursive close dispatches one closing and closed pair", () =>
        {
            using var manager = Floating(out var model); var window = new Probe(model); var closing = 0; var closed = 0;
            window.Closing += (_, _) => { closing++; window.Close(); };
            window.Closed += (_, _) => { closed++; window.Close(); Call(window, "CloseHost"); };
            window.Close(); Check.Equal(1, closing); Check.Equal(1, closed); Check.True(window.UserCloseObserved); Check.False(window.UserCloseNow);
        });
        tests.Test("model close veto does not emit host Closed", () =>
        {
            using var manager = Floating(out var model); var window = new Probe(model);
            model.RootDocument!.Closing += (_, e) => e.Cancel = true;
            window.Close(); Check.Equal(0, window.Closures); Check.True(model.RootDocument != null); Call(window, "CloseHost");
        });
        tests.Test("closing exception resets reentrancy and user-close flag", () =>
        {
            using var manager = Floating(out var model); var window = new Probe(model);
            EventHandler<CancelEventArgs> handler = (_, _) => throw new InvalidOperationException("observer");
            window.Closing += handler; Check.Throws<InvalidOperationException>(window.Close); Check.False(window.UserCloseNow);
            window.Closing -= handler; window.Close(); Check.Equal(1, window.Closures);
        });
        tests.Test("closing callback root replacement does not close replacement content", () =>
        {
            using var manager = Floating(out var model); var window = new Probe(model);
            var replacement = new LayoutDocument(); var next = Root(new LayoutDocumentPane(replacement));
            window.Closing += (_, _) => manager.Layout = next; window.Close();
            Check.Same(next, replacement.Root); Check.True(model.RootDocument != null); Call(window, "CloseHost");
        });
        tests.Test("permanently closed floating host cannot execute system commands", () =>
        {
            using var manager = Floating(out var model); var window = new Probe(model); Call(window, "CloseHost");
            window.Activate(); window.Close(); window.Hide(); Call(window, "ShowNative");
            Check.Equal(1, window.Closures); Check.True(window.NativeWindow == null);
            Check.False(SystemCommands.MaximizeWindowCommand.CanExecute(window)); Check.False(SystemCommands.CloseWindowCommand.CanExecute(window));
        });
        tests.Test("message filter defaults to unhandled without requiring Windows", () =>
        {
            using var manager = Floating(out var model); var window = new Probe(model); var handled = false;
            Check.Equal((nint)0, window.InvokeBaseFilter(ref handled)); Check.False(handled); Call(window, "CloseHost");
        });
        tests.Test("tool hide stops after a reentrant root replacement", () =>
        {
            using var manager = new DockingManager(); var a = new LayoutAnchorable(); var b = new LayoutAnchorable();
            var pane = new LayoutAnchorablePane(a); pane.Children.Add(b);
            var model = new LayoutAnchorableFloatingWindow { RootPanel = new LayoutAnchorablePaneGroup(pane) };
            manager.Layout.FloatingWindows.Add(model); var window = new LayoutAnchorableFloatingWindowControl(model);
            a.Hiding += (_, _) => manager.Layout = new(); window.Hide(); Check.Same(pane, b.Parent); Call(window, "CloseHost");
        });
        tests.Test("tool host clears retained single-item adapter on closure", () =>
        {
            using var manager = new DockingManager(); var tool = new LayoutAnchorable();
            var model = new LayoutAnchorableFloatingWindow { RootPanel = new LayoutAnchorablePaneGroup(new LayoutAnchorablePane(tool)) };
            manager.Layout.FloatingWindows.Add(model); var window = new LayoutAnchorableFloatingWindowControl(model);
            window.SingleContentLayoutItem = manager.GetLayoutItemFromModel(tool); window.Close();
            Check.True(window.SingleContentLayoutItem == null); Check.False(window.CloseWindowCommand.CanExecute(null)); Check.False(window.HideWindowCommand.CanExecute(null));
        });
        tests.Test("overlay public closing cancellation keeps preview open", () =>
        {
            var overlay = new OverlayWindow { Visibility = Visibility.Visible };
            EventHandler<CancelEventArgs> veto = (_, e) => e.Cancel = true;
            overlay.Closing += veto; overlay.Close(); Check.Equal(Visibility.Visible, overlay.Visibility);
            overlay.Closing -= veto; overlay.Close(); Check.Equal(Visibility.Collapsed, overlay.Visibility);
        });

        void Live(string name, Func<Fixture, Task> body) => tests.Test(name, async () =>
        {
            using var fixture = new Fixture(host);
            try { await body(fixture); } finally { CallSurface(host, "CloseNavigator", false); }
        });
        Live("maximized presentation leaves all normal model bounds intact", async f =>
        {
            var window = await f.Float(); var normal = Bounds(f.A);
            SystemCommands.MaximizeWindow(window); host.Refresh(); await Tick();
            Check.True(window.IsMaximized); Check.Equal(normal, Bounds(f.A)); Check.True(window.Width > normal.Width);
            SystemCommands.RestoreWindow(window); Check.Equal(normal, Bounds(f.A)); Check.Near(normal.Width, window.Width);
        });
        Live("maximized XML persists normal bounds plus maximized flag", async f =>
        {
            var window = await f.Float(); var normal = Bounds(f.A); SystemCommands.MaximizeWindow(window);
            using var text = new StringWriter(); new XmlLayoutSerializer(host).Serialize(text);
            var node = XDocument.Parse(text.ToString()).Descendants("LayoutDocument").Single(n => (string?)n.Attribute("ContentId") == "a");
            Check.True((bool)node.Attribute("IsMaximized")!); Check.Near(normal.Width, (double)node.Attribute("FloatingWidth")!);
            Check.Near(normal.X, (double)node.Attribute("FloatingLeft")!);
        });
        Live("imported maximized host restores original normal rectangle", async f =>
        {
            var window = await f.Float(); var normal = Bounds(f.A); SystemCommands.MaximizeWindow(window);
            using var text = new StringWriter(); var serializer = new XmlLayoutSerializer(host); serializer.Serialize(text); serializer.Deserialize(new StringReader(text.ToString()));
            host.Refresh(); await Tick(); window = host.FloatingWindows.Single(); Check.True(window.IsMaximized);
            var model = host.Layout.Descendents().OfType<LayoutDocument>().Single(c => c.ContentId == "a");
            Check.Equal(normal, Bounds(model)); SystemCommands.RestoreWindow(window); Check.Near(normal.Width, window.Width); Check.Equal(normal, Bounds(model));
        });
        Live("minimized managed window stays minimized after manager refresh", async f =>
        {
            var window = await f.Float(); SystemCommands.MinimizeWindow(window); host.Refresh(); await Tick();
            Check.Equal(Visibility.Collapsed, window.Visibility); window.Activate(); Check.Equal(Visibility.Visible, window.Visibility);
        });
        Live("maximize minimize restore retains original geometry", async f =>
        {
            var window = await f.Float(); var original = Bounds(f.A);
            SystemCommands.MaximizeWindow(window); SystemCommands.MinimizeWindow(window); host.Refresh(); await Tick();
            Check.True(window.IsMaximized); Check.Equal(Visibility.Collapsed, window.Visibility); Check.Equal(original, Bounds(f.A));
            SystemCommands.RestoreWindow(window); Check.False(window.IsMaximized); Check.Equal(original, Bounds(f.A));
        });
        Live("state hook sees committed model state exactly once per change", async f =>
        {
            var window = await f.Float(); var calls = 0;
            window.StateChanged += (_, _) => { calls++; Check.Equal(window.IsMaximized, f.A.IsMaximized); };
            SystemCommands.MaximizeWindow(window); Check.Equal(1, calls); SystemCommands.MaximizeWindow(window); Check.Equal(1, calls);
            SystemCommands.RestoreWindow(window); Check.Equal(2, calls);
        });
        Live("presenter max and min snapshots cannot corrupt saved normal bounds", async f =>
        {
            var window = await f.Float(); var normal = Bounds(f.A);
            Call(window, "SynchronizeNativeState", new DockRect(0, 0, 1900, 1000), OverlappedPresenterState.Maximized);
            Check.Equal(normal, Bounds(f.A));
            Call(window, "SynchronizeNativeState", new DockRect(-32000, -32000, 0, 0), OverlappedPresenterState.Minimized);
            Check.Equal(normal, Bounds(f.A));
            var restored = new DockRect(80, 90, 420, 330);
            Call(window, "SynchronizeNativeState", restored, OverlappedPresenterState.Restored);
            Check.Equal(restored, Bounds(f.A)); Check.False(window.IsMaximized);
        });
        foreach (var bounds in new[] { new DockRect(double.NaN, 1, 100, 100), new DockRect(1, double.PositiveInfinity, 100, 100), new DockRect(1, 1, 0, 100), new DockRect(1, 1, 100, -1) })
            Live("invalid display geometry leaves model unchanged: " + bounds, async f =>
            {
                var window = await f.Float(); var normal = Bounds(f.A); Call(window, "SetBounds", bounds); Check.Equal(normal, Bounds(f.A));
            });
        Live("maximized resize follows surface without changing persisted dimensions", async f =>
        {
            var window = await f.Float(); var normal = Bounds(f.A); var margin = host.Margin;
            try
            {
                SystemCommands.MaximizeWindow(window); var previous = window.Width; host.Margin = new(60); host.UpdateLayout(); await Tick();
                Check.True(window.Width < previous); Check.Equal(normal, Bounds(f.A));
            }
            finally { host.Margin = margin; host.UpdateLayout(); }
        });
        Live("navigator has distinct document and tool lists", async f =>
        {
            var nav = f.Navigator(); await Tick();
            Check.Equal(2, nav.Documents.Length); Check.Equal(1, nav.Anchorables.Count());
            var lists = nav.FindVisualChildren<ListBox>().ToArray(); Check.Equal(2, lists.Length);
            Check.Equal(2, lists.Single(l => l.Name == "PART_DocumentListBox").Items.Count);
            Check.Equal(1, lists.Single(l => l.Name == "PART_AnchorableListBox").Items.Count);
        });
        Live("navigator filters hidden documents and disabled or hidden tools", async f =>
        {
            host.GetLayoutItemFromModel(f.A).Visibility = Visibility.Collapsed; f.Tool.Hide(); var nav = f.Navigator(); await Tick();
            Check.Equal(1, nav.Documents.Length); Check.Same(f.B, nav.Documents[0].LayoutElement); Check.Equal(0, nav.Anchorables.Count());
        });
        Live("auto-hidden tools remain eligible in navigator", async f =>
        {
            f.Tool.ToggleAutoHide(); var nav = f.Navigator(); await Tick(); Check.Equal(1, nav.Anchorables.Count());
        });
        Live("navigator initialization starts next to actual active item", async f =>
        {
            f.B.IsActive = true; f.A.LastActivationTimeStamp = new(2020, 3, 3); f.B.LastActivationTimeStamp = new(2020, 2, 2); f.Tool.LastActivationTimeStamp = new(2020, 1, 1);
            var nav = f.Navigator(); await Tick(); Check.Same(f.Tool, nav.SelectedAnchorable!.LayoutElement);
        });
        Live("navigator selection dependency properties are mutually exclusive", async f =>
        {
            var nav = f.Navigator(); await Tick(); nav.SelectedAnchorable = nav.Anchorables.Single(); Check.True(nav.SelectedDocument == null);
            nav.SelectedDocument = nav.Documents[0]; Check.True(nav.SelectedAnchorable == null);
            nav.SelectedDocument = null; Check.True(nav.SelectedDocument == null); Check.True(nav.SelectedAnchorable == null);
        });
        Live("navigator rejects foreign-manager selected items", async f =>
        {
            var nav = f.Navigator(); await Tick(); nav.SelectedDocument = nav.Documents[0]; var selected = nav.SelectedDocument;
            using var other = new DockingManager(); var document = new LayoutDocument(); other.Layout = Root(new LayoutDocumentPane(document));
            nav.SelectedDocument = (LayoutDocumentItem)other.GetLayoutItemFromModel(document); Check.Same(selected, nav.SelectedDocument);
        });
        Live("removing a selected navigator item never activates detached content", async f =>
        {
            var nav = f.Navigator(); await Tick(); nav.SelectedDocument = nav.Documents.Single(i => ReferenceEquals(i.LayoutElement, f.A));
            f.A.Close(); Call(nav, "CommitSelection"); Check.True(f.A.Root == null); Check.False(ReferenceEquals(f.A, host.Layout.ActiveContent));
            await Tick(); Check.Equal(1, nav.Documents.Length);
        });
        Live("disabling a selected navigator item is checked at commit time", async f =>
        {
            var nav = f.Navigator(); await Tick(); nav.SelectedDocument = nav.Documents.Single(i => ReferenceEquals(i.LayoutElement, f.A));
            f.A.IsEnabled = false; Call(nav, "CommitSelection"); Check.False(ReferenceEquals(f.A, host.Layout.ActiveContent));
        });
        Live("session MRU order does not reshuffle on activation timestamps", async f =>
        {
            f.A.LastActivationTimeStamp = new(2020, 1, 1); f.B.LastActivationTimeStamp = new(2020, 2, 2);
            var nav = f.Navigator(); await Tick(); var first = nav.Documents[0]; f.A.LastActivationTimeStamp = new(2030, 1, 1); await Tick();
            Check.Same(first, nav.Documents[0]);
        });
        Live("new documents append to an existing navigator session", async f =>
        {
            var nav = f.Navigator(); await Tick(); var first = nav.Documents[0];
            ((LayoutDocumentPane)f.B.Parent!).Children.Add(new LayoutDocument { ContentId = "new", Title = "New" }); await Tick();
            Check.Equal(3, nav.Documents.Length); Check.Same(first, nav.Documents[0]); Check.Equal("new", nav.Documents[2].ContentId);
        });
        foreach (var delta in new[] { -1, int.MinValue, int.MaxValue, 0 })
            Live("navigator advance handles signed range " + delta, async f =>
            {
                var nav = f.Navigator(); await Tick(); Call(nav, "Advance", delta);
                Check.True(nav.SelectedDocument != null || nav.SelectedAnchorable != null);
            });
        Live("navigator cancelled session unsubscribes queued layout refresh", async f =>
        {
            var nav = f.Navigator(); await Tick(); f.A.Title = "queued"; CallSurface(host, "CloseNavigator", false);
            ((LayoutDocumentPane)f.B.Parent!).Children.Add(new LayoutDocument()); await Tick(); Check.Equal(2, nav.Documents.Length);
        });
        Live("root replacement closes navigator and invalidates stale commands", async f =>
        {
            var nav = f.Navigator(); await Tick(); var selected = nav.SelectedDocument; host.Layout = new(); host.Refresh(); await Tick();
            Call(nav, "CommitSelection"); Check.False(nav.IsLoaded); Check.True(host.Layout.ActiveContent == null);
        });
        Live("cancelled navigator returns focus to the same editor control", async f =>
        {
            f.B.IsActive = true; host.Refresh(); await Tick(); Check.True(f.EditorB.Focus(FocusState.Programmatic));
            f.Navigator(); await Tick(); CallSurface(host, "CloseNavigator", false); await Tick();
            Check.Same(f.B, host.Layout.ActiveContent); Check.Same(f.EditorB, FocusManager.GetFocusedElement(host.XamlRoot!));
        });
        Live("committed navigator returns focus to target's retained editor", async f =>
        {
            f.A.IsActive = true; host.Refresh(); await Tick(); Check.True(f.EditorA2.Focus(FocusState.Programmatic));
            f.B.IsActive = true; host.Refresh(); await Tick(); Check.True(f.EditorB.Focus(FocusState.Programmatic));
            var nav = f.Navigator(); await Tick(); nav.SelectedDocument = nav.Documents.Single(d => ReferenceEquals(d.LayoutElement, f.A));
            CallSurface(host, "CloseNavigator", true); await Tick(); Check.Same(f.A, host.Layout.ActiveContent);
            Check.True(ReferenceEquals(f.EditorA2, FocusManager.GetFocusedElement(host.XamlRoot!)),
                "Expected retained second editor, focused: " + (FocusManager.GetFocusedElement(host.XamlRoot!) as TextBox)?.Text);
        });
        Live("navigator respects activation command CanExecute", async f =>
        {
            var nav = f.Navigator(); await Tick(); var item = nav.Documents.Single(i => ReferenceEquals(i.LayoutElement, f.B));
            item.ActivateCommand = new DisabledCommand(); nav.SelectedDocument = item; CallSurface(host, "CloseNavigator", true);
            Check.Same(f.A, host.Layout.ActiveContent);
        });
        Live("navigator supports original named ListBox template parts", async f =>
        {
            var nav = new NavigatorWindow(host) { Template = (ControlTemplate)XamlReader.Load("""
                <ControlTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
                  <StackPanel><ListBox x:Name="PART_DocumentListBox"/><ListBox x:Name="PART_AnchorableListBox"/></StackPanel>
                </ControlTemplate>
                """) };
            CallSurface(host, "ShowNavigator", nav); await Tick(); nav.ApplyTemplate();
            var lists = nav.FindVisualChildren<ListBox>().ToArray(); Check.Equal(2, lists.Length);
            Check.Equal(2, lists.Single(l => l.Name == "PART_DocumentListBox").Items.Count);
            lists.Single(l => l.Name == "PART_AnchorableListBox").SelectedIndex = 0; Check.True(nav.SelectedAnchorable != null); Check.True(nav.SelectedDocument == null);
        });
        if (OperatingSystem.IsLinux() && Environment.GetEnvironmentVariable("UNODOCK_NATIVE_INPUT_TESTS") == "1")
        {
            async Task StartKeyboard(Fixture f, X11TestInput input)
            {
                f.B.IsActive = true; host.Refresh(); await Tick();
                var native = Uno.UI.ApplicationHelper.Windows.Single(w => ReferenceEquals(w.Content?.XamlRoot, host.XamlRoot));
                native.Activate(); Check.True(f.EditorB.Focus(FocusState.Programmatic)); await Tick();
                input.KeyDown(0xffe3); input.KeyPress(0xff09); await Tick();
                Check.Equal(1, host.FindVisualChildren<NavigatorWindow>().Count());

            }
            Live("XTEST Ctrl+Tab from editor commits on Control release and restores target focus", async f =>
            {
                using var input = new X11TestInput(); await StartKeyboard(f, input);
                var nav = host.FindVisualChildren<NavigatorWindow>().Single();
                var selected = (LayoutItem?)nav.SelectedDocument ?? nav.SelectedAnchorable;
                Check.True(selected != null); input.KeyUp(0xffe3); await Tick();
                Check.Equal(0, host.FindVisualChildren<NavigatorWindow>().Count());
                Check.Same(selected!.LayoutElement, host.Layout.ActiveContent);
                Check.True(FocusManager.GetFocusedElement(host.XamlRoot!) is TextBox);
            });
            Live("XTEST Escape cancels navigator and later Control release cannot commit", async f =>
            {
                using var input = new X11TestInput(); await StartKeyboard(f, input);
                input.Escape(); await Tick(); input.KeyUp(0xffe3); await Tick();
                Check.Equal(0, host.FindVisualChildren<NavigatorWindow>().Count());
                Check.Same(f.B, host.Layout.ActiveContent); Check.Same(f.EditorB, FocusManager.GetFocusedElement(host.XamlRoot!));
            });
            Live("XTEST navigator opened from native floating editor restores main editor focus", async f =>
            {
                using var input = new X11TestInput();
                host.FloatingWindowMode = FloatingWindowMode.Native;
                var floating = await f.Float(); floating.Activate(); Check.True(f.EditorA2.Focus(FocusState.Programmatic)); await Tick();
                input.MoveTo(f.EditorA2, new(20, 15)); input.Press(); input.Release(); await Tick();
                f.B.LastActivationTimeStamp = DateTime.UtcNow.AddHours(-1); f.Tool.LastActivationTimeStamp = DateTime.UtcNow.AddHours(-2);
                input.KeyDown(0xffe3); input.KeyPress(0xff09); await Tick();
                var nav = host.FindVisualChildren<NavigatorWindow>().Single();
                Check.Same(f.B, nav.SelectedDocument!.LayoutElement);
                input.KeyUp(0xffe3); await Tick();
                Check.Equal(0, host.FindVisualChildren<NavigatorWindow>().Count()); Check.Same(f.B, host.Layout.ActiveContent);
                Check.Same(f.EditorB, FocusManager.GetFocusedElement(host.XamlRoot!));
            });
            Live("XTEST category keys select tool list and Enter commits without ListBox interception", async f =>
            {
                using var input = new X11TestInput(); await StartKeyboard(f, input);
                input.KeyPress(0xff51); await Tick();
                var nav = host.FindVisualChildren<NavigatorWindow>().Single();
                Check.Same(f.Tool, nav.SelectedAnchorable!.LayoutElement); Check.True(nav.SelectedDocument == null);
                input.KeyPress(0xff0d); await Tick(); input.KeyUp(0xffe3); await Tick();
                Check.Equal(0, host.FindVisualChildren<NavigatorWindow>().Count()); Check.Same(f.Tool, host.Layout.ActiveContent);
            });
        }
        if (!OperatingSystem.IsBrowser() && !OperatingSystem.IsAndroid() && !OperatingSystem.IsIOS())
        {
            tests.Test("native hide and reopen preserves control and does not raise Closed", async () =>
            {
                using var manager = Floating(out var model); var window = new Probe(model); var editor = new TextBox { Text = "retained native content" }; window.Content = editor;
                try
                {
                    Call(window, "ShowNative"); await Tick(); var native = window.NativeWindow; Check.True(native != null); Check.Same(window, native!.Content);
                    Call(window, "HideHost"); await Tick(); Check.Equal(0, window.Closures);
                    Call(window, "ShowNative"); await Tick(); Check.Equal(1, window.Initializations); Check.Same(window, window.NativeWindow!.Content); Check.Same(editor, window.Content);
                }
                finally { Call(window, "CloseHost"); await Tick(); }
                Check.Equal(1, window.Closures);
            });
        }
        if (OperatingSystem.IsWindows()) RegisterWindows(tests);
        return await tests.Run(output, "window-lifecycle");
    }
    private static void RegisterWindows(TestRunner tests)
    {
        tests.Test("Windows FilterMessage receives HWND message and supplies handled result", async () =>
        {
            using var manager = Floating(out var model); var window = new Probe(model);
            try
            {
                Call(window, "ShowNative"); await Tick(); var hwnd = NativeHandle(window.NativeWindow!); Check.True(hwnd != 0);
                Check.Equal((nint)91, SendMessage(hwnd, 0x8061, 0, 0)); Check.Equal(1, window.FilterCalls);
                Call(window, "HideHost"); Call(window, "ShowNative"); await Tick(); hwnd = NativeHandle(window.NativeWindow!);
                Check.Equal((nint)91, SendMessage(hwnd, 0x8061, 0, 0)); Check.Equal(2, window.FilterCalls);
            }
            finally { Call(window, "CloseHost"); await Tick(); }
        });
        tests.Test("Windows native filter failure is reported without unwinding into user32", async () =>
        {
            using var manager = Floating(out var model); var window = new Probe(model) { ThrowFilter = true }; var errors = 0;
            window.MessageFilterFailed += (_, _) => errors++;
            try
            {
                Call(window, "ShowNative"); await Tick(); SendMessage(NativeHandle(window.NativeWindow!), 0x8061, 0, 0); await Tick(); Check.Equal(1, errors);
            }
            finally { Call(window, "CloseHost"); await Tick(); }
        });
    }
    private static nint NativeHandle(Window window)
    {
#if WINDOWS
        return WinRT.Interop.WindowNative.GetWindowHandle(window);
#else
        return Uno.UI.Xaml.WindowHelper.GetNativeWindow(window) is Uno.UI.NativeElementHosting.Win32NativeWindow native ? native.Hwnd : 0;
#endif
    }
    [DllImport("user32.dll", EntryPoint = "SendMessageW", ExactSpelling = true)] private static extern nint SendMessage(nint hwnd, uint message, nuint wParam, nint lParam);
    private sealed class DisabledCommand : System.Windows.Input.ICommand
    {
        public event EventHandler? CanExecuteChanged { add { } remove { } }
        public bool CanExecute(object? parameter) => false;
        public void Execute(object? parameter) => throw new InvalidOperationException("Disabled command must never execute.");
    }
    private sealed class Probe(LayoutDocumentFloatingWindow model) : LayoutDocumentFloatingWindowControl(model)
    {
        internal int Initializations, Closures, FilterCalls;
        internal bool Veto, UserCloseObserved, ThrowFilter;
        internal bool UserCloseNow => CloseInitiatedByUser;
        internal void Initialize() => EnsureInitialized();
        internal nint InvokeBaseFilter(ref bool handled) => base.FilterMessage(0, 0, 0, 0, ref handled);
        protected override void OnInitialized(EventArgs e) { Check.True(Model != null); Initializations++; base.OnInitialized(e); }
        protected override void OnClosing(CancelEventArgs e) { e.Cancel |= Veto; base.OnClosing(e); }
        protected override void OnClosed(EventArgs e) { Closures++; UserCloseObserved = CloseInitiatedByUser; base.OnClosed(e); }
        protected override nint FilterMessage(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
        {
            if (msg != 0x8061) return base.FilterMessage(hwnd, msg, wParam, lParam, ref handled);
            FilterCalls++; if (ThrowFilter) throw new InvalidOperationException("filter-test"); handled = true; return 91;
        }
    }
    private sealed class Fixture : IDisposable
    {
        private readonly DockingManager _host; private readonly LayoutRoot _old; private readonly FloatingWindowMode _mode;
        internal TextBox EditorA2 { get; } = new() { Text = "second retained editor" };
        internal TextBox EditorB { get; } = new() { Text = "other document" };
        internal LayoutDocument A { get; }
        internal LayoutDocument B { get; }
        internal LayoutAnchorable Tool { get; }
        internal Fixture(DockingManager host)
        {
            _host = host; _old = host.Layout; _mode = host.FloatingWindowMode; host.FloatingWindowMode = FloatingWindowMode.InSurface;
            var editors = new StackPanel(); editors.Children.Add(new TextBox { Text = "first editor" }); editors.Children.Add(EditorA2);
            A = new() { ContentId = "a", Title = "Alpha", Content = editors, FloatingLeft = 55, FloatingTop = 65, FloatingWidth = 360, FloatingHeight = 240 };
            B = new() { ContentId = "b", Title = "Beta", Content = EditorB };
            Tool = new() { ContentId = "tool", Title = "Inspector", Content = new TextBox() };
            var documents = new LayoutDocumentPane(A); documents.Children.Add(B);
            host.Layout = Root(documents, new LayoutAnchorablePane(Tool)); A.IsActive = true; host.Refresh(); host.UpdateLayout();
        }
        internal async Task<LayoutFloatingWindowControl> Float() { A.Float(); _host.Refresh(); await Tick(); return _host.FloatingWindows.Single(); }
        internal NavigatorWindow Navigator() { var nav = new NavigatorWindow(_host); CallSurface(_host, "ShowNavigator", nav); return nav; }
        public void Dispose() { CallSurface(_host, "CloseNavigator", false); _host.FloatingWindowMode = _mode; _host.Layout = _old; _host.Refresh(); }
    }
    private static DockRect Bounds(LayoutContent content) => new(content.FloatingLeft, content.FloatingTop, content.FloatingWidth, content.FloatingHeight);
    private static LayoutRoot Root(params ILayoutPanelElement[] items)
    { var panel = new UnoDock.Layout.LayoutPanel(); foreach (var item in items) panel.Children.Add(item); return new() { RootPanel = panel }; }
    private static DockingManager Floating(out LayoutDocumentFloatingWindow model)
    {
        var manager = new DockingManager(); var document = new LayoutDocument { Title = "Probe", Content = new TextBox() };
        manager.Layout = Root(new LayoutDocumentPane(document)); document.Float(); model = manager.Layout.FloatingWindows.OfType<LayoutDocumentFloatingWindow>().Single(); return manager;
    }
    private static void CallSurface(DockingManager host, string name, params object[] args)
    {
        var surface = typeof(DockingManager).GetProperty("Surface", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(host);
        if (surface != null) Call(surface, name, args);
    }
    private static object? Call(object target, string name, params object[] args)
    {
        var method = target.GetType().GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!;
        try { return method.Invoke(target, args); }
        catch (TargetInvocationException e) when (e.InnerException != null) { ExceptionDispatchInfo.Capture(e.InnerException).Throw(); throw; }
    }
    private static async Task Tick() => await Task.Delay(70);
}
