using System.Globalization;
using System.Reflection;
using System.Xml.Linq;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation.Provider;
using Microsoft.UI.Xaml.Markup;
using UnoDock.Controls;
using UnoDock.Themes;
using Windows.Foundation;

namespace UnoDock.Testing;

/// <summary>Public behavior and real arranged controls. Reference data contains observations, not source/templates.</summary>
public static class VisualParityTests
{
    public static async Task<int> Run(DockingManager host, string output)
    {
        var tests = new TestRunner();
        var images = Path.Combine(output, "visuals");
        tests.Test("capture live docked, active, auto-hide, RTL and dark scenes", () => VisualScene.Capture(host, images));
        foreach (var scene in new[] { "docked", "active-tool", "auto-hide", "rtl" })
            tests.Test("original arranged geometry: " + scene, () => CompareScene(scene, images));
        foreach (var scenario in new[] { "one", "two", "pin-one", "ineligible-sibling" })
            tests.Test("original per-item auto-hide sequence: " + scenario, () =>
            {
                using var manager = new DockingManager();
                var items = new[] { "first", "second", "third" }.Select(id => new LayoutAnchorable { ContentId = id, Title = id }).ToArray();
                var pane = new LayoutAnchorablePane(); foreach (var item in items) pane.Children.Add(item);
                manager.Layout = new() { RootPanel = new(pane) };
                if (scenario == "ineligible-sibling") items[1].CanAutoHide = false;
                items[0].ToggleAutoHide();
                if (scenario is "two" or "pin-one") items[1].ToggleAutoHide();
                if (scenario == "pin-one") items[0].ToggleAutoHide();
                var expected = Fixture("behavior-" + scenario).Root!;
                Check.Equal(expected.Element("Docked")!.Value, string.Join(',', pane.Children.Select(c => c.ContentId)));
                var groups = manager.Layout.Descendents().OfType<LayoutAnchorGroup>().ToArray();
                var referenceGroups = expected.Elements("Group").ToArray();
                Check.Equal(referenceGroups.Length, groups.Length);
                for (var i = 0; i < groups.Length; i++)
                {
                    Check.Equal(referenceGroups[i].Value, string.Join(',', groups[i].Children.Select(c => c.ContentId)));
                    Check.Same(pane, groups[i].PreviousContainer);
                }
                Check.True(items.All(c => ReferenceEquals(c.Root, manager.Layout)));
            });
        tests.Test("ineligible auto-hide target is unchanged", () =>
        {
            using var manager = new DockingManager(); var a = new LayoutAnchorable { CanAutoHide = false };
            var pane = new LayoutAnchorablePane(a); manager.Layout = new() { RootPanel = new(pane) };
            a.ToggleAutoHide(); Check.Same(pane, a.Parent); Check.False(a.IsAutoHidden);
        });
        // Use a separate rooted manager so tests do not rely on the gallery's previous state.
        using var dock = new DockingManager { Width = 1000, Height = 640, Template = host.Template, RequestedTheme = ElementTheme.Light };
        var window = new Window { Content = dock, Title = "UnoDock visual regression controls" };
        window.AppWindow.Resize(new() { Width = 1064, Height = 740 }); window.Activate();
        try
        {
            tests.Test("tool tab strip is below content; document strip is above", async () =>
            {
                await Reset();
                var doc = Document("editor"); var tool = Tool("output");
                var docTab = Tab(doc); var toolTab = Tab(tool);
                var editorBounds = Bounds((FrameworkElement)doc.Content!); var toolBounds = Bounds((FrameworkElement)tool.Content!);
                Check.True(Bounds(docTab).Bottom <= editorBounds.Top);
                Check.True(Bounds(toolTab).Top >= toolBounds.Bottom);
            });
            tests.Test("single tool hides tab strip and adding sibling retains editor", async () =>
            {
                await Reset(); var tool = Tool("properties"); var pane = (LayoutAnchorablePane)tool.Parent!;
                var presenter = dock.GetLayoutItemFromModel(tool).View;
                Check.False(dock.FindVisualChildren<LayoutTabItemBase>().Any(t => ReferenceEquals(t.Model, tool) && Visible(t)));
                pane.Children.Add(new() { Title = "Second", ContentId = "extra", Content = new TextBox() });
                await Settle(); Check.True(Visible(Tab(tool))); Check.Same(presenter, dock.GetLayoutItemFromModel(tool).View);
                pane.Children.RemoveAt(1); await Settle(); Check.False(dock.FindVisualChildren<LayoutTabItemBase>().Any(t => ReferenceEquals(t.Model, tool) && Visible(t))); Check.Same(presenter, dock.GetLayoutItemFromModel(tool).View);
            });
            tests.Test("only selected closable document displays a close button", async () =>
            {
                await Reset(); var first = Document("editor"); var next = Document("readme");
                Check.True(Visible(NamedButton(Tab(first), "Close tab"))); Check.False(Visible(NamedButton(Tab(next), "Close tab")));
                next.IsActive = true; await Settle();
                Check.False(Visible(NamedButton(Tab(first), "Close tab"))); Check.True(Visible(NamedButton(Tab(next), "Close tab")));
                next.CanClose = false; await Settle(); Check.False(Visible(NamedButton(Tab(next), "Close tab")));
            });
            tests.Test("caption capabilities update in place", async () =>
            {
                await Reset(); var tool = Tool("explorer"); var view = Pane(tool);
                var pin = NamedButton(view, "Auto-hide tool"); var close = NamedButton(view, "Hide or close tool");
                Check.True(Visible(pin)); Check.True(Visible(close)); tool.CanAutoHide = false; tool.CanHide = false; tool.CanClose = false;
                await Settle(); Check.False(Visible(pin)); Check.False(Visible(close)); Check.Same(view, Pane(tool));
            });
            tests.Test("caption close follows model cancellation rather than discarding view", async () =>
            {
                await Reset(); var tool = Tool("explorer"); var pane = tool.Parent;
                tool.Hiding += (_, e) => e.Cancel = true;
                Invoke(NamedButton(Pane(tool), "Hide or close tool")); await Settle(); Check.Same(pane, tool.Parent); Check.False(tool.IsHidden);
            });
            tests.Test("caption pin moves requested item and retains its sibling", async () =>
            {
                await Reset(); var a = Tool("explorer"); var b = Tool("toolbox"); var original = a.Parent;
                Invoke(NamedButton(Pane(a), "Auto-hide tool")); await Settle();
                Check.True(a.IsAutoHidden); Check.Same(original, b.Parent); Check.False(b.IsAutoHidden);
            });
            tests.Test("rail label reserves vertical layout size", async () =>
            {
                await Reset(); var a = Tool("explorer"); a.ToggleAutoHide(); await Settle();
                var anchor = dock.FindVisualChildren<LayoutAnchorControl>().Single(c => ReferenceEquals(c.Model, a));
                Check.True(anchor.ActualHeight > anchor.ActualWidth * 2); Check.Near(26, anchor.ActualWidth + anchor.Margin.Left + anchor.Margin.Right, 1);
                var side = dock.FindVisualChildren<LayoutAnchorSideControl>().Single(c => c.IsLeftSide); Check.Near(26, side.ActualWidth, 1);
            });
            tests.Test("auto-hide popup close honors cancellation and then hides the model", async () =>
            {
                await Reset(); var a = Tool("explorer"); a.ToggleAutoHide(); await Settle();
                var anchor = dock.FindVisualChildren<LayoutAnchorControl>().Single(c => ReferenceEquals(c.Model, a));
                Invoke(anchor.FindVisualChildren<Button>().Single()); await Settle();
                var popup = dock.FindVisualChildren<LayoutAutoHideWindowControl>().Single(c => ReferenceEquals(c.Model, a));
                EventHandler<CancelEventArgs> cancel = (_, e) => e.Cancel = true; a.Hiding += cancel;
                Invoke(NamedButton(popup, "Hide or close auto-hidden tool")); await Settle(); Check.True(a.IsAutoHidden); Check.True(Visible(popup));
                a.Hiding -= cancel; Invoke(NamedButton(popup, "Hide or close auto-hidden tool")); await Settle(); Check.True(a.IsHidden); Check.False(Visible(popup));
            });
            tests.Test("auto-hide popup pin restores the original pane and retained presenter", async () =>
            {
                await Reset(); var a = Tool("explorer"); var pane = a.Parent; var presenter = dock.GetLayoutItemFromModel(a).View;
                a.ToggleAutoHide(); await Settle();
                var anchor = dock.FindVisualChildren<LayoutAnchorControl>().Single(c => ReferenceEquals(c.Model, a));
                Invoke(anchor.FindVisualChildren<Button>().Single()); await Settle();
                var popup = dock.FindVisualChildren<LayoutAutoHideWindowControl>().Single(c => ReferenceEquals(c.Model, a));
                Invoke(NamedButton(popup, "Pin tool")); await Settle(); Check.Same(pane, a.Parent); Check.False(a.IsAutoHidden); Check.Same(presenter, dock.GetLayoutItemFromModel(a).View);
            });
            tests.Test("live theme switching keeps presenter and changes compact caption contrast", async () =>
            {
                await Reset(); var a = Tool("explorer"); var view = Pane(a); var presenter = dock.GetLayoutItemFromModel(a).View;
                var button = NamedButton(view, "Auto-hide tool"); var light = ((SolidColorBrush)button.Foreground).Color;
                dock.RequestedTheme = ElementTheme.Dark; await Settle();
                var dark = ((SolidColorBrush)button.Foreground).Color; Check.True(light != dark); Check.Same(presenter, dock.GetLayoutItemFromModel(a).View); Check.Same(view, Pane(a));
                dock.RequestedTheme = ElementTheme.Light; await Settle(); Check.Equal(light, ((SolidColorBrush)button.Foreground).Color);
            });
            tests.Test("invalid density override falls back and valid override changes measured strip", async () =>
            {
                await Reset(); var tab = Tab(Document("editor"));
                dock.Resources["UnoDock.TabHeight"] = double.NaN; await Settle(); Check.Near(19, tab.Height);
                dock.Resources["UnoDock.TabHeight"] = 32d; await Settle(); Check.Near(31, tab.Height);
                dock.Resources.Remove("UnoDock.TabHeight"); await Settle(); Check.Near(19, tab.Height);
            });
            tests.Test("original title templates are rendered by compact captions", async () =>
            {
                await Reset();
                dock.AnchorableTitleTemplate = (DataTemplate)XamlReader.Load("<DataTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'><TextBlock Text='custom caption marker'/></DataTemplate>");
                await Settle(); Check.True(Pane(Tool("explorer")).FindVisualChildren<TextBlock>().Any(t => t.Text == "custom caption marker"));
                dock.AnchorableTitleTemplate = null; await Settle();
                Check.False(Pane(Tool("explorer")).FindVisualChildren<TextBlock>().Any(t => t.Text == "custom caption marker"));
            });
            tests.Test("default document icon is visible without replacing model content", async () =>
            {
                await Reset(); var doc = Document("editor"); var content = doc.Content;
                var image = new Microsoft.UI.Xaml.Media.Imaging.WriteableBitmap(1, 1); doc.IconSource = image; await Settle();
                Check.True(Tab(doc).FindVisualChildren<Image>().Any(i => ReferenceEquals(i.Source, image) && Visible(i))); Check.Same(content, doc.Content);
                doc.IconSource = null; await Settle(); Check.False(Tab(doc).FindVisualChildren<Image>().Any(Visible));
            });
            tests.Test("overflow selection exposes selected header and returning first restores offset", async () =>
            {
                await Reset(); var first = Document("editor"); var pane = (LayoutDocumentPane)first.Parent!;
                for (var i = 0; i < 35; i++) pane.Children.Add(new LayoutDocument { Title = $"Overflow document {i:D2}", ContentId = "overflow-" + i });
                await Settle(); var selected = pane.Children[^1]; selected.IsActive = true; await Settle(); await Task.Delay(120);
                var scroll = Pane(first).FindVisualChildren<ScrollViewer>().Single(s => s.Content is Panel panel && panel.Children.OfType<LayoutTabItemBase>().Any());
                Check.True(scroll.HorizontalOffset > 100); Check.True(Bounds(Tab(selected)).Right <= Bounds(scroll).Right + 1);
                first.IsActive = true; await Settle(); await Task.Delay(80); Check.Near(0, scroll.HorizontalOffset, 1);
            });
            tests.Test("queued overflow reveal cannot affect a replacement layout", async () =>
            {
                await Reset(); var pane = (LayoutDocumentPane)Document("editor").Parent!;
                pane.Children.Add(new LayoutDocument { Title = "old", ContentId = "old" }); pane.Children[^1].IsActive = true;
                VisualScene.Populate(dock); await Settle(); Check.Equal("editor", dock.Layout.ActiveContent!.ContentId);
                Check.False(dock.Layout.Descendents().OfType<LayoutContent>().Any(c => c.ContentId == "old"));
            });
            tests.Test("RTL native client projection contains root mirroring", async () =>
            {
                await Reset(); using var coordinates = new DesktopWindowCoordinates();
                var ltr = coordinates.ToScreen(dock, default); dock.FlowDirection = FlowDirection.RightToLeft; await Settle();
                var rtl = coordinates.ToScreen(dock, default); var scale = dock.XamlRoot!.RasterizationScale;
                Check.Near(dock.ActualWidth * scale, rtl.X - ltr.X, 1); Check.Near(ltr.Y, rtl.Y, 1);
                var point = new Point(123.25, 67.75); var roundtrip = coordinates.FromScreen(coordinates.ToScreen(dock, point), dock);
                Check.Near(point.X, roundtrip.X, 1); Check.Near(point.Y, roundtrip.Y, 1);
                dock.FlowDirection = FlowDirection.LeftToRight;
            });
            tests.Test("RTL and LTR native windows translate through physical axes", async () =>
            {
                await Reset(); using var coordinates = new DesktopWindowCoordinates();
                var grid = new Grid { FlowDirection = FlowDirection.RightToLeft }; var second = new Window { Content = grid };
                second.AppWindow.Move(new() { X = 1180, Y = 83 }); second.AppWindow.Resize(new() { Width = 400, Height = 300 }); second.Activate();
                try
                {
                    await Task.Delay(100); grid.UpdateLayout();
                    var origin = coordinates.ToScreen(dock, default); var right = coordinates.ToScreen(grid, default);
                    var translated = coordinates.Translate(dock, new(27, 41), grid);
                    var physical = coordinates.ToScreen(dock, new(27, 41));
                    Check.Near((right.X - physical.X) / grid.XamlRoot!.RasterizationScale, translated.X, 1);
                    Check.Near((physical.Y - right.Y) / grid.XamlRoot!.RasterizationScale, translated.Y, 1);
                    Check.True(Math.Abs(origin.X - right.X) > 50);
                }
                finally { second.Content = null; second.Close(); }
            });
            return await tests.Run(output, "visual-parity");
        }
        finally { window.Content = null; window.Close(); }
        async Task Reset()
        {
            dock.FlowDirection = FlowDirection.LeftToRight; dock.Theme = null; dock.RequestedTheme = ElementTheme.Light;
            dock.AnchorableTitleTemplate = null; dock.Resources.Remove("UnoDock.TabHeight"); VisualScene.Populate(dock); window.Activate(); await Settle();
        }
        async Task Settle() { dock.Refresh(); dock.UpdateLayout(); await Task.Delay(80); dock.UpdateLayout(); }
        LayoutDocument Document(string id) => dock.Layout.Descendents().OfType<LayoutDocument>().Single(d => d.ContentId == id);
        LayoutAnchorable Tool(string id) => dock.Layout.Descendents().OfType<LayoutAnchorable>().Single(d => d.ContentId == id);
        LayoutTabItemBase Tab(LayoutContent content) => dock.FindVisualChildren<LayoutTabItemBase>().Single(t => ReferenceEquals(t.Model, content) && t is not AnchorablePaneTitle);
        LayoutCachePaneControl Pane(LayoutContent content) => dock.FindVisualChildren<LayoutCachePaneControl>().Single(p => ReferenceEquals(((ILayoutControl)p).Model, content.Parent));
    }
    private static XDocument Fixture(string name)
    {
        using var stream = typeof(VisualParityTests).Assembly.GetManifestResourceStream("VisualFixtures." + name + ".xml") ?? throw new InvalidOperationException("Missing visual observation " + name);
        return XDocument.Load(stream);
    }
    private static void CompareScene(string name, string output)
    {
        var expected = Fixture(name); var actual = XDocument.Load(Path.Combine(output, name + ".xml"));
        // Text rendering and the length of a rotated label depend on OS fonts. Compare
        // host/editor/rail rectangles, not font-dependent glyph or label pixel extents.
        static bool Geometry(XElement e) => (string?)e.Attribute("type") is "LayoutPanelControl" or "LayoutDocumentPaneControl" or "LayoutAnchorablePaneControl" or "LayoutAnchorSideControl" or "TextBox"
            && double.Parse(e.Attribute("width")!.Value, CultureInfo.InvariantCulture) > 0 && double.Parse(e.Attribute("height")!.Value, CultureInfo.InvariantCulture) > 0;
        var grouped = actual.Root!.Elements("Element").Where(Geometry).GroupBy(e => ((string?)e.Attribute("type"), (string?)e.Attribute("content"))).ToDictionary(g => g.Key, g => new Queue<XElement>(g));
        foreach (var element in expected.Root!.Elements("Element").Where(Geometry))
        {
            var key = ((string?)element.Attribute("type"), (string?)element.Attribute("content"));
            Check.True(grouped.TryGetValue(key, out var queue) && queue.Count > 0, "Missing arranged element " + key);
            var observed = grouped[key].Dequeue();
            foreach (var dimension in new[] { "x", "y", "width", "height" })
            {
                var a = double.Parse(element.Attribute(dimension)!.Value, CultureInfo.InvariantCulture);
                var b = double.Parse(observed.Attribute(dimension)!.Value, CultureInfo.InvariantCulture);
                Check.True(double.IsFinite(b) && Math.Abs(a - b) <= 1, $"{name}: {key} {dimension}: reference={a:R}, actual={b:R}");
            }
        }
        Check.True(grouped.Values.All(q => q.Count == 0), "Unexpected visible arranged elements.");
    }
    private static Rect Bounds(FrameworkElement element) => element.TransformToVisual(null).TransformBounds(new Rect(0, 0, element.ActualWidth, element.ActualHeight));
    private static bool Visible(FrameworkElement element)
    {
        for (DependencyObject? p = element; p != null; p = VisualTreeHelper.GetParent(p)) if (p is UIElement { Visibility: Visibility.Collapsed }) return false;
        return element.IsLoaded && element.ActualWidth > 0 && element.ActualHeight > 0;
    }
    private static Button NamedButton(FrameworkElement parent, string name) => parent.FindVisualChildren<Button>().Single(b => AutomationProperties.GetName(b) == name);
    private static void Invoke(Button button)
    {
        var peer = FrameworkElementAutomationPeer.CreatePeerForElement(button);
        Check.True(peer?.GetPattern(PatternInterface.Invoke) is IInvokeProvider, "Compact button must retain native Invoke provider.");
        ((IInvokeProvider)peer!.GetPattern(PatternInterface.Invoke)!).Invoke();
    }
}
