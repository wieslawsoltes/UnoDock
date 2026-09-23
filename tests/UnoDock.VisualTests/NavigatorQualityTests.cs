using System.Globalization;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Xml.Linq;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media;
using Xceed.Wpf.AvalonDock.Controls;
using Windows.Foundation;

namespace UnoDock.Testing;

/// <summary>Live selection, realized containers, real scrolling and public-reference chrome observations.</summary>
public static class NavigatorQualityTests
{
    public static async Task<int> Run(DockingManager host, string output)
    {
        var tests = new TestRunner();
        tests.Test("navigator public labels match original observations", () =>
        {
            var original = Reference("navigator").Root!;
            var nav = new NavigatorWindow(host);
            Check.Equal(original.Attribute("documentLabel")!.Value, nav.LayoutDocumentsLabel);
            Check.Equal(original.Attribute("toolLabel")!.Value, nav.LayoutAnchorablesLabel);
        });
        tests.Test("tool list preserves observed model order rather than document MRU sorting", async () =>
        {
            using var f = new Fixture(host); var nav = f.Show(); await Ready(nav);
            Check.Equal("Solution Explorer,Properties,Output", string.Join(',', nav.Anchorables.Select(a => a.Title)));
        });
        tests.Test("model adapters receive actual selectable visual containers", async () =>
        {
            using var f = new Fixture(host); var nav = f.Show(); await Ready(nav);
            foreach (var item in nav.Documents)
            {
                var row = List(nav, true).ContainerFromItem(item);
                Check.True(row is ListBoxItem { ActualHeight: > 0 }); Check.False(ReferenceEquals(item, row));
                Check.True(((FrameworkElement)row).FindVisualChildren<TextBlock>().Any(text => text.Text == item.Title));
            }
        });
        tests.Test("navigation preserves both public collections and ItemsSources", async () =>
        {
            using var f = new Fixture(host, 40); var nav = f.Show(); await Ready(nav);
            var documents = nav.Documents; var tools = nav.Anchorables;
            var documentSource = List(nav, true).ItemsSource; var toolSource = List(nav, false).ItemsSource;
            var row = List(nav, true).ContainerFromItem(nav.Documents[0]);
            for (var i = 0; i < 150; i++) Call(nav, "Advance", 1);
            await Tick(); Check.Same(documents, nav.Documents); Check.Same(tools, nav.Anchorables);
            Check.Same(documentSource, List(nav, true).ItemsSource); Check.Same(toolSource, List(nav, false).ItemsSource);
            Check.Same(row, List(nav, true).ContainerFromItem(nav.Documents[0]));
        });
        tests.Test("nonstructural model changes do not reset either list", async () =>
        {
            using var f = new Fixture(host, 10); var nav = f.Show(); await Ready(nav);
            var documents = nav.Documents; var tools = nav.Anchorables;
            f.Docs[0].Title = "Renamed"; f.Docs[0].Description = "Updated description";
            f.Docs[0].LastActivationTimeStamp = DateTime.UtcNow; host.Refresh(); await Tick();
            Check.Same(documents, nav.Documents); Check.Same(tools, nav.Anchorables);
            Check.Equal("Renamed", documents.Single(d => d.ContentId == f.Docs[0].ContentId).Title);
        });
        tests.Test("adding document preserves tool array and existing MRU prefix", async () =>
        {
            using var f = new Fixture(host); var nav = f.Show(); await Ready(nav);
            var prefix = nav.Documents; var tools = nav.Anchorables; var selected = nav.SelectedDocument;
            f.Pane.Children.Add(new LayoutDocument { ContentId = "added", Title = "Added", LastActivationTimeStamp = DateTime.MaxValue });
            await Until(() => nav.Documents.Length == 4); Check.Same(tools, nav.Anchorables);
            for (var i = 0; i < prefix.Length; i++) Check.Same(prefix[i], nav.Documents[i]);
            Check.Equal("added", nav.Documents[^1].ContentId); Check.Same(selected, nav.SelectedDocument);
        });
        tests.Test("adding tool preserves document array", async () =>
        {
            using var f = new Fixture(host); var nav = f.Show(); var documents = nav.Documents;
            f.Tools.Children.Add(new LayoutAnchorable() { ContentId = "more-tools", Title = "More" });
            await Until(() => nav.Anchorables.Count() == 4); Check.Same(documents, nav.Documents);
        });
        tests.Test("removal during queued refresh preserves the final selection", async () =>
        {
            using var f = new Fixture(host); var nav = f.Show(); await Ready(nav);
            nav.SelectedDocument = nav.Documents[0]; f.Pane.Children.Remove((LayoutDocument)nav.SelectedDocument.LayoutElement);
            var survivor = nav.Documents.Last(); nav.SelectedDocument = survivor;
            await Until(() => nav.Documents.Length == 2); Check.Same(survivor, nav.SelectedDocument);
        });
        tests.Test("title and description update without rebuilding selected row", async () =>
        {
            using var f = new Fixture(host); var nav = f.Show(); await Ready(nav); nav.SelectedDocument = nav.Documents[0];
            var row = List(nav, true).ContainerFromItem(nav.SelectedDocument); var model = (LayoutDocument)nav.SelectedDocument.LayoutElement;
            model.Title = "Changed title"; model.Description = "Changed description"; await Tick();
            Check.Equal("Changed title", Field<TextBlock>(nav, "_selectionTitle").Text);
            Check.Equal("Changed description", Field<TextBlock>(nav, "_selectionDescription").Text);
            Check.Same(row, List(nav, true).ContainerFromItem(nav.SelectedDocument));
        });
        tests.Test("selecting a tool clears a previous document description", async () =>
        {
            using var f = new Fixture(host); var nav = f.Show(); await Ready(nav); nav.SelectedDocument = nav.Documents[0];
            Check.True(Field<TextBlock>(nav, "_selectionDescription").Text.Length > 0);
            nav.SelectedAnchorable = nav.Anchorables.First(); Check.Equal("", Field<TextBlock>(nav, "_selectionDescription").Text);
        });
        tests.Test("all items removed gives coherent empty selection", async () =>
        {
            using var f = new Fixture(host); var nav = f.Show(); await Ready(nav);
            f.Pane.Children.Clear(); f.Tools.Children.Clear(); await Until(() => nav.Documents.Length == 0 && !nav.Anchorables.Any());
            Check.True(nav.SelectedDocument == null && nav.SelectedAnchorable == null);
            Check.True(List(nav, true).SelectedItem == null && List(nav, false).SelectedItem == null);
        });
        tests.Test("empty category navigation retains the valid other selection", async () =>
        {
            using var f = new Fixture(host); f.Tools.Children.Clear(); var nav = f.Show(); await Ready(nav);
            var selected = nav.SelectedDocument; Call(nav, "SelectGroup", false); Check.Same(selected, nav.SelectedDocument);
        });
        tests.Test("group boundaries stay within selected category", async () =>
        {
            using var f = new Fixture(host); var nav = f.Show(); await Ready(nav);
            nav.SelectedDocument = nav.Documents[1]; Call(nav, "SelectBoundary", true); Check.Same(nav.Documents[^1], nav.SelectedDocument);
            Call(nav, "SelectBoundary", false); Check.Same(nav.Documents[0], nav.SelectedDocument);
            nav.SelectedAnchorable = nav.Anchorables.First(); Call(nav, "SelectBoundary", true); Check.Same(nav.Anchorables.Last(), nav.SelectedAnchorable);
        });
        tests.Test("category switch restores its last explicitly selected item", async () =>
        {
            using var f = new Fixture(host); var nav = f.Show(); await Ready(nav);
            var doc = nav.Documents[^1]; var tool = nav.Anchorables.Last(); nav.SelectedDocument = doc; nav.SelectedAnchorable = tool;
            Call(nav, "SelectGroup", true); Check.Same(doc, nav.SelectedDocument);
            Call(nav, "SelectGroup", false); Check.Same(tool, nav.SelectedAnchorable);
        });
        tests.Test("selection callback redirection converges before returning", async () =>
        {
            using var f = new Fixture(host); var nav = f.Show(); await Ready(nav);
            nav.SelectedDocument = nav.Documents[0]; var target = nav.Documents[1]; var redirected = nav.Documents[2];
            var token = nav.RegisterPropertyChangedCallback(NavigatorWindow.SelectedDocumentProperty, (_, _) =>
            { if (ReferenceEquals(nav.SelectedDocument, target)) nav.SelectedDocument = redirected; });
            try { nav.SelectedDocument = target; Check.Same(redirected, nav.SelectedDocument); Check.Same(redirected, List(nav, true).SelectedItem); }
            finally { nav.UnregisterPropertyChangedCallback(NavigatorWindow.SelectedDocumentProperty, token); }
        });
        tests.Test("published selection callback can switch category", async () =>
        {
            using var f = new Fixture(host); var nav = f.Show(); await Ready(nav); nav.SelectedDocument = nav.Documents[0];
            var tool = nav.Anchorables.Last(); var target = nav.Documents[1];
            var token = nav.RegisterPropertyChangedCallback(NavigatorWindow.SelectedDocumentProperty, (_, _) =>
            { if (ReferenceEquals(nav.SelectedDocument, target)) nav.SelectedAnchorable = tool; });
            try { nav.SelectedDocument = target; Check.Same(tool, nav.SelectedAnchorable); Check.True(nav.SelectedDocument == null); Check.Same(tool, List(nav, false).SelectedItem); }
            finally { nav.UnregisterPropertyChangedCallback(NavigatorWindow.SelectedDocumentProperty, token); }
        });
        tests.Test("throwing selection observer does not poison the next request", async () =>
        {
            using var f = new Fixture(host); var nav = f.Show(); await Ready(nav); nav.SelectedDocument = nav.Documents[0];
            var token = nav.RegisterPropertyChangedCallback(NavigatorWindow.SelectedDocumentProperty, (_, _) => throw new InvalidOperationException("test observer"));
            try { Check.Throws<InvalidOperationException>(() => nav.SelectedDocument = nav.Documents[1]); }
            finally { nav.UnregisterPropertyChangedCallback(NavigatorWindow.SelectedDocumentProperty, token); }
            nav.SelectedDocument = nav.Documents[2]; Check.Same(nav.Documents[2], List(nav, true).SelectedItem); Check.True(nav.SelectedAnchorable == null);
        });
        tests.Test("list-publication callback cannot initialize a replaced layout", async () =>
        {
            using var f = new Fixture(host); var nav = new NavigatorWindow(host);
            var token = nav.RegisterPropertyChangedCallback(NavigatorWindow.DocumentsProperty, (_, _) =>
                host.Layout = new() { RootPanel = new(new LayoutDocumentPane(new LayoutDocument { Title = "Replacement" })) });
            try { Call(nav, "Initialize"); Check.True(Field<object?>(nav, "_sessionRoot") == null); }
            finally { nav.UnregisterPropertyChangedCallback(NavigatorWindow.DocumentsProperty, token); Call(nav, "EndSession"); }
        });
        tests.Test("far document selection scrolls a realized row into view", async () =>
        {
            using var f = new Fixture(host, 100); var nav = f.Show(); await Ready(nav); nav.SelectedDocument = nav.Documents[^1];
            await Until(() => FullyVisible(List(nav, true), nav.SelectedDocument!));
            Check.True(Scroll(List(nav, true)).VerticalOffset > 0);
        });
        tests.Test("rapid selection discards stale reveal requests", async () =>
        {
            using var f = new Fixture(host, 80); var nav = f.Show(); await Ready(nav);
            nav.SelectedDocument = nav.Documents[^1]; nav.SelectedDocument = nav.Documents[20]; nav.SelectedDocument = nav.Documents[0];
            await Until(() => FullyVisible(List(nav, true), nav.Documents[0])); await Tick(); Check.Near(0, Scroll(List(nav, true)).VerticalOffset, 1);
        });
        tests.Test("navigation to first row scrolls back from the bottom", async () =>
        {
            using var f = new Fixture(host, 60); var nav = f.Show(); await Ready(nav); nav.SelectedDocument = nav.Documents[^1];
            await Until(() => FullyVisible(List(nav, true), nav.SelectedDocument!)); Call(nav, "SelectBoundary", false);
            await Until(() => FullyVisible(List(nav, true), nav.Documents[0])); Check.Near(0, Scroll(List(nav, true)).VerticalOffset, 1);
        });
        tests.Test("unrelated refresh does not undo a user's scroll offset", async () =>
        {
            using var f = new Fixture(host, 60); var nav = f.Show(); await Ready(nav); nav.SelectedDocument = nav.Documents[0]; await Tick();
            var scroll = Scroll(List(nav, true)); scroll.ChangeView(null, 180, null, true); await Until(() => Math.Abs(scroll.VerticalOffset - 180) < 1);
            f.Docs[0].Description = "No selection change"; host.Refresh(); await Tick(); Check.Near(180, scroll.VerticalOffset, 1);
        });
        tests.Test("ending session cancels pending reveal before visual removal", async () =>
        {
            using var f = new Fixture(host, 60); var nav = f.Show(); await Ready(nav);
            var scroll = Scroll(List(nav, true)); var offset = scroll.VerticalOffset;
            nav.SelectedDocument = nav.Documents[^1]; Call(nav, "EndSession"); await Tick(); Check.Near(offset, scroll.VerticalOffset, 1);
        });
        tests.Test("disabled pending target is filtered before reveal or commit", async () =>
        {
            using var f = new Fixture(host, 40); var nav = f.Show(); await Ready(nav); var target = nav.Documents[^1];
            nav.SelectedDocument = target; target.LayoutElement.IsEnabled = false; await Until(() => !nav.Documents.Contains(target));
            Check.False(ReferenceEquals(target, nav.SelectedDocument)); Call(nav, "CommitSelection"); Check.False(ReferenceEquals(target.LayoutElement, host.Layout.ActiveContent));
        });
        tests.Test("custom named ListBox parts retain their own templates", async () =>
        {
            using var f = new Fixture(host, 12); var nav = new NavigatorWindow(host) { Template = CustomTemplate() };
            f.Show(nav); await Ready(nav); var list = List(nav, true); var template = list.Template;
            Check.False(ReferenceEquals(list, Field<ListBox>(nav, "_defaultDocuments")));
            nav.SelectedDocument = nav.Documents[^1]; host.Refresh(); await Tick(); Check.Same(template, list.Template);
            Check.Same(nav.SelectedDocument, list.SelectedItem);
        });
        tests.Test("old template list no longer drives selection after replacement", async () =>
        {
            using var f = new Fixture(host); var nav = new NavigatorWindow(host) { Template = CustomTemplate() };
            f.Show(nav); await Ready(nav); var old = List(nav, true); nav.Template = CustomTemplate(); nav.ApplyTemplate(); await Ready(nav);
            Check.False(ReferenceEquals(old, List(nav, true))); nav.SelectedDocument = nav.Documents[0]; old.SelectedItem = nav.Documents[2];
            Check.Same(nav.Documents[0], nav.SelectedDocument);
        });
        tests.Test("navigator theme update retains rows and both collections", async () =>
        {
            using var f = new Fixture(host); var nav = f.Show(); await Ready(nav); var source = nav.Documents; var row = List(nav, true).ContainerFromItem(source[0]);
            var previous = nav.Background; host.RequestedTheme = ElementTheme.Dark; host.Refresh(); await Tick();
            Check.Same(source, nav.Documents); Check.Same(row, List(nav, true).ContainerFromItem(source[0])); Check.False(ReferenceEquals(previous, nav.Background));
        });
        tests.Test("navigator color override is applied without resetting ItemsSource", async () =>
        {
            using var f = new Fixture(host); var nav = f.Show(); await Ready(nav); var source = List(nav, true).ItemsSource;
            var brush = new SolidColorBrush(Microsoft.UI.Colors.AliceBlue); host.Resources["UnoDock.NavigatorBrush"] = brush;
            try { host.Refresh(); await Tick(); Check.Same(brush, nav.Background); Check.Same(source, List(nav, true).ItemsSource); }
            finally { host.Resources.Remove("UnoDock.NavigatorBrush"); }
        });
        tests.Test("labels are live and accessible", async () =>
        {
            using var f = new Fixture(host); var nav = f.Show(); await Ready(nav);
            nav.LayoutDocumentsLabel = "Files in workspace"; nav.LayoutAnchorablesLabel = "Tool windows"; await Tick();
            Check.Equal("Files in workspace", Field<TextBlock>(nav, "_documentHeading").Text);
            Check.Equal("Tool windows", AutomationProperties.GetName(List(nav, false)));
        });
        tests.Test("larger font density remeasures rows and separates both detail lines", async () =>
        {
            using var f = new Fixture(host); var nav = f.Show(); await Ready(nav); nav.SelectedDocument = nav.Documents[0];
            host.Resources["UnoDock.FontSize"] = 20d;
            try
            {
                host.Refresh(); await Tick(); nav.UpdateLayout();
                var row = (ListBoxItem)List(nav, true).ContainerFromItem(nav.Documents[0]);
                Check.True(row.ActualHeight >= 34); Check.Near(20, row.FontSize);
                var title = Bounds(Field<TextBlock>(nav, "_selectionTitle"), nav);
                var description = Bounds(Field<TextBlock>(nav, "_selectionDescription"), nav);
                Check.True(title.Bottom <= description.Top); Check.True(description.Bottom <= Bounds(List(nav, true), nav).Top);
            }
            finally { host.Resources.Remove("UnoDock.FontSize"); host.Refresh(); }
        });
        tests.Test("long details do not widen the list-sized popup", async () =>
        {
            using var f = new Fixture(host); var nav = f.Show(); await Ready(nav); nav.SelectedDocument = nav.Documents[0]; await Tick(); var width = nav.ActualWidth;
            ((LayoutDocument)nav.SelectedDocument!.LayoutElement).Description = new string('W', 800); await Tick(); nav.UpdateLayout();
            Check.Near(width, nav.ActualWidth, 1); Check.True(nav.ActualWidth < 500);
        });
        tests.Test("details are non-overlapping within the original compact band", async () =>
        {
            using var f = new Fixture(host); var nav = f.Show(); await Ready(nav);
            var title = Bounds(Field<TextBlock>(nav, "_selectionTitle"), nav); var description = Bounds(Field<TextBlock>(nav, "_selectionDescription"), nav);
            Check.True(title.Bottom <= description.Top); Check.True(description.Bottom <= Bounds(List(nav, true), nav).Top);
            Check.Near(3, nav.BorderThickness.Left); Check.Near(5, nav.Padding.Left);
            Check.Near(24, ((FrameworkElement)List(nav, true).ContainerFromItem(nav.Documents[0])).ActualHeight, 0.1);
            Check.Near(23.96, ((FrameworkElement)List(nav, true).ContainerFromItem(nav.Documents[0])).ActualHeight, 1);
        });
        foreach (var scenario in new[] { "navigator", "navigator-tool", "navigator-many", "navigator-rtl", "navigator-dark" })
            tests.Test("capture live navigator scene: " + scenario, async () =>
            {
                using var f = new Fixture(host, scenario == "navigator-many" ? 40 : 3);
                var nav = new NavigatorWindow(host) { FlowDirection = scenario == "navigator-rtl" ? FlowDirection.RightToLeft : FlowDirection.LeftToRight };
                if (scenario == "navigator-dark") host.RequestedTheme = ElementTheme.Dark;
                f.Show(nav); await Ready(nav);
                if (scenario == "navigator-tool") nav.SelectedAnchorable = nav.Anchorables.First(); else nav.SelectedDocument = nav.Documents.Last();
                await Tick(); nav.UpdateLayout();
                var path = Path.Combine(output, "visuals", scenario);
                await VisualCapture.Save(nav, path + ".png"); SaveGeometry(nav, path + ".xml");
                Check.True(nav.ActualWidth < 620 && nav.ActualHeight <= 560);
            });
        if (OperatingSystem.IsLinux() && Environment.GetEnvironmentVariable("UNODOCK_NATIVE_INPUT_TESTS") == "1")
        {
            tests.Test("XTEST End and Home reveal actual navigator rows", async () =>
            {
                using var f = new Fixture(host, 60); var nav = f.Show(); await Ready(nav); nav.SelectedDocument = nav.Documents[0]; nav.Focus(FocusState.Programmatic);
                using var input = new X11TestInput(); input.KeyPress(0xff57); // End
                await Until(() => ReferenceEquals(nav.SelectedDocument, nav.Documents[^1]) && FullyVisible(List(nav, true), nav.Documents[^1]));
                input.KeyPress(0xff50); // Home
                await Until(() => ReferenceEquals(nav.SelectedDocument, nav.Documents[0]) && FullyVisible(List(nav, true), nav.Documents[0]));
            });
            tests.Test("XTEST clicking a realized row commits that item not the previous selection", async () =>
            {
                using var f = new Fixture(host); var nav = f.Show(); await Ready(nav); nav.SelectedDocument = nav.Documents[0];
                var target = nav.Documents[^1]; var row = (FrameworkElement)List(nav, true).ContainerFromItem(target);
                using var input = new X11TestInput(); input.MoveTo(row, new(row.ActualWidth / 2, row.ActualHeight / 2)); await Tick();
                input.Press(); await Task.Delay(50); input.Release(); await Until(() => ReferenceEquals(host.Layout.ActiveContent, target.LayoutElement));
            });
        }
        return await tests.Run(output, "navigator-quality");
    }
    private sealed class Fixture : IDisposable
    {
        private readonly DockingManager _host; private readonly LayoutRoot _root; private readonly ElementTheme _theme;
        internal LayoutDocument[] Docs { get; }
        internal LayoutDocumentPane Pane { get; } = new();
        internal LayoutAnchorablePane Tools { get; } = new();
        internal Fixture(DockingManager host, int count = 3)
        {
            _host = host; _root = host.Layout; _theme = host.RequestedTheme; host.RequestedTheme = ElementTheme.Light;
            Docs = Enumerable.Range(0, count).Select(i => new LayoutDocument { ContentId = "document-" + i, Title = $"Document {i:D2}.cs",
                Description = $"Project / Source / Document {i:D2}.cs", Content = new TextBox { Text = "Owned probe document " + i } }).ToArray();
            foreach (var doc in Docs) Pane.Children.Add(doc);
            foreach (var title in new[] { "Solution Explorer", "Properties", "Output" }) Tools.Children.Add(new() { ContentId = title, Title = title, Content = new TextBox() });
            var panel = new Xceed.Wpf.AvalonDock.Layout.LayoutPanel(Tools); panel.Children.Add(Pane); host.Layout = new() { RootPanel = panel };
            Docs[0].IsActive = true; var time = new DateTime(2000, 1, 1); var i = 0;
            foreach (var c in host.Layout.Descendents().OfType<LayoutContent>()) c.LastActivationTimeStamp = time.AddSeconds(i++);
            host.Refresh(); host.UpdateLayout();
            var owner = Uno.UI.ApplicationHelper.Windows.FirstOrDefault(w => ReferenceEquals(w.Content?.XamlRoot, host.XamlRoot)); owner?.Activate();
        }
        internal NavigatorWindow Show(NavigatorWindow? nav = null)
        { nav ??= new(_host); Surface(_host, "ShowNavigator", nav); return nav; }
        public void Dispose() { Surface(_host, "CloseNavigator", false); _host.Layout = _root; _host.RequestedTheme = _theme; _host.Refresh(); }
    }
    private static async Task Ready(NavigatorWindow nav)
    { await Until(() => nav.ActualHeight > 0 && (nav.Documents.Length == 0 || List(nav, true).ContainerFromItem(nav.Documents[0]) is FrameworkElement { ActualHeight: > 0 })); await Tick(); }
    private static async Task Until(Func<bool> condition)
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        while (!condition() && watch.Elapsed < TimeSpan.FromSeconds(4)) await Task.Delay(16);
        Check.True(condition(), "Asynchronous navigator condition did not complete.");
    }
    private static Task Tick() => Task.Delay(100);
    private static ListBox List(NavigatorWindow nav, bool documents) => Field<ListBox>(nav, documents ? "_documentsList" : "_anchorablesList");
    private static T Field<T>(object instance, string name) => (T)instance.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(instance)!;
    private static ScrollViewer Scroll(ListBox list) => list.FindVisualChildren<ScrollViewer>().First();
    private static Rect Bounds(FrameworkElement element, UIElement relative) => element.TransformToVisual(relative).TransformBounds(new(0, 0, element.ActualWidth, element.ActualHeight));
    private static bool FullyVisible(ListBox list, LayoutItem item)
    {
        if (list.ContainerFromItem(item) is not FrameworkElement row || row.ActualHeight <= 0) return false;
        var scroll = Scroll(list); var rect = Bounds(row, scroll); return rect.Top >= -1 && rect.Bottom <= scroll.ViewportHeight + 2;
    }
    private static XDocument Reference(string name)
    { using var stream = typeof(NavigatorQualityTests).Assembly.GetManifestResourceStream("VisualFixtures." + name + ".xml")!; return XDocument.Load(stream); }
    private static ControlTemplate CustomTemplate() => (ControlTemplate)XamlReader.Load("""
        <ControlTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" xmlns:dock="using:Xceed.Wpf.AvalonDock.Controls">
          <StackPanel Width="320"><dock:NavigatorListBox x:Name="PART_DocumentListBox" Height="140"/><dock:NavigatorListBox x:Name="PART_AnchorableListBox" Height="100"/></StackPanel>
        </ControlTemplate>
        """);
    private static void Surface(DockingManager host, string name, params object[] args)
    { var surface = typeof(DockingManager).GetProperty("Surface", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(host)!; Call(surface, name, args); }
    private static object? Call(object target, string name, params object[] args)
    {
        try { return target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.Invoke(target, args); }
        catch (TargetInvocationException e) when (e.InnerException != null) { ExceptionDispatchInfo.Capture(e.InnerException).Throw(); throw; }
    }
    private static void SaveGeometry(NavigatorWindow nav, string path)
    {
        var root = new XElement("Navigator", new XAttribute("width", nav.ActualWidth), new XAttribute("height", nav.ActualHeight));
        foreach (var element in nav.FindVisualChildren<FrameworkElement>().Prepend(nav))
        {
            var bounds = Bounds(element, nav);
            var row = new XElement("Element", new XAttribute("type", element.GetType().Name), new XAttribute("name", element.Name ?? ""),
                new XAttribute("x", bounds.X.ToString("R", CultureInfo.InvariantCulture)), new XAttribute("y", bounds.Y.ToString("R", CultureInfo.InvariantCulture)),
                new XAttribute("width", bounds.Width.ToString("R", CultureInfo.InvariantCulture)), new XAttribute("height", bounds.Height.ToString("R", CultureInfo.InvariantCulture)));
            if (element is TextBlock text) row.SetAttributeValue("text", text.Text);
            root.Add(row);
        }
        new XDocument(root).Save(path);
    }
}
