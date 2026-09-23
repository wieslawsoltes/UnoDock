// Independent black-box observations of publicly realized controls.
// No original source, IL, templates, resources or drawing geometry are inspected.
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using System.Xml.Linq;
using Xceed.Wpf.AvalonDock;
using Xceed.Wpf.AvalonDock.Controls;
using Xceed.Wpf.AvalonDock.Layout;

internal static class AutoHideWindowObservations
{
    public static void Run(DockingManager manager, string directory)
    {
        var observations = new XElement("AutoHideWindows", new XAttribute("input", "public-model-activation-and-mouse-enter-event-not-native-pointer-acceptance"));
        foreach (var side in new[] { AnchorableShowStrategy.Left, AnchorableShowStrategy.Right, AnchorableShowStrategy.Top, AnchorableShowStrategy.Bottom })
        foreach (var explicitSize in new[] { false, true })
        {
            manager.FlowDirection = FlowDirection.LeftToRight;
            var doc = new LayoutDocument { ContentId = "editor", Title = "Editor", Content = "Application-owned probe content" };
            manager.Layout = new LayoutRoot { RootPanel = new LayoutPanel(new LayoutDocumentPane(doc)) };
            var tool = new LayoutAnchorable { ContentId = "tool", Title = "Explorer", Content = new System.Windows.Controls.TextBox { Text = "Application-owned flyout editor" }, AutoHideWidth = explicitSize ? 320 : 0, AutoHideHeight = explicitSize ? 220 : 0 };
            tool.AddToLayout(manager, side | AnchorableShowStrategy.Most);
            manager.UpdateLayout(); tool.ToggleAutoHide(); manager.UpdateLayout();
            var anchor = Walk(manager).OfType<LayoutAnchorControl>().Single(c => ReferenceEquals(c.Model, tool));
            anchor.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, Environment.TickCount) { RoutedEvent = Mouse.MouseEnterEvent });
            var frame = new DispatcherFrame();
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(650) };
            timer.Tick += (_, __) => { timer.Stop(); frame.Continue = false; };
            timer.Start(); Dispatcher.PushFrame(frame); manager.UpdateLayout();
            var flyout = manager.AutoHideWindow;
            var row = new XElement("Window", new XAttribute("side", side), new XAttribute("explicitSize", explicitSize),
                new XAttribute("autoHideWidth", tool.AutoHideWidth), new XAttribute("autoHideHeight", tool.AutoHideHeight),
                new XAttribute("minWidth", tool.AutoHideMinWidth), new XAttribute("minHeight", tool.AutoHideMinHeight),
                new XAttribute("active", tool.IsActive), new XAttribute("visible", flyout != null && flyout.IsVisible));
            if (flyout != null)
            {
                row.SetAttributeValue("width", flyout.ActualWidth.ToString("R", CultureInfo.InvariantCulture));
                row.SetAttributeValue("height", flyout.ActualHeight.ToString("R", CultureInfo.InvariantCulture));
                foreach (var element in Walk(flyout).OfType<FrameworkElement>().Where(e => e.IsVisible && (e is LayoutGridResizerControl || e is LayoutAnchorableControl || e is AnchorablePaneTitle)))
                {
                    var rect = element.TransformToAncestor(flyout).TransformBounds(new Rect(0, 0, element.ActualWidth, element.ActualHeight));
                    row.Add(new XElement("Element", new XAttribute("type", element.GetType().Name),
                        new XAttribute("x", rect.X.ToString("R", CultureInfo.InvariantCulture)), new XAttribute("y", rect.Y.ToString("R", CultureInfo.InvariantCulture)),
                        new XAttribute("width", rect.Width.ToString("R", CultureInfo.InvariantCulture)), new XAttribute("height", rect.Height.ToString("R", CultureInfo.InvariantCulture))));
                }
            }
            observations.Add(row); tool.ToggleAutoHide(); manager.UpdateLayout();
        }
        new XDocument(observations).Save(Path.Combine(directory, "auto-hide-window-observations.xml"));
        Console.WriteLine("Captured eight public auto-hide window observations.");
    }
    private static System.Collections.Generic.IEnumerable<DependencyObject> Walk(DependencyObject node)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++)
        {
            var child = VisualTreeHelper.GetChild(node, i); yield return child;
            foreach (var nested in Walk(child)) yield return nested;
        }
    }
}
