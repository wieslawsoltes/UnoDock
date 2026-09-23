// Independently authored public-API observation of the documentation's sample
// arrangement. Original sample source/templates/resources are not read or exported.
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Xml.Linq;
using Xceed.Wpf.AvalonDock;
using Xceed.Wpf.AvalonDock.Controls;
using Xceed.Wpf.AvalonDock.Layout;
using Xceed.Wpf.Toolkit;

internal static class Program
{
    [STAThread] private static int Main(string[] args)
    {
        if (args.Length != 1) throw new ArgumentException("Usage: ReferenceSampleProbe <output>");
        Directory.CreateDirectory(args[0]);
        var app = new Application(); var failure = 0;
        var manager = new DockingManager { Width = 1000, Height = 640, FontSize = 12, AllowMixedOrientation = true };
        var window = new Window { Content = manager, SizeToContent = SizeToContent.WidthAndHeight, WindowStyle = WindowStyle.None, ResizeMode = ResizeMode.NoResize };
        window.Loaded += (_, __) => window.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
        {
            try
            {
                foreach (var scenario in new[] { "classic", "selected-editor", "rtl" })
                {
                    var propertyGrid = Populate(manager);
                    manager.FlowDirection = scenario == "rtl" ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
                    if (scenario == "selected-editor")
                    {
                        var second = manager.Layout.Descendents().OfType<LayoutDocument>().Single(d => d.ContentId == "document2");
                        second.IsActive = true; propertyGrid.SelectedObject = second.Content;
                    }
                    manager.UpdateLayout(); window.Dispatcher.Invoke(DispatcherPriority.Render, new Action(() => { }));
                    var bitmap = new RenderTargetBitmap(1000, 640, 96, 96, PixelFormats.Pbgra32); bitmap.Render(manager);
                    var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                    using (var stream = File.Create(Path.Combine(args[0], "sample-reference-" + scenario + ".png"))) encoder.Save(stream);
                    var scene = new XElement("Scene", new XAttribute("name", scenario), new XAttribute("width", manager.ActualWidth), new XAttribute("height", manager.ActualHeight));
                    Walk(manager, manager, scene);
                    new XDocument(scene).Save(Path.Combine(args[0], "sample-reference-" + scenario + ".xml"));
                }
            }
            catch (Exception error) { failure = 1; Console.Error.WriteLine(error); }
            finally { window.Close(); app.Shutdown(); }
        }));
        window.Show(); app.Run(); return failure;
    }
    private static PropertyGrid Populate(DockingManager manager)
    {
        var first = new LayoutDocument { ContentId = "document1", Title = "Document 1", Content = new Button { Content = "Document 1 Content", FontSize = 12, Padding = new Thickness(8, 3, 8, 3), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } };
        var second = new LayoutDocument { ContentId = "document2", Title = "Document 2", Content = Text("Document 2 Content") };
        var docs = new LayoutDocumentPane(); docs.Children.Add(first); docs.Children.Add(second);
        var propertyGrid = new PropertyGrid { SelectedObject = first.Content, NameColumnWidth = 96, FontSize = 12 };
        var properties = Tool("properties", "Properties", propertyGrid); properties.CanHide = false; properties.CanClose = false; properties.AutoHideWidth = 240;
        var left = new LayoutAnchorablePane { DockWidth = new GridLength(200), DockMinWidth = 150 }; left.Children.Add(properties);
        var right = new LayoutAnchorablePane();
        right.Children.Add(Tool("alarms", "Alarms", new ListBox { ItemsSource = new[] { "Alarm 1", "Alarm 2", "Alarm 3" }, FontSize = 12, BorderThickness = new Thickness(0) }));
        right.Children.Add(Tool("journal", "Journal", Text("Journal\n\nAn editable journal accompanies this docking sample.")));
        var rightGroup = new LayoutAnchorablePaneGroup { DockWidth = new GridLength(125), DockMinWidth = 100 }; rightGroup.Children.Add(right);
        var documentGroup = new LayoutDocumentPaneGroup(); documentGroup.Children.Add(docs);
        var panel = new LayoutPanel { Orientation = Orientation.Horizontal }; panel.Children.Add(left); panel.Children.Add(documentGroup); panel.Children.Add(rightGroup);
        var root = new LayoutRoot { RootPanel = panel }; var rail = new LayoutAnchorGroup();
        rail.Children.Add(Tool("agenda", "Agenda", Text("Agenda"))); rail.Children.Add(Tool("contacts", "Contacts", Text("Contacts")));
        root.LeftSide.Children.Add(rail); manager.Layout = root; first.IsActive = true; return propertyGrid;
    }
    private static LayoutAnchorable Tool(string id, string title, object content) => new LayoutAnchorable { ContentId = id, Title = title, Content = content };
    private static TextBox Text(string value) => new TextBox { Text = value, AcceptsReturn = true, FontSize = 12, FontFamily = new FontFamily("Consolas"), Padding = new Thickness(5), BorderThickness = new Thickness(0) };
    private static void Walk(DependencyObject node, FrameworkElement origin, XElement output)
    {
        if (node is FrameworkElement element && element.IsVisible && node is ILayoutControl layout)
        {
            var rect = element.TransformToAncestor(origin).TransformBounds(new Rect(0, 0, element.ActualWidth, element.ActualHeight));
            var model = layout.Model;
            var ids = model is LayoutContent content ? content.ContentId : model is ILayoutContainer container ? string.Join(",", container.Children.OfType<LayoutContent>().Select(c => c.ContentId)) : "";
            output.Add(new XElement("Element", new XAttribute("type", node.GetType().Name), new XAttribute("content", ids ?? ""),
                new XAttribute("x", rect.X.ToString("R", CultureInfo.InvariantCulture)), new XAttribute("y", rect.Y.ToString("R", CultureInfo.InvariantCulture)),
                new XAttribute("width", rect.Width.ToString("R", CultureInfo.InvariantCulture)), new XAttribute("height", rect.Height.ToString("R", CultureInfo.InvariantCulture))));
        }
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++) Walk(VisualTreeHelper.GetChild(node, i), origin, output);
    }
}
