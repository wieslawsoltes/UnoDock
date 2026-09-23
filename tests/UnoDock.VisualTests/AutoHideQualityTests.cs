using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Xml.Linq;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Windows.Foundation;
using Windows.System;
using UnoDock.Controls;
using UnoDock.Themes;

namespace UnoDock.Testing;

/// <summary>Arranged controls and real input; public original observations are separate from pointer acceptance.</summary>
public static class AutoHideQualityTests
{
    private sealed class FocusFlyout : LayoutAutoHideWindowControl
    {
        public bool Keep { get; set; }
        protected override bool HasFocusWithinCore() => Keep || base.HasFocusWithinCore();
    }
    private sealed class CallbackTemplateSelector : DataTemplateSelector
    {
        public Action? Callback { get; set; }
        protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
        { var callback = Callback; Callback = null; callback?.Invoke(); return null!; }
    }
    private sealed class Manager : DockingManager
    {
        public Action<DependencyPropertyChangedEventArgs>? HostChanged { get; set; }
        protected override LayoutAutoHideWindowControl CreateAutoHideWindowControl() => new FocusFlyout();
        protected override void OnAutoHideWindowChanged(DependencyPropertyChangedEventArgs e)
        { base.OnAutoHideWindowChanged(e); HostChanged?.Invoke(e); }
    }
    public static async Task<int> Run(DockingManager templateSource, string output)
    {
        var tests = new TestRunner();
        using var dock = new Manager { Width = 1000, Height = 640, Template = templateSource.Template,
            RequestedTheme = ElementTheme.Light, FloatingWindowMode = FloatingWindowMode.InSurface, AutoHideWindowClosingTimer = 120 };
        var scene = new Grid { Width = 1000, Height = 640 }; scene.Children.Add(dock);
        var window = new Window { Content = scene, Title = "UnoDock auto-hide acceptance" };
        window.AppWindow.Resize(new() { Width = 1040, Height = 720 }); window.Activate();
        using var registration = Microsoft.Windows.Shell.SystemCommands.RegisterWindow(window);
        LayoutAnchorable tool = null!; LayoutDocument doc = null!; FocusFlyout flyout = null!;
        LayoutGridResizerControl splitter = null!; AnchorableShowStrategy side = AnchorableShowStrategy.Left;
        object surface = null!; bool horizontal = true;
        try
        {
            using (var stream = typeof(AutoHideQualityTests).Assembly.GetManifestResourceStream("VisualFixtures.auto-hide-window-observations.xml")!)
            {
                var observations = XDocument.Load(stream);
                foreach (var row in observations.Root!.Elements("Window"))
                    tests.Test("original flyout extent and hover activation: " + row.Attribute("side")!.Value + "/explicit=" + row.Attribute("explicitSize")!.Value, async () =>
                    {
                        await Reset(Enum.Parse<AnchorableShowStrategy>(row.Attribute("side")!.Value), requested: (bool)row.Attribute("explicitSize")! ? (double)row.Attribute("autoHideWidth")! : 0,
                            requestedHeight: (double)row.Attribute("autoHideHeight")!);
                        Check.Near((double)row.Attribute("width")!, flyout.ActualWidth, .2);
                        Check.Near((double)row.Attribute("height")!, flyout.ActualHeight, .2);
                        Check.Equal((bool)row.Attribute("active")!, tool.IsActive);
                        Check.Near((double)row.Attribute("autoHideWidth")!, tool.AutoHideWidth);
                        Check.Near((double)row.Attribute("autoHideHeight")!, tool.AutoHideHeight);
                    });
            }
            foreach (var direction in new[] { AnchorableShowStrategy.Left, AnchorableShowStrategy.Right, AnchorableShowStrategy.Top, AnchorableShowStrategy.Bottom })
            foreach (var rtl in new[] { false, true })
                tests.Test($"dedicated gutter, deferred preview and committed extent: {direction}/RTL={rtl}", async () =>
                {
                    await Reset(direction, rtl); var initial = Pixels(); var stored = Requested();
                    var editor = (FrameworkElement)tool.Content!;
                    var editorBounds = editor.TransformToVisual(flyout).TransformBounds(new(0, 0, editor.ActualWidth, editor.ActualHeight));
                    var gutter = splitter.TransformToVisual(flyout).TransformBounds(new(0, 0, splitter.ActualWidth, splitter.ActualHeight));
                    Check.True(horizontal ? gutter.Right <= editorBounds.Left + .1 || gutter.Left >= editorBounds.Right - .1 : gutter.Bottom <= editorBounds.Top + .1 || gutter.Top >= editorBounds.Bottom - .1);
                    Begin(); Move(40 * Sign()); await Settle();
                    Check.Near(initial, Pixels()); Check.Near(stored, Requested()); Check.Equal(1, Ghosts().Length);
                    End(); await Settle(); Check.Near(initial + 40, Pixels(), .2); Check.Near(stored + 40, Requested(), .2); Check.Equal(0, Ghosts().Length);
                    Call(dock, "CloseAutoHide"); Open(); await Settle(); Check.Near(stored + 40, Requested()); Check.Near(initial + 40, Pixels(), .2);
                });
            tests.Test("zero-displacement resize preserves unspecified model size", async () =>
            { await Reset(requested: 0, requestedHeight: 0); Begin(); Move(20); Move(0); End(); await Settle(); Check.Near(0, tool.AutoHideWidth); Check.Near(106, flyout.ActualWidth, .1); });
            tests.Test("preview is bounded by minimum and viewport without accumulated clamp drift", async () =>
            {
                await Reset(); Begin(); Move(-100000); End(); await Settle(); Check.Near(tool.AutoHideMinWidth, Requested());
                Begin(); Move(100000); Move(25); End(); await Settle(); Check.Near(tool.AutoHideMinWidth + 25, Requested());
                Begin(); Move(100000); End(); await Settle(); Check.True(flyout.ActualWidth <= 974.1); Check.Near(968, Requested(), .1);
            });
            tests.Test("tiny viewport clips presentation without rewriting saved sizes", async () =>
            { await Reset(); var saved = Requested(); dock.Width = 70; await Settle(); Check.True(flyout.ActualWidth <= 44.1); Check.Near(saved, Requested()); Begin(); Move(0); End(); Check.Near(saved, Requested()); });
            tests.Test("CancelDrag discards preview and leaves persisted dimensions exact", async () =>
            { await Reset(); var old = Requested(); Begin(); Move(70); splitter.CancelDrag(); End(); Check.Near(old, Requested()); Check.Equal(0, Ghosts().Length); });
            tests.Test("NaN displacement cancels and permits a fresh gesture", async () =>
            { await Reset(); Begin(); Check.Throws<ArgumentOutOfRangeException>(() => Move(double.NaN)); Check.False(splitter.IsDragging); Check.Equal(0, Ghosts().Length); Begin(); Move(10); End(); Check.Near(330, Requested()); });
            tests.Test("external dimension edit wins over pending resize", async () =>
            { await Reset(); Begin(); Move(50); tool.AutoHideWidth = 270; await Settle(); Check.False(splitter.IsDragging); End(); Check.Near(270, Requested()); Check.Equal(0, Ghosts().Length); });
            tests.Test("minimum edit invalidates pending transaction", async () =>
            { await Reset(); Begin(); Move(50); tool.AutoHideMinWidth = 180; await Settle(); End(); Check.Near(320, Requested()); Check.False(splitter.IsDragging); });
            tests.Test("root replacement invalidates old preview and releases content", async () =>
            { await Reset(); var old = tool; Begin(); Move(50); dock.Layout = new(); await Settle(); End(); Check.Near(320, old.AutoHideWidth); Check.Equal(0, Ghosts().Length); Check.True(dock.AutoHideWindow == null); Check.True(flyout.Model == null); });
            tests.Test("switching flyouts cannot apply old displacement to new model", async () =>
            {
                await Reset(); var old = tool; Begin(); Move(50);
                var next = new LayoutAnchorable { Title = "Other", ContentId = "other", Content = new TextBox(), AutoHideWidth = 260 };
                next.AddToLayout(dock, AnchorableShowStrategy.Right); next.ToggleAutoHide();
                Call(dock, "OpenAutoHide", next, false); await Settle(); End();
                Check.Near(320, old.AutoHideWidth); Check.Near(260, next.AutoHideWidth); Check.Same(next, dock.AutoHideWindow!.Model); Check.Equal(0, Ghosts().Length);
            });
            tests.Test("disabled tool closes flyout and discards resize", async () =>
            { await Reset(); Begin(); Move(50); tool.IsEnabled = false; await Settle(); End(); Check.Near(320, tool.AutoHideWidth); Check.True(dock.AutoHideWindow == null); Check.Equal(0, Ghosts().Length); });
            tests.Test("unload clears model, preview and current host", async () =>
            { await Reset(); Begin(); Move(50); scene.Children.Remove(dock); await Task.Delay(50); End(); Check.Near(320, tool.AutoHideWidth); Check.True(flyout.Model == null); Check.False(splitter.IsDragging); scene.Children.Add(dock); await Settle(); });
            tests.Test("RTL or viewport change cancels old coordinate snapshot", async () =>
            { await Reset(); Begin(); Move(50); scene.FlowDirection = FlowDirection.RightToLeft; await Settle(); End(); Check.Near(320, Requested()); Begin(); Move(50); dock.Width = 900; await Settle(); End(); Check.Near(320, Requested()); });
            tests.Test("throwing model observer rolls back only operation-owned size", async () =>
            {
                await Reset(); var once = false;
                tool.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(tool.AutoHideWidth) && !once) { once = true; throw new InvalidOperationException("observer"); } };
                Begin(); Move(40); Check.Throws<InvalidOperationException>(End); Check.Near(320, Requested()); Check.Equal(0, Ghosts().Length);
                Begin(); Move(20); End(); Check.Near(340, Requested());
            });
            tests.Test("observer replacement value is not overwritten", async () =>
            {
                await Reset(); var once = false;
                tool.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(tool.AutoHideWidth) && !once) { once = true; tool.AutoHideWidth = 280; throw new InvalidOperationException("replacement"); } };
                Begin(); Move(40); Check.Throws<InvalidOperationException>(End); Check.Near(280, Requested()); Check.Equal(0, Ghosts().Length);
            });
            tests.Test("retained flyout content and editor focus survive model refresh", async () =>
            {
                await Reset(); var view = dock.GetLayoutItemFromModel(tool).View; var editor = (TextBox)tool.Content!;
                Check.True(editor.Focus(FocusState.Programmatic)); tool.Title = "Changed"; await Settle();
                Check.Same(view, dock.GetLayoutItemFromModel(tool).View); Check.Same(editor, FocusManager.GetFocusedElement(editor.XamlRoot!)); Check.True(tool.IsActive);
            });
            tests.Test("hover path preserves active document while activation path selects tool", async () =>
            { await Reset(); Check.Same(doc, dock.Layout.ActiveContent); Call(dock, "OpenAutoHide", tool, true); await Settle(); Check.Same(tool, dock.Layout.ActiveContent); });
            tests.Test("activation callback replacing layout cannot publish a stale flyout", async () =>
            {
                await Reset(); tool.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(tool.IsActive) && tool.IsActive) dock.Layout = new(); };
                Call(dock, "OpenAutoHide", tool, true); await Settle(); Check.True(dock.AutoHideWindow == null); Check.True(flyout.Model == null);
            });
            tests.Test("focus-retention override participates in actual timeout policy", async () =>
            { await Reset(); flyout.Keep = true; Call(surface, "ExpireAutoHide"); Check.Same(flyout, dock.AutoHideWindow); flyout.Keep = false; Call(surface, "ExpireAutoHide"); Check.True(dock.AutoHideWindow == null); });
            tests.Test("focused editor blocks expiry; moving focus out re-arms timeout", async () =>
            {
                await Reset(); Check.True(((TextBox)tool.Content!).Focus(FocusState.Programmatic)); await Task.Delay(30);
                Call(surface, "ExpireAutoHide"); Check.Same(flyout, dock.AutoHideWindow);
                Check.True(((TextBox)doc.Content!).Focus(FocusState.Programmatic)); await Task.Delay(220); Check.True(dock.AutoHideWindow == null);
            });
            tests.Test("active resize blocks timeout and cancellation releases it", async () =>
            { await Reset(); Begin(); Move(50); Call(surface, "ExpireAutoHide"); Check.Same(flyout, dock.AutoHideWindow); splitter.CancelDrag(); Call(surface, "ExpireAutoHide"); Check.True(dock.AutoHideWindow == null); });
            tests.Test("menu ownership is stable across refresh and cleaned on dismissal", async () =>
            {
                await Reset(); var menu = (MenuFlyout)flyout.ContextFlyout; tool.Title = "New title"; await Settle();
                Check.Same(menu, flyout.ContextFlyout);
                try { menu.ShowAt(flyout); await Task.Delay(50); Call(surface, "ExpireAutoHide"); Check.Same(flyout, dock.AutoHideWindow); }
                finally { menu.Hide(); Call(dock, "CloseAutoHide"); await Task.Delay(100); }
                Check.True(flyout.ContextFlyout == null);
            });
            tests.Test("shared menu resolves flyout model and releases temporary context", async () =>
            {
                await Reset(); var command = new MenuFlyoutItem { Text = "Owned action" };
                var menu = new MenuFlyout(); menu.Items.Add(command); dock.AnchorableContextMenu = menu; await Settle();
                try
                {
                    Check.Same(menu, flyout.ContextFlyout); menu.ShowAt(flyout); await Task.Delay(50);
                    Check.Same(dock.GetLayoutItemFromModel(tool), command.DataContext);
                    menu.Hide(); await Task.Delay(100);
                    Check.True(ReferenceEquals(command.ReadLocalValue(FrameworkElement.DataContextProperty), DependencyProperty.UnsetValue));
                }
                finally { menu.Hide(); dock.AnchorableContextMenu = null; Call(dock, "CloseAutoHide"); await Task.Delay(100); }
            });
            tests.Test("host close callback can reopen retained flyout without old cleanup removing it", async () =>
            {
                await Reset(); var old = tool;
                var next = new LayoutAnchorable { Title = "Next", ContentId = "next", Content = new TextBox(), AutoHideWidth = 210 };
                next.AddToLayout(dock, AnchorableShowStrategy.Right); next.ToggleAutoHide();
                dock.HostChanged = e => { if (e.NewValue == null) { dock.HostChanged = null; Call(dock, "OpenAutoHide", next, false); } };
                try { Call(dock, "CloseAutoHide"); await Settle(); Check.Same(next, dock.AutoHideWindow!.Model); Check.True(dock.AutoHideWindow.IsLoaded); Check.Near(216, dock.AutoHideWindow.ActualWidth, .2); Check.True(old.IsAutoHidden); }
                finally { dock.HostChanged = null; }
            });
            tests.Test("reentrant host close callback supersedes an in-progress switch", async () =>
            {
                await Reset(); var requested = new LayoutAnchorable { Title = "Requested", ContentId = "requested", Content = new TextBox() };
                var replacement = new LayoutAnchorable { Title = "Replacement", ContentId = "replacement", Content = new TextBox() };
                requested.AddToLayout(dock, AnchorableShowStrategy.Right); requested.ToggleAutoHide();
                replacement.AddToLayout(dock, AnchorableShowStrategy.Bottom); replacement.ToggleAutoHide();
                dock.HostChanged = e => { if (e.NewValue == null) { dock.HostChanged = null; Call(dock, "OpenAutoHide", replacement, false); } };
                try { Call(dock, "OpenAutoHide", requested, false); await Settle(); Check.Same(replacement, dock.AutoHideWindow!.Model); Check.True(dock.AutoHideWindow.IsLoaded); }
                finally { dock.HostChanged = null; }
            });
            tests.Test("throwing host-close observer leaves no flyout model or capture", async () =>
            {
                await Reset(); Begin(); Move(35);
                dock.HostChanged = e => { if (e.NewValue == null) throw new InvalidOperationException("Owned callback failure"); };
                try { Check.Throws<InvalidOperationException>(() => Call(dock, "CloseAutoHide")); }
                finally { dock.HostChanged = null; }
                Check.True(flyout.Model == null); Check.False(splitter.IsDragging); Check.Equal(0, Ghosts().Length);
                Open(); await Settle(); Check.Same(tool, dock.AutoHideWindow!.Model);
            });
            tests.Test("title selector replacing the root cannot leave a stale host", async () =>
            {
                await Reset(); Call(dock, "CloseAutoHide");
                var selector = new CallbackTemplateSelector { Callback = () => dock.Layout = new() };
                dock.AnchorableTitleTemplateSelector = selector;
                try { Open(); await Settle(); Check.True(dock.AutoHideWindow == null); Check.True(flyout.Model == null); }
                finally { dock.AnchorableTitleTemplateSelector = null; }
            });
            tests.Test("title selector redirecting to another flyout owns the final chrome", async () =>
            {
                await Reset(); Call(dock, "CloseAutoHide");
                var next = new LayoutAnchorable { Title = "Replacement title", ContentId = "replacement", Content = new TextBox(), AutoHideWidth = 245 };
                next.AddToLayout(dock, AnchorableShowStrategy.Right); next.ToggleAutoHide();
                var selector = new CallbackTemplateSelector { Callback = () => Call(dock, "OpenAutoHide", next, false) };
                dock.AnchorableTitleTemplateSelector = selector;
                try { Open(); await Settle(); Check.Same(next, dock.AutoHideWindow!.Model); Check.Near(251, dock.AutoHideWindow.ActualWidth, .2); Check.True(dock.AutoHideWindow.FindVisualChildren<TextBlock>().Any(t => t.Text == next.Title)); }
                finally { dock.AnchorableTitleTemplateSelector = null; }
            });
            tests.Test("font density expands caption without replacing retained content", async () =>
            {
                await Reset(); var view = dock.GetLayoutItemFromModel(tool).View;
                dock.Resources["UnoDock.FontSize"] = 24d; await Settle();
                var caption = flyout.FindVisualChildren<TextBlock>().Single(t => t.Text == tool.Title);
                Check.Near(24, caption.FontSize); Check.True(caption.ActualHeight >= 24); Check.Same(view, dock.GetLayoutItemFromModel(tool).View);
                dock.Resources.Remove("UnoDock.FontSize");
            });
            foreach (var direction in new[] { AnchorableShowStrategy.Left, AnchorableShowStrategy.Right, AnchorableShowStrategy.Top, AnchorableShowStrategy.Bottom })
                tests.Test("keyboard consumes only movement axis: " + direction, async () =>
                {
                    await Reset(direction); Check.False((bool)Call(splitter, "ResizeFromKey", horizontal ? VirtualKey.Down : VirtualKey.Right)!);
                    Check.True((bool)Call(splitter, "ResizeFromKey", horizontal ? VirtualKey.Right : VirtualKey.Down)!);
                    Check.Near(320 + 10 * Sign(), Requested(), .1);
                });
            foreach (var variant in new[] { "left", "right", "top", "bottom", "rtl", "dark" })
                tests.Test("capture auto-hide flyout and independent preview: " + variant, async () =>
                {
                    var direction = variant switch { "right" => AnchorableShowStrategy.Right, "top" => AnchorableShowStrategy.Top, "bottom" => AnchorableShowStrategy.Bottom, _ => AnchorableShowStrategy.Left };
                    await Reset(direction, variant == "rtl"); if (variant == "dark") dock.Theme = new FluentTheme(ElementTheme.Dark);
                    Check.Equal(6, ((TextBox)tool.Content!).Text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n').Length);
                    await VisualCapture.Save(flyout, Path.Combine(output, "visuals", "auto-hide-client-" + variant + ".png"));
                    Begin(); Move(Sign() * 55); await Settle(); Check.Equal(1, Ghosts().Length);
                    await VisualCapture.Save(scene, Path.Combine(output, "visuals", "auto-hide-" + variant + ".png")); splitter.CancelDrag();
                });
            if (OperatingSystem.IsLinux() && Environment.GetEnvironmentVariable("UNODOCK_NATIVE_INPUT_TESTS") == "1")
            {
                tests.Test("XTEST hover does not activate tool and click does", async () =>
                {
                    await Reset(); Call(dock, "CloseAutoHide"); await Settle(); using var input = new X11TestInput();
                    var anchor = dock.FindVisualChildren<LayoutAnchorControl>().Single(a => ReferenceEquals(a.Model, tool));
                    input.MoveTo(anchor, new(anchor.ActualWidth / 2, anchor.ActualHeight / 2)); await Task.Delay(100);
                    Check.Same(doc, dock.Layout.ActiveContent); Check.True(dock.AutoHideWindow != null);
                    input.Press(); input.Release(); await Task.Delay(60); Check.Same(tool, dock.Layout.ActiveContent);
                });
                foreach (var rtl in new[] { false, true })
                    tests.Test("XTEST deferred resize primary release: RTL=" + rtl, async () =>
                    {
                        await Reset(rtl: rtl); using var input = new X11TestInput(); await PointerBegin(input);
                        var start = splitter.TransformToVisual(scene).TransformPoint(new(splitter.ActualWidth / 2, splitter.ActualHeight / 2));
                        input.MoveTo(scene, new(start.X + 45, start.Y)); await Task.Delay(60);
                        Check.Near(320, tool.AutoHideWidth); Check.True(splitter.IsDragging); input.Release(); await Settle(); Check.Near(365, tool.AutoHideWidth, 1.1);
                    });
                foreach (var operation in new[] { "escape", "capture-loss", "disable", "unload" })
                    tests.Test("XTEST flyout resize cancellation: " + operation, async () =>
                    {
                        await Reset(); using var input = new X11TestInput(); await PointerBegin(input);
                        var start = splitter.TransformToVisual(scene).TransformPoint(new(splitter.ActualWidth / 2, splitter.ActualHeight / 2));
                        input.MoveTo(scene, new(start.X + 45, start.Y)); await Task.Delay(60); Check.True(splitter.IsDragging);
                        switch (operation)
                        {
                            case "escape": input.Escape(); break;
                            case "capture-loss": splitter.FindVisualChildren<Thumb>().Single().ReleasePointerCaptures(); break;
                            case "disable": splitter.IsEnabled = false; break;
                            case "unload": scene.Children.Remove(dock); break;
                        }
                        await Task.Delay(50); input.Release(); await Task.Delay(70); Check.Near(320, tool.AutoHideWidth); Check.False(splitter.IsDragging); Check.Equal(0, Ghosts().Length);
                        if (!scene.Children.Contains(dock)) scene.Children.Add(dock);
                    });
            }
            AutoHideChromeTests.Register(tests, dock, scene, () => flyout, () => tool, async () => await Reset(), Settle);
            return await tests.Run(output, "auto-hide-quality");
        }
        finally { Call(dock, "CloseAutoHide"); window.Content = null; window.Close(); }

        double Pixels() => horizontal ? flyout.ActualWidth : flyout.ActualHeight;
        double Requested() => horizontal ? tool.AutoHideWidth : tool.AutoHideHeight;
        double Sign() => side is AnchorableShowStrategy.Left or AnchorableShowStrategy.Top ? 1 : -1;
        Border[] Ghosts() => scene.FindVisualChildren<Border>().Where(b => b.Name == "PART_AutoHideResizePreview").ToArray();
        void Begin() => Call(splitter, "BeginResize");
        void Move(double delta) => Call(splitter, "UpdateResize", delta);
        void End() => Call(splitter, "EndResize", false);
        void Open() => Call(dock, "OpenAutoHide", tool, false);
        async Task Settle() { dock.Refresh(); scene.UpdateLayout(); await Task.Delay(40); scene.UpdateLayout(); }
        async Task Reset(AnchorableShowStrategy direction = AnchorableShowStrategy.Left, bool rtl = false, double requested = 320, double requestedHeight = 320)
        {
            Call(dock, "CloseAutoHide"); if (!scene.Children.Contains(dock)) scene.Children.Add(dock);
            dock.Width = 1000; dock.Theme = null; scene.FlowDirection = rtl ? FlowDirection.RightToLeft : FlowDirection.LeftToRight; side = direction;
            horizontal = direction is AnchorableShowStrategy.Left or AnchorableShowStrategy.Right;
            doc = new LayoutDocument { ContentId = "editor", Title = "Workspace.cs", Content = new TextBox { Text = "The document retains its editor and geometry while an auto-hidden tool is previewed or resized.", AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, Padding = new(16) } };
            dock.Layout = new() { RootPanel = new(new LayoutDocumentPane(doc)) };
            tool = new() { Title = "Solution Explorer", ContentId = "tool", AutoHideWidth = requested, AutoHideHeight = requestedHeight,
                Content = new TextBox { AcceptsReturn = true, FontSize = 12, Padding = new(10), BorderThickness = new(0), Text = "Solution\n  Sources\n    Workspace.cs\n    Layout.cs\n  Tests\n    AutoHideQuality.cs" } };
            tool.AddToLayout(dock, direction | AnchorableShowStrategy.Most); tool.ToggleAutoHide(); doc.IsActive = true;
            window.Activate(); await Settle(); Open(); await Settle();
            flyout = (FocusFlyout)dock.AutoHideWindow!; flyout.Keep = false;
            splitter = flyout.FindVisualChildren<LayoutGridResizerControl>().Single(); splitter.IsEnabled = true;
            surface = typeof(DockingManager).GetProperty("Surface", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(dock)!;
            Check.True(splitter.IsLoaded && splitter.ActualWidth > 0 && splitter.ActualHeight > 0);
        }
        async Task PointerBegin(X11TestInput input)
        {
            window.Activate(); await Task.Delay(60);
            input.MoveTo(splitter, new(splitter.ActualWidth / 2, splitter.ActualHeight / 2)); await Task.Delay(40);
            input.Press(); await Task.Delay(50); Check.True(splitter.IsDragging);
        }
    }
    private static object? Call(object target, string method, params object[] args)
    {
        var type = target.GetType(); MethodInfo? info = null;
        while (type != null && (info = type.GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)) == null) type = type.BaseType;
        try { return (info ?? throw new MissingMethodException(method)).Invoke(target, args); }
        catch (TargetInvocationException e) when (e.InnerException != null) { ExceptionDispatchInfo.Capture(e.InnerException).Throw(); throw; }
    }
}
