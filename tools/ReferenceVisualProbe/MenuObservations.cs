// Independently authored public-menu observation. No original templates, IL,
// source bodies, resources or icon geometry are read or exported.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Xml.Linq;
using Xceed.Wpf.AvalonDock;
using Xceed.Wpf.AvalonDock.Layout;

internal static class MenuObservations
{
    public static void Run(DockingManager manager, string directory)
    {
        var output = new XElement("MenuObservations", new XAttribute("method", "public-model-context-menu-opening-not-native-pointer-acceptance"));
        foreach (var scenario in new[]
        {
            "document",
            "document-single",
            "document-no-close",
            "document-no-float",
            "document-multiple-groups",
            "tool",
            "tool-no-hide",
            "tool-no-autohide",
            "tool-no-document",
            "tool-autohidden"
        }

        )
        {
            var document = new LayoutDocument
            {
                ContentId = "doc",
                Title = "Workspace.cs",
                Content = "Owned menu probe"
            };
            var documents = new LayoutDocumentPane(document);
            if (scenario != "document-single")
                documents.Children.Add(new LayoutDocument { ContentId = "other", Title = "Readme.md" });
            var tool = new LayoutAnchorable
            {
                ContentId = "tool",
                Title = "Solution Explorer",
                Content = "Owned menu probe"
            };
            var panel = new LayoutPanel(new LayoutAnchorablePane(tool));
            panel.Children.Add(documents);
            if (scenario == "document-multiple-groups")
                panel.Children.Add(new LayoutDocumentPane(new LayoutDocument { ContentId = "third" }));
            manager.Layout = new LayoutRoot
            {
                RootPanel = panel
            };
            manager.FlowDirection = FlowDirection.LeftToRight;
            if (scenario == "document-no-close")
                document.CanClose = false;
            if (scenario == "document-no-float")
                document.CanFloat = false;
            if (scenario == "tool-no-hide")
                tool.CanHide = false;
            if (scenario == "tool-no-autohide")
                tool.CanAutoHide = false;
            if (scenario == "tool-no-document")
                tool.CanDockAsTabbedDocument = false;
            if (scenario == "tool-autohidden")
                tool.ToggleAutoHide();
            manager.UpdateLayout();
            var isTool = scenario.StartsWith("tool", StringComparison.Ordinal);
            LayoutContent model = isTool ? (LayoutContent)tool : document;
            var adapter = manager.GetLayoutItemFromModel(model);
            var menu = isTool ? manager.AnchorableContextMenu : manager.DocumentContextMenu;
            if (menu == null)
                throw new InvalidOperationException("Original default menu is missing: " + scenario);
            var commands = new Dictionary<ICommand, string>();
            foreach (var property in adapter.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public))
                if (typeof(ICommand).IsAssignableFrom(property.PropertyType) && property.GetIndexParameters().Length == 0 && property.GetValue(adapter)is ICommand command)
                    commands[command] = property.Name;
            menu.DataContext = adapter;
            menu.PlacementTarget = manager;
            menu.Placement = PlacementMode.Relative;
            menu.HorizontalOffset = 30;
            menu.VerticalOffset = 30;
            try
            {
                menu.IsOpen = true;
                menu.UpdateLayout();
                CommandManager.InvalidateRequerySuggested();
                menu.Dispatcher.Invoke(DispatcherPriority.ApplicationIdle, new Action(() =>
                {
                }));
                menu.UpdateLayout();
                var scene = new XElement("Scenario", new XAttribute("name", scenario), new XAttribute("width", Number(menu.ActualWidth)), new XAttribute("height", Number(menu.ActualHeight)), new XAttribute("fontSize", Number(menu.FontSize)), new XAttribute("background", Color(menu.Background)), new XAttribute("foreground", Color(menu.Foreground)), new XAttribute("padding", menu.Padding));
                foreach (var entry in menu.Items)
                {
                    var element = entry as FrameworkElement ?? menu.ItemContainerGenerator.ContainerFromItem(entry) as FrameworkElement;
                    if (element == null)
                        throw new InvalidOperationException("Unrealized original menu item");
                    var node = new XElement("Item", new XAttribute("kind", element is Separator ? "separator" : "command"), new XAttribute("visibility", element.Visibility), new XAttribute("enabled", element.IsEnabled), new XAttribute("height", Number(element.ActualHeight)));
                    if (element is MenuItem item)
                    {
                        node.SetAttributeValue("header", Convert.ToString(item.Header, CultureInfo.InvariantCulture));
                        node.SetAttributeValue("command", item.Command != null && commands.TryGetValue(item.Command, out var name) ? name : "");
                        node.SetAttributeValue("checked", item.IsChecked);
                        node.SetAttributeValue("checkable", item.IsCheckable);
                        node.SetAttributeValue("gesture", item.InputGestureText);
                        node.SetAttributeValue("padding", item.Padding);
                    }

                    scene.Add(node);
                }

                output.Add(scene);
                if (scenario == "document" || scenario == "tool")
                {
                    var bitmap = new RenderTargetBitmap((int)Math.Ceiling(menu.ActualWidth), (int)Math.Ceiling(menu.ActualHeight), 96, 96, PixelFormats.Pbgra32);
                    bitmap.Render(menu);
                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(bitmap));
                    using (var stream = File.Create(Path.Combine(directory, "menu-" + scenario + ".png")))
                        encoder.Save(stream);
                }
            }
            finally
            {
                menu.IsOpen = false;
                menu.DataContext = null;
                menu.PlacementTarget = null;
            }
        }

        new XDocument(output).Save(Path.Combine(directory, "menu-observations.xml"));
        Console.WriteLine("Captured ten independent public-menu scenarios.");
    }

    private static string Number(double value) => value.ToString("R", CultureInfo.InvariantCulture);
    private static string Color(Brush value) => value is SolidColorBrush solid ? solid.Color.ToString() : value == null ? "null" : value.GetType().Name;
}
