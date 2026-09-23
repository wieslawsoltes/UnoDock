// Independently authored public-API observations. Only live public values and
// rendered pixels are exported, never templates, resources or implementation IL.
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

internal static class NavigatorObservations
{
    public static void Run(DockingManager manager, Window owner, string output)
    {
        foreach (var scenario in new[] { "navigator", "navigator-tool", "navigator-many", "navigator-rtl" })
        {
            var documents = new LayoutDocumentPane(); var tools = new LayoutAnchorablePane();
            var count = scenario == "navigator-many" ? 40 : 3;
            for (var i = 0; i < count; i++) documents.Children.Add(new LayoutDocument {
                ContentId = "document-" + i, Title = "Document " + i.ToString("D2") + ".cs",
                Description = "Project / Source / Document " + i.ToString("D2") + ".cs", Content = "Owned probe document " + i });
            foreach (var title in new[] { "Solution Explorer", "Properties", "Output" })
                tools.Children.Add(new LayoutAnchorable { ContentId = title, Title = title, Content = "Owned probe tool " + title });
            var panel = new LayoutPanel(tools); panel.Children.Add(documents);
            manager.Layout = new LayoutRoot { RootPanel = panel };
            documents.Children[0].IsActive = true;
            var time = new DateTime(2000, 1, 1);
            var ordinal = 0;
            foreach (var c in manager.Layout.Descendents().OfType<LayoutContent>()) c.LastActivationTimeStamp = time.AddSeconds(ordinal++);
            manager.UpdateLayout();
            var navigator = new NavigatorWindow(manager) { Owner = owner,
                FlowDirection = scenario == "navigator-rtl" ? FlowDirection.RightToLeft : FlowDirection.LeftToRight };
            try
            {
                navigator.Show(); navigator.UpdateLayout();
                if (scenario == "navigator-tool") navigator.SelectedAnchorable = navigator.Anchorables.First();
                else navigator.SelectedDocument = navigator.Documents.Last();
                navigator.UpdateLayout(); navigator.Dispatcher.Invoke(DispatcherPriority.Render, new Action(() => { }));
                // Capture the client content only, excluding OS-owned non-client chrome.
                var root = navigator.Content as FrameworkElement;
                if (root == null) throw new InvalidOperationException("Navigator client content is not realized.");
                var bitmap = new RenderTargetBitmap((int)Math.Ceiling(root.ActualWidth), (int)Math.Ceiling(root.ActualHeight), 96, 96, PixelFormats.Pbgra32);
                bitmap.Render(root); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using (var file = File.Create(Path.Combine(output, scenario + ".png"))) encoder.Save(file);
                var scene = new XElement("Navigator", new XAttribute("name", scenario), new XAttribute("width", root.ActualWidth),
                    new XAttribute("height", root.ActualHeight), new XAttribute("windowWidth", navigator.ActualWidth),
                    new XAttribute("windowHeight", navigator.ActualHeight), new XAttribute("documentLabel", navigator.LayoutDocumentsLabel),
                    new XAttribute("toolLabel", navigator.LayoutAnchorablesLabel));
                Walk(root, root, scene); new XDocument(scene).Save(Path.Combine(output, scenario + ".xml"));
                Console.WriteLine("Captured public reference " + scenario);
            }
            finally { navigator.Close(); }
        }
    }
    private static void Walk(DependencyObject node, FrameworkElement root, XElement output)
    {
        if (node is FrameworkElement element && element.IsVisible)
        {
            var bounds = element.TransformToAncestor(root).TransformBounds(new Rect(0, 0, element.ActualWidth, element.ActualHeight));
            var row = new XElement("Element", new XAttribute("type", node.GetType().Name), new XAttribute("name", element.Name ?? ""),
                new XAttribute("x", bounds.X.ToString("R", CultureInfo.InvariantCulture)), new XAttribute("y", bounds.Y.ToString("R", CultureInfo.InvariantCulture)),
                new XAttribute("width", bounds.Width.ToString("R", CultureInfo.InvariantCulture)), new XAttribute("height", bounds.Height.ToString("R", CultureInfo.InvariantCulture)));
            if (node is TextBlock text) { row.SetAttributeValue("text", text.Text); row.SetAttributeValue("fontSize", text.FontSize); row.SetAttributeValue("foreground", Brush(text.Foreground)); }
            if (node is Control control) { row.SetAttributeValue("fontSize", control.FontSize); row.SetAttributeValue("padding", control.Padding); row.SetAttributeValue("background", Brush(control.Background)); row.SetAttributeValue("foreground", Brush(control.Foreground)); }
            if (node is Border border) { row.SetAttributeValue("background", Brush(border.Background)); row.SetAttributeValue("border", Brush(border.BorderBrush)); row.SetAttributeValue("thickness", border.BorderThickness); }
            if (node is ListBoxItem item) row.SetAttributeValue("selected", item.IsSelected);
            if (node is ScrollViewer scroll) { row.SetAttributeValue("offset", scroll.VerticalOffset); row.SetAttributeValue("extent", scroll.ExtentHeight); row.SetAttributeValue("viewport", scroll.ViewportHeight); }
            output.Add(row);
        }
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++) Walk(VisualTreeHelper.GetChild(node, i), root, output);
    }
    private static string Brush(Brush brush) => brush is SolidColorBrush solid ? solid.Color.ToString() : brush == null ? "null" : brush.GetType().Name;
}
