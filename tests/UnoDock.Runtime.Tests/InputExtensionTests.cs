using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Xceed.Wpf.AvalonDock;
using Xceed.Wpf.AvalonDock.Compatibility;
using Xceed.Wpf.AvalonDock.Controls;
using Xceed.Wpf.AvalonDock.Layout;

namespace UnoDock.Testing;

using LayoutPanel = Xceed.Wpf.AvalonDock.Layout.LayoutPanel;

public static class InputExtensionTests
{
    public static async Task<int> Run(DockingManager host, string output)
    {
        var tests = new TestRunner();
        tests.Test("pane construction does not dispatch overridable selection callbacks", () =>
        {
            var (_, pane, a, _) = Pane(); var view = new SelectionProbe(pane);
            Check.Equal(0, view.Calls); Check.Same(a, view.SelectedItem); Check.Equal(0, view.SelectedIndex); Release(view);
        });
        tests.Test("selection DP writes update model, item and index before public event", () =>
        {
            var (_, pane, a, b) = Pane(); var view = new SelectionProbe(pane); var events = 0;
            view.SelectionChanged += (_, e) => { events++; Check.Same(b, pane.SelectedContent); Check.Same(b, view.SelectedItem); Check.Equal(1, view.SelectedIndex); Check.Same(a, e.RemovedItems.Single()); Check.Same(b, e.AddedItems.Single()); };
            view.SetValue(LayoutCachePaneControl.SelectedIndexProperty, 1);
            Check.Equal(1, events); Check.Equal(1, view.Calls); Release(view);
        });
        tests.Test("model selection synchronously updates view dependency properties", () =>
        {
            var (_, pane, _, b) = Pane(); var view = new SelectionProbe(pane);
            b.IsSelected = true; Check.Equal(1, view.SelectedIndex); Check.Same(b, view.SelectedItem); Check.Equal(1, view.Calls); Release(view);
        });
        tests.Test("same selection is not emitted twice", () =>
        {
            var (_, pane, _, b) = Pane(); var view = new SelectionProbe(pane);
            view.SelectedItem = b; view.SelectedIndex = 1; b.IsSelected = true; Check.Equal(1, view.Calls); Release(view);
        });
        tests.Test("null item clears selection and flags", () =>
        {
            var (_, pane, a, _) = Pane(); var view = new SelectionProbe(pane);
            view.SelectedItem = null; Check.Equal(-1, pane.SelectedContentIndex); Check.Equal(-1, view.SelectedIndex); Check.False(a.IsSelected); Release(view);
        });
        tests.Test("foreign selected item is rejected without corrupting model or DP", () =>
        {
            var (_, pane, a, _) = Pane(); var view = new SelectionProbe(pane);
            view.SelectedItem = new LayoutDocument(); Check.Same(a, pane.SelectedContent); Check.Same(a, view.SelectedItem); Check.Equal(0, view.Calls); Release(view);
        });
        foreach (var index in new[] { -2, 2, int.MaxValue })
            tests.Test("invalid index rolls back both selection properties: " + index, () =>
            {
                var (_, pane, a, _) = Pane(); var view = new SelectionProbe(pane);
                Check.Throws<ArgumentOutOfRangeException>(() => view.SelectedIndex = index);
                Check.Equal(0, view.SelectedIndex); Check.Same(a, view.SelectedItem); Check.Same(a, pane.SelectedContent); Release(view);
            });
        tests.Test("moving selected child changes index without selection notification", () =>
        {
            var (_, pane, a, _) = Pane(); var view = new SelectionProbe(pane);
            pane.Children.Move(0, 1); Check.Same(a, view.SelectedItem); Check.Equal(1, view.SelectedIndex); Check.Equal(0, view.Calls); Release(view);
        });
        tests.Test("inserting before selection preserves identity and updates index", () =>
        {
            var (_, pane, a, _) = Pane(); var view = new SelectionProbe(pane);
            pane.Children.Insert(0, new LayoutDocument()); Check.Equal(1, view.SelectedIndex); Check.Same(a, view.SelectedItem); Check.Equal(0, view.Calls); Release(view);
        });
        tests.Test("removing selected child emits replacement selection once", () =>
        {
            var (_, pane, a, b) = Pane(); var view = new SelectionProbe(pane);
            pane.Children.Remove(a); Check.Same(b, view.SelectedItem); Check.Equal(0, view.SelectedIndex); Check.Equal(1, view.Calls); Release(view);
        });
        tests.Test("clearing pane emits empty selection", () =>
        {
            var (_, pane, _, _) = Pane(); var view = new SelectionProbe(pane);
            pane.Children.Clear(); Check.Equal(-1, view.SelectedIndex); Check.True(view.SelectedItem == null); Release(view);
        });
        tests.Test("selection callback can request another selection without losing it", () =>
        {
            var (_, pane, a, b) = Pane(); var c = new LayoutDocument(); pane.Children.Add(c); var view = new SelectionProbe(pane);
            view.SelectionChanged += (_, _) => { if (ReferenceEquals(view.SelectedItem, b)) view.SelectedItem = c; };
            view.SelectedItem = b; Check.Same(c, pane.SelectedContent); Check.Same(c, view.SelectedItem);
            Check.Equal(2, view.Calls); Check.False(a.IsSelected); Check.False(b.IsSelected); Check.True(c.IsSelected); Release(view);
        });
        tests.Test("model observer reentrant selection is drained in order", () =>
        {
            var (_, pane, _, b) = Pane(); var c = new LayoutDocument(); pane.Children.Add(c);
            pane.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(pane.SelectedContent) && ReferenceEquals(pane.SelectedContent, b)) pane.SelectedContentIndex = 2; };
            pane.SelectedContentIndex = 1; Check.Same(c, pane.SelectedContent); Check.True(c.IsSelected); Check.False(b.IsSelected);
        });
        tests.Test("last explicit reentrant selection can withdraw an earlier request", () =>
        {
            var (_, pane, a, b) = Pane(); var c = new LayoutDocument(); pane.Children.Add(c);
            a.IsSelectedChanged += (_, _) => { if (!a.IsSelected) { pane.SelectedContentIndex = 2; pane.SelectedContentIndex = 1; } };
            pane.SelectedContentIndex = 1; Check.Same(b, pane.SelectedContent); Check.True(b.IsSelected); Check.False(c.IsSelected);
        });
        tests.Test("internal selected-flag synchronization does not overwrite a queued user request", () =>
        {
            var (_, pane, a, b) = Pane(); var c = new LayoutDocument(); pane.Children.Add(c);
            a.IsSelectedChanged += (_, _) => { if (!a.IsSelected) pane.SelectedContentIndex = 2; };
            pane.SelectedContentIndex = 1; Check.Same(c, pane.SelectedContent); Check.True(c.IsSelected); Check.False(b.IsSelected);
        });
        tests.Test("selection callback removal does not enumerate a mutating collection", () =>
        {
            var (_, pane, a, b) = Pane(); var c = new LayoutDocument(); pane.Children.Add(c);
            a.IsSelectedChanged += (_, _) => { if (!a.IsSelected && pane.Children.Contains(b)) pane.Children.Remove(b); };
            pane.SelectedContentIndex = 1;
            Check.True(pane.SelectedContent == null || pane.Children.Contains(pane.SelectedContent));
            Check.True(pane.Children.Count(x => x.IsSelected) <= 1);
        });
        tests.Test("throwing selection subscriber does not poison future transitions", () =>
        {
            var (_, pane, a, b) = Pane(); var view = new SelectionProbe(pane);
            SelectionChangedEventHandler bad = (_, _) => throw new InvalidOperationException("subscriber");
            view.SelectionChanged += bad; Check.Throws<InvalidOperationException>(() => view.SelectedIndex = 1);
            view.SelectionChanged -= bad; view.SelectedItem = a; Check.Same(a, pane.SelectedContent); Check.False(b.IsSelected); Release(view);
        });
        tests.Test("nonconvergent model selection observers are bounded and recoverable", () =>
        {
            var (_, pane, _, _) = Pane();
            PropertyChangedEventHandler loop = (_, e) => { if (e.PropertyName == nameof(pane.SelectedContent)) pane.SelectedContentIndex = 1 - pane.SelectedContentIndex; };
            pane.PropertyChanged += loop; Check.Throws<InvalidOperationException>(() => pane.SelectedContentIndex = 1);
            pane.PropertyChanged -= loop; pane.SelectedContentIndex = -1; Check.True(pane.SelectedContent == null);
        });
        tests.Test("released pane view no longer observes model changes", () =>
        {
            var (_, pane, _, _) = Pane(); var view = new SelectionProbe(pane); Release(view);
            pane.SelectedContentIndex = 1; Check.Equal(0, view.Calls);
        });
        tests.Test("pane observer does not retain discarded view", () =>
        {
            var (_, pane, _, _) = Pane(); var weak = AbandonView(pane);
            for (var i = 0; i < 3; i++) { GC.Collect(); GC.WaitForPendingFinalizers(); }
            Check.False(weak.IsAlive); pane.SelectedContentIndex = 1;
        });
        tests.Test("bindable SelectedIndex supports actual two-way binding", async () =>
        {
            var (_, pane, _, b) = Pane(); var view = new SelectionProbe(pane); var source = new SelectionSource();
            view.SetBinding(LayoutCachePaneControl.SelectedIndexProperty, new Binding { Source = source, Path = new PropertyPath(nameof(source.Index)), Mode = BindingMode.TwoWay });
            source.Index = 1; await Task.Delay(20); Check.Same(b, pane.SelectedContent);
            pane.SelectedContentIndex = 0; await Task.Delay(20); Check.Equal(0, source.Index);
            source.Index = 1; await Task.Delay(20); Check.Equal(1, view.SelectedIndex); Release(view);
        });
        tests.Test("weak event override receives real collection notifications", () =>
        {
            using var manager = new SourceProbe(); var source = new ObservableCollection<object>(); manager.DocumentsSource = source;
            source.Add(new object()); Check.Equal(1, manager.Calls); Check.Equal(1, manager.Layout.Descendents().OfType<LayoutDocument>().Count());
        });
        tests.Test("weak event override can suppress default reconciliation", () =>
        {
            using var manager = new SourceProbe { Suppress = true }; var source = new ObservableCollection<object>(); manager.DocumentsSource = source;
            source.Add(new object()); Check.Equal(1, manager.Calls); Check.Equal(0, manager.Layout.Descendents().OfType<LayoutDocument>().Count());
            manager.Suppress = false; source.Add(new object()); Check.Equal(2, manager.Layout.Descendents().OfType<LayoutDocument>().Count());
        });
        tests.Test("unknown weak event manager or sender is not accepted", () =>
        {
            using var manager = new SourceProbe(); var source = new ObservableCollection<object>(); manager.DocumentsSource = source;
            var listener = (IWeakEventListener)manager;
            Check.False(listener.ReceiveWeakEvent(typeof(string), source, EventArgs.Empty));
            Check.False(listener.ReceiveWeakEvent(typeof(INotifyCollectionChanged), new object(), new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset)));
        });
        tests.Test("replaced source subscription cannot dispatch", () =>
        {
            using var manager = new SourceProbe(); var old = new ObservableCollection<object>(); manager.DocumentsSource = old; manager.DocumentsSource = new ObservableCollection<object>();
            old.Add(new object()); Check.Equal(0, manager.Calls);
        });
        tests.Test("disposed source listener cannot dispatch", () =>
        {
            var manager = new SourceProbe(); var source = new ObservableCollection<object>(); manager.DocumentsSource = source; manager.Dispose();
            source.Add(new object()); Check.Equal(0, manager.Calls);
        });
        tests.Test("worker collection events marshal the override to UI thread", async () =>
        {
            using var manager = new SourceProbe(); var source = new ObservableCollection<object>(); manager.DocumentsSource = source;
            await Task.Run(() => source.Add(new object())); await Task.Delay(50);
            Check.Equal(1, manager.Calls); Check.True(manager.AllOnUi); Check.Equal(1, manager.Layout.Descendents().OfType<LayoutDocument>().Count());
        });
        tests.Test("queued event from replaced collection is discarded", async () =>
        {
            using var manager = new SourceProbe(); var source = new ObservableCollection<object>(); manager.DocumentsSource = source;
            Task.Run(() => source.Add(new object())).GetAwaiter().GetResult();
            manager.DocumentsSource = new ObservableCollection<object>(); await Task.Delay(50);
            Check.Equal(0, manager.Calls); Check.Equal(0, manager.Layout.Descendents().OfType<LayoutDocument>().Count());
        });
        tests.Test("throwing weak event override does not poison later reconciliation", () =>
        {
            using var manager = new SourceProbe { Throw = true }; var source = new ObservableCollection<object>(); manager.DocumentsSource = source;
            Check.Throws<InvalidOperationException>(() => source.Add(new object())); manager.Throw = false; source.Add(new object());
            Check.Equal(2, manager.Layout.Descendents().OfType<LayoutDocument>().Count());
        });
        tests.Test("focus hooks observe native transition and activate only after focus", async () =>
        {
            var (root, _, a, b) = Pane(); using var manager = new DockingManager { Layout = root }; a.IsActive = true;
            var outside = new Button { Content = "outside" }; var inside = new Button { Content = "inside" };
            var probe = new FocusProbe { Model = b, Content = inside };
            await InWindow(new StackPanel { Children = { outside, probe } }, async () =>
            {
                outside.Focus(FocusState.Programmatic); await Task.Delay(30); probe.Previews = probe.Completed = 0;
                probe.DuringPreview = () => Check.Same(a, root.ActiveContent);
                Check.True(inside.Focus(FocusState.Programmatic)); await Task.Delay(40);
                Check.True(probe.Previews > 0); Check.True(probe.Completed > 0); Check.Same(b, root.ActiveContent);
                Check.Same(inside, probe.NewFocus); Check.Same(outside, probe.OldFocus);
            });
        });
        tests.Test("preview focus cancellation prevents activation and post-focus callback", async () =>
        {
            var (root, _, a, b) = Pane(); using var manager = new DockingManager { Layout = root }; a.IsActive = true;
            var outside = new Button { Content = "outside" }; var inside = new Button { Content = "inside" };
            var probe = new FocusProbe { Model = b, Content = inside };
            await InWindow(new StackPanel { Children = { outside, probe } }, async () =>
            {
                outside.Focus(FocusState.Programmatic); await Task.Delay(30); a.IsActive = true;
                probe.Cancel = true; probe.Previews = probe.Completed = 0;
                inside.Focus(FocusState.Programmatic); await Task.Delay(30);
                Check.True(probe.Previews > 0); Check.Equal(0, probe.Completed); Check.Same(a, root.ActiveContent);
                Check.Same(outside, Microsoft.UI.Xaml.Input.FocusManager.GetFocusedElement(outside.XamlRoot!));
            });
        });
        tests.Test("completed focus hook can suppress default model activation", async () =>
        {
            var (root, _, a, b) = Pane(); using var manager = new DockingManager { Layout = root }; a.IsActive = true;
            var outside = new Button { Content = "outside" }; var inside = new Button { Content = "inside" };
            var probe = new FocusProbe { Model = b, Content = inside, Suppress = true };
            await InWindow(new StackPanel { Children = { outside, probe } }, async () =>
            {
                outside.Focus(FocusState.Programmatic); await Task.Delay(30); a.IsActive = true;
                inside.Focus(FocusState.Programmatic); await Task.Delay(30);
                Check.True(probe.Completed > 0); Check.Same(a, root.ActiveContent);
                Check.Same(inside, Microsoft.UI.Xaml.Input.FocusManager.GetFocusedElement(inside.XamlRoot!));
            });
        });
        tests.Test("custom pane and tab factories participate in actual rendering", async () =>
        {
            var (root, pane, _, b) = Pane(); using var manager = new FactoryProbe { Layout = root, Template = host.Template, Width = 360, Height = 150 };
            await InWindow(manager, async () =>
            {
                manager.Refresh(); manager.UpdateLayout(); await Task.Delay(40);
                Check.Equal(1, manager.DocumentPanes); Check.Equal(2, manager.Tabs);
                var view = manager.FindVisualChildren<FactoryPane>().Single(); Check.Same(pane, view.Model);
                Check.Equal(2, view.FindVisualChildren<InputProbe>().Count());
                manager.Refresh(); Check.Equal(1, manager.DocumentPanes); Check.Equal(2, manager.Tabs);
                view.SelectedItem = b; Check.Same(b, pane.SelectedContent);
            });
        });
        if (OperatingSystem.IsLinux() && Environment.GetEnvironmentVariable("UNODOCK_NATIVE_INPUT_TESTS") == "1")
        {
            foreach (var mode in new[] { "allow", "preview-veto", "down-veto" })
                tests.Test("XTEST protected tab press path: " + mode, async () =>
                {
                    var (root, _, a, b) = Pane(); using var manager = new DockingManager { Layout = root }; a.IsActive = true;
                    var tab = new InputProbe { Model = b, PreviewVeto = mode == "preview-veto", DownVeto = mode == "down-veto" };
                    await InWindow(tab, async () =>
                    {
                        using var input = new X11TestInput(); input.MoveTo(tab, new(30, 12)); await Task.Delay(60); input.Press(); await Task.Delay(60); input.Release(); await Task.Delay(80);
                        Check.Equal(1, tab.Previews); Check.Equal(mode == "preview-veto" ? 0 : 1, tab.Downs);
                        Check.Same(mode == "allow" ? b : a, root.ActiveContent);
                    });
                });
            tests.Test("XTEST middle-button hook closes exactly once", async () =>
            {
                var (root, pane, _, b) = Pane(); using var manager = new DockingManager { Layout = root }; var tab = new InputProbe { Model = b };
                await InWindow(tab, async () => { using var input = new X11TestInput(); input.MoveTo(tab, new(30, 12)); await Task.Delay(40); input.Press(2); await Task.Delay(30); input.Release(2); await Task.Delay(80); Check.Equal(1, tab.MiddleDowns); Check.False(pane.Children.Contains(b)); });
            });
            tests.Test("XTEST ordinary right-button release reaches override once", async () =>
            {
                var tab = new InputProbe(); await InWindow(tab, async () =>
                {
                    using var input = new X11TestInput(); input.MoveTo(tab, new(30, 12)); await Task.Delay(40);
                    input.Press(3); await Task.Delay(40); input.Release(3); await Task.Delay(60);
                    Check.Equal(1, tab.RightUps); Check.Equal(0, tab.Ups);
                });
            });
            tests.Test("XTEST button chord preserves primary release identity", async () =>
            {
                var tab = new InputProbe(); await InWindow(tab, async () =>
                {
                    using var input = new X11TestInput(); input.MoveTo(tab, new(30, 12)); await Task.Delay(40);
                    input.Press(); await Task.Delay(30); input.Press(3); await Task.Delay(30); input.Release(3); await Task.Delay(30); input.Release(); await Task.Delay(80);
                    Check.Equal(1, tab.Ups); // X11 may omit secondary-only transitions; do not invent them.
                });
            });
            tests.Test("XTEST disabling a captured custom tab cancels docking immediately", async () =>
            {
                var (root, pane, _, b) = Pane(); using var manager = new FactoryProbe { Layout = root, Template = host.Template, Width = 360, Height = 150, FloatingWindowMode = FloatingWindowMode.InSurface };
                await InWindow(manager, async () =>
                {
                    manager.Refresh(); manager.UpdateLayout(); await Task.Delay(40);
                    var view = manager.FindVisualChildren<FactoryPane>().Single();
                    var tab = view.FindVisualChildren<InputProbe>().Single(t => ReferenceEquals(t.Model, b));
                    using var input = new X11TestInput(); await input.Begin(tab, new(30, 12));
                    input.MoveTo(view, new(view.ActualWidth / 2, view.ActualHeight - 5)); await Task.Delay(50);
                    tab.IsEnabled = false; await Task.Delay(20);
                    await input.Drop(view, new(view.ActualWidth / 2, view.ActualHeight - 5));
                    Check.Same(pane, b.Parent); Check.Equal(0, manager.FloatingWindows.Count());
                    tab.IsEnabled = true;
                });
            });
            tests.Test("XTEST release override vetoes a real edge-compatibility docking operation", async () =>
            {
                var guideMode = host.DockingGuideMode; host.DockingGuideMode = DockingGuideMode.GuidesAndEdges;
                var old = host.Layout; var a = new LayoutDocument { Title = "stay" }; var b = new LayoutDocument { Title = "drag" };
                var pane = new LayoutDocumentPane(a); pane.Children.Add(b); host.FloatingWindowMode = FloatingWindowMode.InSurface;
                host.Layout = new() { RootPanel = new(pane) }; host.Refresh(); host.UpdateLayout(); await Task.Delay(80);
                var control = host.FindVisualChildren<LayoutDocumentPaneControl>().Single();
                var tabs = (IDictionary)typeof(LayoutCachePaneControl).GetField("_tabs", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(control)!;
                var previous = (LayoutDocumentTabItem)tabs[b]!;
                typeof(LayoutTabItemBase).GetMethod("DetachModel", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(previous, null);
                var probe = new InputProbe { Model = b, ReleaseVeto = true }; tabs[b] = probe;
                host.Refresh(); host.UpdateLayout(); await Task.Delay(80);
                try
                {
                    // Closing the temporary specimen windows does not reactivate the main X11
                    // window on a WM-less display. Do not spend the tested press on activation.
                    Uno.UI.ApplicationHelper.Windows.Single(w => ReferenceEquals(w.Content?.XamlRoot, host.XamlRoot)).Activate();
                    await Task.Delay(80);
                    using var input = new X11TestInput(); await input.Begin(probe, new(30, 12));
                    await input.Drop(control, new(control.ActualWidth / 2, control.ActualHeight - 10));
                    Check.Equal(1, probe.Ups); Check.True(probe.Moves > 0); Check.Same(pane, b.Parent);
                    probe.ReleaseVeto = false; await input.Begin(probe, new(30, 12)); await input.Drop(control, new(control.ActualWidth / 2, control.ActualHeight - 10));
                    Check.False(ReferenceEquals(pane, b.Parent));
                }
                finally { host.DockingGuideMode = guideMode; host.Layout = old; host.Refresh(); await Task.Delay(80); }
            });
        }
        return await tests.Run(output, "input-extensions");
    }
    private static (LayoutRoot root, LayoutDocumentPane pane, LayoutDocument a, LayoutDocument b) Pane()
    {
        var a = new LayoutDocument { Title = "first" }; var b = new LayoutDocument { Title = "second" };
        var pane = new LayoutDocumentPane(a); pane.Children.Add(b); return (new() { RootPanel = new(pane) }, pane, a, b);
    }
    private static void Release(LayoutCachePaneControl view) => typeof(LayoutCachePaneControl).GetMethod("ReleaseViews", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(view, null);
    [MethodImpl(MethodImplOptions.NoInlining)] private static WeakReference AbandonView(LayoutDocumentPane pane) => new(new SelectionProbe(pane));
    private static async Task InWindow(UIElement content, Func<Task> test)
    {
        var window = new Window { Content = new Grid { Padding = new Thickness(20), Children = { new Border { Child = content, VerticalAlignment = VerticalAlignment.Top, HorizontalAlignment = HorizontalAlignment.Left, MinWidth = 180, MinHeight = 40 } } } };
        window.AppWindow.Move(new() { X = 100, Y = 100 }); window.AppWindow.Resize(new() { Width = 450, Height = 220 }); window.Activate();
        try { await Task.Delay(120); await test(); }
        finally { window.Content = null; window.Close(); await Task.Delay(100); }
    }
    private sealed class FocusProbe : LayoutAnchorableControl
    {
        public int Previews, Completed; public bool Cancel, Suppress; public DependencyObject? OldFocus, NewFocus; public Action? DuringPreview;
        // Use the document base property: this probe tests common content-control activation.
        public new LayoutContent? Model { get => ((LayoutDocumentControl)this).Model; set => ((LayoutDocumentControl)this).Model = value; }
        protected override void OnPreviewGotKeyboardFocus(DockKeyboardFocusChangedEventArgs e)
        { Previews++; DuringPreview?.Invoke(); if (Cancel) e.Cancel = true; base.OnPreviewGotKeyboardFocus(e); }
        protected override void OnGotKeyboardFocus(DockKeyboardFocusChangedEventArgs e)
        { Completed++; OldFocus = e.OldFocus; NewFocus = e.NewFocus; if (Suppress) e.Handled = true; base.OnGotKeyboardFocus(e); }
    }
    private sealed class FactoryProbe : DockingManager
    {
        public int DocumentPanes, Tabs;
        protected override LayoutDocumentPaneControl CreateDocumentPaneControl(LayoutDocumentPane model)
        { DocumentPanes++; return new FactoryPane(model, this); }
    }
    private sealed class FactoryPane(LayoutDocumentPane model, FactoryProbe owner) : LayoutDocumentPaneControl(model)
    {
        protected override LayoutTabItemBase CreateTabItem(LayoutContent model) { owner.Tabs++; return new InputProbe(); }
    }
    private sealed class SelectionProbe(LayoutDocumentPane pane) : LayoutDocumentPaneControl(pane)
    {
        public int Calls;
        protected override void OnSelectionChanged(SelectionChangedEventArgs e) { Calls++; base.OnSelectionChanged(e); }
    }
    private sealed class SourceProbe : DockingManager
    {
        public int Calls; public bool Suppress, Throw; public bool AllOnUi = true;
        protected override bool OnReceiveWeakEvent(Type managerType, object sender, EventArgs e)
        { Calls++; AllOnUi &= DispatcherQueue.HasThreadAccess; if (Throw) throw new InvalidOperationException("source hook"); return !Suppress && base.OnReceiveWeakEvent(managerType, sender, e); }
    }
    private sealed class InputProbe : LayoutDocumentTabItem
    {
        public int Previews, Downs, Ups, RightUps, MiddleDowns, Moves;
        public bool PreviewVeto, DownVeto, ReleaseVeto;
        protected override void OnPreviewMouseLeftButtonDown(DockMouseButtonEventArgs e) { Previews++; if (PreviewVeto) e.Handled = true; else base.OnPreviewMouseLeftButtonDown(e); }
        protected override void OnMouseLeftButtonDown(DockMouseButtonEventArgs e) { Downs++; if (!DownVeto) base.OnMouseLeftButtonDown(e); }
        protected override void OnMouseLeftButtonUp(DockMouseButtonEventArgs e) { Ups++; if (ReleaseVeto) e.Handled = true; else base.OnMouseLeftButtonUp(e); }
        protected override void OnMouseRightButtonUp(DockMouseButtonEventArgs e) { RightUps++; base.OnMouseRightButtonUp(e); }
        protected override void OnMouseDown(DockMouseButtonEventArgs e) { if (e.ChangedButton == DockMouseButton.Middle) MiddleDowns++; base.OnMouseDown(e); }
        protected override void OnMouseMove(DockMouseEventArgs e) { Moves++; base.OnMouseMove(e); }
    }
    [Microsoft.UI.Xaml.Data.Bindable]
    public sealed class SelectionSource : INotifyPropertyChanged
    {
        private int _index;
        public int Index { get => _index; set { if (_index == value) return; _index = value; PropertyChanged?.Invoke(this, new(nameof(Index))); } }
        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
