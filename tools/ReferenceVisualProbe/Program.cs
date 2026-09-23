// Independent public-API observation program. No implementation, templates or
// non-public fields are inspected; screenshots are CI evidence, not product assets.
using System;
using System.IO;
using System.Linq;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Xml.Linq;
using UnoDock.VisualValidation;
using Xceed.Wpf.AvalonDock;
using Xceed.Wpf.AvalonDock.Layout;
using Xceed.Wpf.AvalonDock.Controls;

internal static class Program
{
    [STAThread] private static int Main(string[] args)
    {
        if (args.Length != 1) throw new ArgumentException("Usage: ReferenceVisualProbe <output-directory>");
        Directory.CreateDirectory(args[0]); var app = new Application(); var failure = 0;
        app.Startup += (_, __) =>
        {
            var manager = new DockingManager { Width = SceneContent.Width, Height = SceneContent.Height };
            var window = new Window { Content = manager, SizeToContent = SizeToContent.WidthAndHeight, WindowStyle = WindowStyle.None, ResizeMode = ResizeMode.NoResize, ShowInTaskbar = false };
            window.Loaded += (_, ___) => window.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
            {
                try
                {
                    AutoHideObservations.Run(args[0]);
                    foreach (var scenario in new[] { "docked", "active-tool", "auto-hide", "rtl" })
                    {
                        Populate(manager); manager.FlowDirection = scenario == "rtl" ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
                        if (scenario == "active-tool") manager.Layout.Descendents().OfType<LayoutAnchorable>().First(a => a.ContentId == "explorer").IsActive = true;
                        if (scenario == "auto-hide") manager.Layout.Descendents().OfType<LayoutAnchorable>().First(a => a.ContentId == "explorer").ToggleAutoHide();
                        manager.UpdateLayout(); window.Dispatcher.Invoke(DispatcherPriority.Render, new Action(() => { }));
                        var bitmap = new RenderTargetBitmap(SceneContent.Width, SceneContent.Height, 96, 96, PixelFormats.Pbgra32);
                        bitmap.Render(manager); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                        using (var file = File.Create(Path.Combine(args[0], scenario + ".png"))) encoder.Save(file);
                        var elements = new XElement("Scene", new XAttribute("name", scenario), new XAttribute("width", manager.ActualWidth), new XAttribute("height", manager.ActualHeight));
                        Walk(manager, manager, elements); new XDocument(elements).Save(Path.Combine(args[0], scenario + ".xml"));
                        Console.WriteLine("Captured public reference scene " + scenario);
                    }
                    NavigatorObservations.Run(manager, window, args[0]);
                    OverlayObservations.Run(manager, window, args[0]);
                }
                catch (Exception error) { failure = 1; Console.Error.WriteLine(error); }
                finally { window.Close(); app.Shutdown(); }
            }));
            Populate(manager); window.Show();
        };
        app.Run(); return failure;
    }
    private static void Populate(DockingManager manager)
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
    private static LayoutDocument Doc(string id, string title, string text) => new LayoutDocument { ContentId = id, Title = title, Content = Text(text) };
    private static LayoutAnchorable Tool(string id, string title, object content) => new LayoutAnchorable { ContentId = id, Title = title, Content = content, CanClose = false };
    private static TextBox Text(string text) => new TextBox { AcceptsReturn = true, Text = text, TextWrapping = TextWrapping.Wrap, FontFamily = new FontFamily("Consolas"), FontSize = 12, Padding = new Thickness(14), BorderThickness = new Thickness(0) };
    private static ScrollViewer Rows(string[] rows)
    {
        var items = new StackPanel { Margin = new Thickness(10, 9, 8, 9) };
        foreach (var row in rows) items.Children.Add(new TextBlock { Text = row, FontSize = 12, Margin = new Thickness(0, 0, 0, 8), TextTrimming = TextTrimming.CharacterEllipsis });
        return new ScrollViewer { Content = items, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
    }
    private static void Walk(DependencyObject node, FrameworkElement relative, XElement output)
    {
        if (node is FrameworkElement element && element.IsVisible && (node is ILayoutControl || node is TextBox))
        {
            var rect = element.TransformToAncestor(relative).TransformBounds(new Rect(0, 0, element.ActualWidth, element.ActualHeight));
            var model = (node as ILayoutControl)?.Model;
            var ids = model is LayoutContent content ? content.ContentId : model is ILayoutContainer container ? string.Join(",", container.Children.OfType<LayoutContent>().Select(c => c.ContentId)) : "";
            output.Add(new XElement("Element", new XAttribute("type", node.GetType().Name), new XAttribute("content", ids ?? ""),
                new XAttribute("x", rect.X.ToString("R", CultureInfo.InvariantCulture)), new XAttribute("y", rect.Y.ToString("R", CultureInfo.InvariantCulture)),
                new XAttribute("width", rect.Width.ToString("R", CultureInfo.InvariantCulture)), new XAttribute("height", rect.Height.ToString("R", CultureInfo.InvariantCulture))));
        }
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++) Walk(VisualTreeHelper.GetChild(node, i), relative, output);
    }
}
