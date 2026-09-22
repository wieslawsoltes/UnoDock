using Xceed.Wpf.AvalonDock.Controls;
using Xceed.Wpf.AvalonDock.Themes;
using UnoDock.VisualValidation;
using System.Xml.Linq;
using System.Globalization;

namespace UnoDock.Testing;

public static class VisualScene
{
    public static void Populate(DockingManager manager)
    {
        var code = Doc("editor", "Workspace.cs", SceneContent.Code);
        var docs = new LayoutDocumentPane(code); docs.Children.Add(Doc("readme", "Readme.md", SceneContent.Readme)); docs.Children.Add(Doc("notes", "Notes.txt", SceneContent.Notes));
        var left = new LayoutAnchorablePane(Tool("explorer", "Solution Explorer", Rows(SceneContent.Explorer))) { DockWidth = new GridLength(200), DockMinWidth = 120 };
        left.Children.Add(Tool("toolbox", "Toolbox", Rows(SceneContent.Toolbox)));
        var right = new LayoutAnchorablePane(Tool("properties", "Properties", Rows(SceneContent.Properties))) { DockWidth = new GridLength(230), DockMinWidth = 120 };
        var bottom = new LayoutAnchorablePane(Tool("output", "Output", Text(SceneContent.Output))) { DockHeight = new GridLength(155), DockMinHeight = 60 };
        bottom.Children.Add(Tool("errors", "Error List", Rows(SceneContent.Errors)));
        var middle = new LayoutPanel(docs) { Orientation = Orientation.Vertical }; middle.Children.Add(bottom);
        var panel = new LayoutPanel(left) { Orientation = Orientation.Horizontal }; panel.Children.Add(middle); panel.Children.Add(right);
        manager.Layout = new LayoutRoot { RootPanel = panel }; code.IsActive = true;
    }
    private static LayoutDocument Doc(string id, string title, string text) => new() { ContentId = id, Title = title, Content = Text(text) };
    private static LayoutAnchorable Tool(string id, string title, object content) => new() { ContentId = id, Title = title, Content = content, CanClose = false };
    private static TextBox Text(string text) => new() { AcceptsReturn = true, Text = text, TextWrapping = TextWrapping.Wrap,
        FontFamily = new FontFamily(OperatingSystem.IsWindows() ? "Consolas" : "DejaVu Sans Mono"), FontSize = 12, Padding = new Thickness(14), BorderThickness = new Thickness(0),
        Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent), CornerRadius = new(0) };
    private static ScrollViewer Rows(string[] rows)
    {
        var items = new StackPanel { Margin = new Thickness(10, 9, 8, 9) };
        foreach (var row in rows) items.Children.Add(new TextBlock { Text = row, FontSize = 12, Margin = new Thickness(0, 0, 0, 8), TextTrimming = TextTrimming.CharacterEllipsis });
        return new ScrollViewer { Content = items, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
    }
    public static async Task Capture(DockingManager templateSource, string output)
    {
        using var manager = new DockingManager { Width = SceneContent.Width, Height = SceneContent.Height, Template = templateSource.Template, RequestedTheme = ElementTheme.Light, FontSize = 12 };
        var window = new Window { Content = manager, Title = "UnoDock visual observations" };
        window.AppWindow.Resize(new() { Width = SceneContent.Width + 64, Height = SceneContent.Height + 100 });
        manager.HorizontalAlignment = HorizontalAlignment.Left; manager.VerticalAlignment = VerticalAlignment.Top;
        window.Activate();
        try
        {
            foreach (var scenario in new[] { "docked", "active-tool", "auto-hide", "rtl", "dark" })
            {
                manager.RequestedTheme = scenario == "dark" ? ElementTheme.Dark : ElementTheme.Light;
                Populate(manager); manager.FlowDirection = scenario == "rtl" ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
                if (scenario == "active-tool") manager.Layout.Descendents().OfType<LayoutAnchorable>().First(a => a.ContentId == "explorer").IsActive = true;
                if (scenario == "auto-hide") manager.Layout.Descendents().OfType<LayoutAnchorable>().First(a => a.ContentId == "explorer").ToggleAutoHide();
                await Task.Delay(120); manager.Refresh(); manager.UpdateLayout(); await Task.Delay(50);
                Check.Near(SceneContent.Width, manager.ActualWidth); Check.Near(SceneContent.Height, manager.ActualHeight);
                await VisualCapture.Save(manager, Path.Combine(output, scenario + ".png"));
                new XDocument(Measure(manager, scenario)).Save(Path.Combine(output, scenario + ".xml"));
            }
        }
        finally { window.Content = null; window.Close(); }
    }
    internal static XElement Measure(DockingManager manager, string scenario)
    {
        var output = new XElement("Scene", new XAttribute("name", scenario), new XAttribute("width", manager.ActualWidth), new XAttribute("height", manager.ActualHeight));
        foreach (var element in manager.FindVisualChildren<FrameworkElement>())
        {
            if (element is not ILayoutControl && element is not TextBox) continue;
            if (element.Visibility != Visibility.Visible || element.ActualWidth <= 0 || element.ActualHeight <= 0) continue;
            bool visible = true; for (DependencyObject? p = element; p != null; p = VisualTreeHelper.GetParent(p)) if (p is UIElement { Visibility: Visibility.Collapsed }) visible = false;
            if (!visible) continue;
            var rect = element.TransformToVisual(null).TransformBounds(new Windows.Foundation.Rect(0, 0, element.ActualWidth, element.ActualHeight));
            var origin = manager.TransformToVisual(null).TransformBounds(new Windows.Foundation.Rect(0, 0, manager.ActualWidth, manager.ActualHeight));
            rect.X -= origin.X; rect.Y -= origin.Y;
            var model = (element as ILayoutControl)?.Model;
            var ids = model is LayoutContent content ? content.ContentId : model is ILayoutContainer container ? string.Join(",", container.Children.OfType<LayoutContent>().Select(c => c.ContentId)) : "";
            output.Add(new XElement("Element", new XAttribute("type", element.GetType().Name), new XAttribute("content", ids ?? ""),
                new XAttribute("x", rect.X.ToString("R", CultureInfo.InvariantCulture)), new XAttribute("y", rect.Y.ToString("R", CultureInfo.InvariantCulture)),
                new XAttribute("width", rect.Width.ToString("R", CultureInfo.InvariantCulture)), new XAttribute("height", rect.Height.ToString("R", CultureInfo.InvariantCulture))));
        }
        return output;
    }
}
