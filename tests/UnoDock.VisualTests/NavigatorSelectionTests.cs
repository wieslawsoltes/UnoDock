using System.ComponentModel;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Windows.Input;
using System.Xml.Linq;
using UnoDock.Controls;
using UnoDock.Layout;

namespace UnoDock.Testing;

internal static class NavigatorSelectionTests
{
    internal static void Register(TestRunner tests, DockingManager host, string output)
    {
        using var resource = typeof(NavigatorSelectionTests).Assembly.GetManifestResourceStream("VisualFixtures.navigator-selection-observations.xml")
            ?? throw new InvalidOperationException("Missing original navigator setter observations.");
        var reference = XDocument.Load(resource).Root!;
        var cases = reference.Elements("Case").ToArray();
        tests.Test("direct selection: complete original 28-case observation matrix is retained", () => Check.Equal(28, cases.Length));
        foreach (var observation in cases)
        {
            var name = observation.Attribute("name")!.Value;
            // Keep all raw observations. These four cases are explicit boundaries,
            // not silently edited reference results or relaxed comparator rules.
            if (name.EndsWith("/disabled", StringComparison.Ordinal) || name is "document/query-reselect" or "document/execute-reselect") continue;
            var tool = name.StartsWith("tool/", StringComparison.Ordinal);
            var scenario = name[(name.IndexOf('/') + 1)..];
            tests.Test("original direct-selection state and event replay: " + name, async () =>
            {
                using var f = new Fixture(host); await f.Show();
                var original = f.Target(tool).ActivateCommand!;
                if (scenario is "veto" or "noop" or "query-reselect" or "execute-reselect" or "query-throws" or "execute-throws")
                {
                    f.Target(tool).ActivateCommand = new Command(() =>
                    {
                        f.Trace.Add("query");
                        if (scenario == "query-throws") throw new ApplicationException("Observed query failure");
                        if (scenario == "query-reselect") { f.Trace.Add("query-reselect"); f.Nav.SelectedDocument = f.Document(1); }
                        return scenario != "veto";
                    }, () =>
                    {
                        f.Trace.Add("execute");
                        if (scenario == "execute-throws") throw new ApplicationException("Observed execution failure");
                        if (scenario == "execute-reselect") { f.Trace.Add("execute-reselect"); f.Nav.SelectedDocument = f.Document(1); }
                        else if (scenario != "noop") original.Execute(null);
                    });
                }
                CancelEventHandler closing = (_, e) =>
                {
                    if (scenario == "closing-veto") { e.Cancel = true; f.Trace.Add("cancel-close"); }
                    if (scenario == "closing-reselect") { f.Trace.Add("closing-reselect"); f.Nav.SelectedDocument = f.Document(1); }
                };
                f.Nav.Closing += closing;
                try
                {
                    AssertState(observation.Element("State")!, f);
                    f.Recording = true;
                    foreach (var step in observation.Elements("Step"))
                    {
                        f.Trace.Clear(); Exception? error = null;
                        try
                        {
                            if (step.Attribute("name")!.Value == "same-again") f.Assign(tool);
                            else if (scenario == "null")
                            { if (tool) f.Nav.SelectedAnchorable = null; else f.Nav.SelectedDocument = null; }
                            else if (scenario == "same")
                            { if (tool) f.Nav.SelectedAnchorable = f.Nav.SelectedAnchorable; else f.Nav.SelectedDocument = f.Nav.SelectedDocument; }
                            else if (scenario == "setvalue") f.Nav.SetValue(tool ? NavigatorWindow.SelectedAnchorableProperty : NavigatorWindow.SelectedDocumentProperty, f.Target(tool));
                            else f.Assign(tool);
                        }
                        catch (Exception caught) { error = caught; }
                        Check.Equal(step.Attribute("exception")!.Value, error?.GetType().FullName ?? "none");
                        Check.Equal(string.Join('|', step.Elements("Event").Select(e => e.Value)), string.Join('|', f.Trace));
                        AssertState(step.Element("State")!, f);
                    }
                    f.Recording = false; host.UpdateLayout(); await Task.Delay(30);
                    AssertState(observation.Elements("State").Last(), f);
                }
                finally { f.Nav.Closing -= closing; }
            });
        }
        foreach (var tool in new[] { false, true })
        {
            var category = tool ? "tool" : "document";
            tests.Test("direct selection: explicit preview does not query or execute: " + category, async () =>
            {
                using var f = new Fixture(host); await f.Show();
                var calls = 0; f.Target(tool).ActivateCommand = new Command(() => { calls++; return true; }, () => calls++);
                if (tool) f.Nav.PreviewAnchorable((LayoutAnchorableItem)f.Target(tool)); else f.Nav.PreviewDocument((LayoutDocumentItem)f.Target(tool));
                Check.Equal(0, calls); Check.True(f.Visible); Check.Same(f.Docs[0], host.Layout.ActiveContent);
                Check.Same(f.Target(tool), tool ? f.Nav.SelectedAnchorable : f.Nav.SelectedDocument as LayoutItem);
                Check.True(tool ? f.Nav.SelectedDocument == null : f.Nav.SelectedAnchorable == null);
            });
            tests.Test("direct selection: disabled target stays inert instead of reproducing unsafe original activation: " + category, async () =>
            {
                using var f = new Fixture(host); await f.Show(); var calls = 0;
                f.Target(tool).ActivateCommand = new Command(() => true, () => calls++);
                f.Target(tool).LayoutElement.IsEnabled = false; f.Assign(tool);
                Check.Equal(0, calls); Check.True(f.Visible); Check.Same(f.Docs[0], host.Layout.ActiveContent);
            });
            foreach (var mutation in new[] { "command-ABA", "enabled-ABA", "parent-ABA", "root-replaced", "root-ABA", "cancel", "replacement-navigator" })
                tests.Test("direct selection: rejects CanExecute ownership change: " + category + "/" + mutation, async () =>
                {
                    using var f = new Fixture(host); await f.Show(); var calls = 0; NavigatorWindow? replacement = null;
                    ICommand? command = null;
                    command = new Command(() =>
                    {
                        var target = f.Target(tool);
                        switch (mutation)
                        {
                            case "command-ABA": target.ActivateCommand = new Command(() => true, () => { }); target.ActivateCommand = command; break;
                            case "enabled-ABA": target.LayoutElement.IsEnabled = false; target.LayoutElement.IsEnabled = true; break;
                            case "parent-ABA":
                                if (tool) { f.Tools.Children.Remove(f.ToolModels[2]); f.Tools.Children.Add(f.ToolModels[2]); }
                                else { f.Pane.Children.Remove(f.Docs[2]); f.Pane.Children.Add(f.Docs[2]); }
                                break;
                            case "root-replaced": host.Layout = new(); break;
                            case "root-ABA": var root = host.Layout; host.Layout = new(); host.Layout = root; break;
                            case "cancel": Surface(host, "CloseNavigator", false); break;
                            case "replacement-navigator": Surface(host, "CloseNavigator", false); replacement = new(host); Surface(host, "ShowNavigator", replacement); break;
                        }
                        return true;
                    }, () => calls++);
                    f.Target(tool).ActivateCommand = command; f.Assign(tool);
                    Check.Equal(0, calls);
                    if (replacement != null) Check.True((bool)Surface(host, "OwnsNavigator", replacement)!);
                });
            tests.Test("direct selection: a title update during query does not invalidate activation: " + category, async () =>
            {
                using var f = new Fixture(host); await f.Show(); var count = 0;
                f.Target(tool).ActivateCommand = new Command(() => { f.Target(tool).LayoutElement.Title = "renamed"; return true; }, () => count++);
                f.Assign(tool); Check.Equal(1, count); Check.False(f.Visible);
            });
            tests.Test("direct selection: repeated identical assignment does not requery a veto: " + category, async () =>
            {
                using var f = new Fixture(host); await f.Show(); var count = 0;
                f.Target(tool).ActivateCommand = new Command(() => { count++; return false; }, () => throw new InvalidOperationException("Veto was ignored."));
                f.Assign(tool); f.Assign(tool); Check.Equal(1, count); Check.True(f.Visible);
            });
            tests.Test("direct selection: query exception releases guards for a different subsequent selection: " + category, async () =>
            {
                using var f = new Fixture(host); await f.Show(); var failure = new ApplicationException("one query");
                f.Target(tool).ActivateCommand = new Command(() => throw failure, () => { });
                Exception? observed = null; try { f.Assign(tool); } catch (Exception e) { observed = e; }
                Check.Same(failure, observed); Check.True(f.Visible);
                f.Nav.PreviewDocument(f.Document(1));
                var executed = 0; f.Target(tool).ActivateCommand = new Command(() => true, () => executed++);
                f.Assign(tool); Check.Equal(1, executed); Check.False(f.Visible);
            });
        }
        tests.Test("direct selection: tool Closed callback cannot activate into a replacement navigator", async () =>
        {
            using var f = new Fixture(host); await f.Show(); var executed = 0; NavigatorWindow? replacement = null;
            f.Target(true).ActivateCommand = new Command(() => true, () => executed++);
            f.Nav.Closed += (_, _) => { replacement = new(host); Surface(host, "ShowNavigator", replacement); };
            f.Assign(true); Check.Equal(0, executed); Check.True(replacement != null);
            Check.True((bool)Surface(host, "OwnsNavigator", replacement!)!);
        });
        tests.Test("direct selection: document hide does not close lifecycle and the same view can reopen", async () =>
        {
            using var f = new Fixture(host); await f.Show(); f.Assign(false);
            Check.False(f.Visible); Check.False(f.Closed);
            Surface(host, "ShowNavigator", f.Nav); await Ready(f.Nav);
            Check.True(f.Visible); Check.False(f.Closed);
        });
        tests.Test("direct selection: tool close is terminal for that navigator instance", async () =>
        {
            using var f = new Fixture(host); await f.Show(); f.Assign(true); Check.True(f.Closed);
            Check.Throws<InvalidOperationException>(() => Surface(host, "ShowNavigator", f.Nav));
        });
        tests.Test("direct selection: closing handler exception leaves a usable live session", async () =>
        {
            using var f = new Fixture(host); await f.Show(); var failure = new ApplicationException("closing");
            CancelEventHandler handler = (_, _) => throw failure;
            f.Nav.Closing += handler; Exception? observed = null;
            try { f.Assign(true); } catch (Exception e) { observed = e; } finally { f.Nav.Closing -= handler; }
            Check.Same(failure, observed); Check.True(f.Visible); Check.False(f.Closed);
            f.Nav.PreviewDocument(f.Document(1)); f.Assign(true); Check.True(f.Closed);
        });
        tests.Test("direct selection: queued query reselect chooses the new document without executing stale command", async () =>
        {
            using var f = new Fixture(host); await f.Show(); var executed = 0;
            f.Target(false).ActivateCommand = new Command(() => { f.Nav.SelectedDocument = f.Document(1); return true; }, () => executed++);
            f.Assign(false); Check.Equal(0, executed); Check.Same(f.Docs[1], host.Layout.ActiveContent); Check.False(f.Visible);
        });
        tests.Test("direct selection: explicit document request after detach is inert", async () =>
        {
            using var f = new Fixture(host); await f.Show(); var executed = 0;
            f.Document(1).ActivateCommand = new Command(() => true, () => executed++);
            f.Target(false).ActivateCommand = new Command(() => true, () => f.Nav.SelectedDocument = f.Document(1));
            f.Assign(false); Check.Equal(0, executed); Check.False(f.Visible);
        });
        tests.Test("direct selection: a derived property hook may veto by omitting its base implementation", async () =>
        {
            using var f = new Fixture(host); await f.Show(); f.Nav.BlockBase = true; f.Assign(false);
            Check.True(f.Visible); Check.Same(f.Docs[0], host.Layout.ActiveContent);
        });
        tests.Test("direct selection: a foreign adapter never executes in the local manager", async () =>
        {
            using var f = new Fixture(host); await f.Show(); using var other = new DockingManager();
            var model = new LayoutDocument { ContentId = "foreign", Title = "Foreign" };
            other.Layout.RootPanel.Children.Add(new LayoutDocumentPane(model)); var calls = 0;
            var item = (LayoutDocumentItem)other.GetLayoutItemFromModel(model); item.ActivateCommand = new Command(() => true, () => calls++);
            f.Nav.SelectedDocument = item; Check.Equal(0, calls); Check.True(f.Visible); Check.Same(f.Docs[0], host.Layout.ActiveContent);
        });
        foreach (var rtl in new[] { false, true })
            tests.Test("direct selection: command veto retains visible realized selection rows: rtl=" + rtl, async () =>
            {
                using var f = new Fixture(host); f.Nav.FlowDirection = rtl ? FlowDirection.RightToLeft : FlowDirection.LeftToRight; await f.Show();
                f.Target(false).ActivateCommand = new Command(() => false, () => { }); f.Assign(false);
                host.UpdateLayout(); await Task.Delay(60);
                var list = f.Nav.FindVisualChildren<ListBox>().Single(l => l.Name == "PART_DocumentListBox");
                var row = list.ContainerFromItem(f.Target(false)) as FrameworkElement;
                Check.True(row is { ActualWidth: > 0, ActualHeight: > 0 }); Check.Same(f.Target(false), list.SelectedItem); Check.True(f.Visible);
                Directory.CreateDirectory(Path.Combine(output, "visuals"));
                await UnoDock.VisualValidation.VisualCapture.Save(host, Path.Combine(output, "visuals", "navigator-direct-veto-" + (rtl ? "rtl" : "ltr") + ".png"));
            });
    }

    private static void AssertState(XElement expected, Fixture f)
    {
        Check.Equal(bool.Parse(expected.Attribute("visible")!.Value), f.Visible);
        Check.Equal(bool.Parse(expected.Attribute("closed")!.Value), f.Closed);
        Check.Equal(expected.Attribute("active")!.Value, Id(f.Host.Layout.ActiveContent));
        Check.Equal(expected.Attribute("document")!.Value, Id(f.Nav.SelectedDocument?.LayoutElement));
        Check.Equal(expected.Attribute("tool")!.Value, Id(f.Nav.SelectedAnchorable?.LayoutElement));
    }
    private static string Id(LayoutContent? model) => model?.ContentId ?? "null";
    private sealed class Fixture : IDisposable
    {
        internal readonly DockingManager Host;
        private readonly LayoutRoot _saved;
        internal readonly LayoutDocumentPane Pane = new();
        internal readonly LayoutAnchorablePane Tools = new();
        internal readonly LayoutDocument[] Docs;
        internal readonly LayoutAnchorable[] ToolModels;
        internal readonly ObservedNavigator Nav;
        internal readonly List<string> Trace = [];
        internal bool Recording, Closed;
        internal bool Visible => (bool)Surface(Host, "OwnsNavigator", Nav)!;
        internal Fixture(DockingManager host)
        {
            Host = host; _saved = host.Layout;
            Docs = Enumerable.Range(0, 3).Select(i => new LayoutDocument { ContentId = "d" + i, Title = "Document " + i, Content = new TextBox { Text = "Owned document " + i } }).ToArray();
            ToolModels = Enumerable.Range(0, 3).Select(i => new LayoutAnchorable { ContentId = "t" + i, Title = "Tool " + i, Content = new TextBox { Text = "Owned tool " + i } }).ToArray();
            foreach (var doc in Docs) Pane.Children.Add(doc);
            foreach (var tool in ToolModels) Tools.Children.Add(tool);
            var panel = new UnoDock.Layout.LayoutPanel(Tools); panel.Children.Add(Pane); host.Layout = new() { RootPanel = panel };
            Docs[0].IsActive = true; host.Refresh(); host.UpdateLayout();
            Nav = new(host, Log);
            host.ActiveContentChanged += Active;
            Nav.Closing += (_, _) => Log("closing"); Nav.Closed += (_, _) => { Closed = true; Log("closed"); };
        }
        internal async Task Show()
        {
            Surface(Host, "ShowNavigator", Nav); await Ready(Nav);
            // Replay the recorded initial selection explicitly. MRU initialization
            // is a separate contract and is not inferred from this setter probe.
            Nav.PreviewDocument(Document(1)); Host.UpdateLayout(); await Task.Delay(30);
        }
        internal LayoutDocumentItem Document(int index) => (LayoutDocumentItem)Host.GetLayoutItemFromModel(Docs[index]);
        internal LayoutItem Target(bool tool) => Host.GetLayoutItemFromModel(tool ? ToolModels[2] : Docs[2] as LayoutContent ?? throw new InvalidOperationException());
        internal void Assign(bool tool)
        { if (tool) Nav.SelectedAnchorable = (LayoutAnchorableItem)Target(true); else Nav.SelectedDocument = Document(2); }
        private void Active(object? sender, EventArgs e) => Log("active:" + Id(Host.Layout.ActiveContent));
        private void Log(string value) { if (Recording) Trace.Add(value); }
        public void Dispose()
        {
            Recording = false; Host.ActiveContentChanged -= Active;
            Surface(Host, "CloseNavigator", false); Host.Layout = _saved; Host.Refresh();
        }
    }
    private sealed class ObservedNavigator(DockingManager manager, Action<string> log) : NavigatorWindow(manager)
    {
        internal bool BlockBase;
        protected override void OnSelectedDocumentChanged(DependencyPropertyChangedEventArgs e)
        {
            log("document-enter:" + Id((e.NewValue as LayoutDocumentItem)?.LayoutElement));
            try { if (!BlockBase) base.OnSelectedDocumentChanged(e); } finally { log("document-exit"); }
        }
        protected override void OnSelectedAnchorableChanged(DependencyPropertyChangedEventArgs e)
        {
            log("tool-enter:" + Id((e.NewValue as LayoutAnchorableItem)?.LayoutElement));
            try { if (!BlockBase) base.OnSelectedAnchorableChanged(e); } finally { log("tool-exit"); }
        }
    }
    private sealed class Command(Func<bool> query, Action execute) : ICommand
    {
        public bool CanExecute(object? parameter) => query();
        public void Execute(object? parameter) => execute();
        public event EventHandler? CanExecuteChanged { add { } remove { } }
    }
    private static async Task Ready(NavigatorWindow nav)
    {
        for (var i = 0; i < 150 && (!nav.IsLoaded || nav.ActualHeight <= 0); i++) await Task.Delay(20);
        Check.True(nav.IsLoaded && nav.ActualHeight > 0, "Navigator did not realize.");
    }
    private static object? Surface(DockingManager host, string method, params object[] args)
    {
        var surface = typeof(DockingManager).GetProperty("Surface", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(host)!;
        try { return surface.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(surface, args); }
        catch (TargetInvocationException e) when (e.InnerException != null) { ExceptionDispatchInfo.Capture(e.InnerException).Throw(); throw; }
    }
}
