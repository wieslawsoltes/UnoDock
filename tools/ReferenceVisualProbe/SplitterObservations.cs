// Independent public-event protocol probe; no source/IL/template inspection.
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Xml.Linq;
using Xceed.Wpf.AvalonDock;
using Xceed.Wpf.AvalonDock.Controls;
using Xceed.Wpf.AvalonDock.Layout;

internal static class SplitterObservations
{
    public static void Run(DockingManager manager, string directory)
    {
        var observations = new XElement("SplitterObservations",
            new XAttribute("input", "public-routed-drag-events-not-pointer-acceptance"));
        foreach (var vertical in new[] { false, true })
        foreach (var rtl in new[] { false, true })
        foreach (var cancel in new[] { false, true })
        {
            var first = new LayoutAnchorablePane(new LayoutAnchorable { ContentId = "tool", Title = "Tool", Content = "Owned probe" });
            var second = new LayoutDocumentPane(new LayoutDocument { ContentId = "document", Title = "Document", Content = "Owned probe" });
            first.DockWidth = first.DockHeight = new GridLength(1, GridUnitType.Star);
            second.DockWidth = second.DockHeight = new GridLength(2, GridUnitType.Star);
            var panel = new LayoutPanel(first) { Orientation = vertical ? Orientation.Vertical : Orientation.Horizontal };
            panel.Children.Add(second);
            manager.Layout = new LayoutRoot { RootPanel = panel };
            manager.FlowDirection = rtl ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
            manager.UpdateLayout();
            var splitter = Descendants(manager).OfType<LayoutGridResizerControl>().Single();
            var scene = new XElement("Scenario", new XAttribute("orientation", panel.Orientation),
                new XAttribute("rtl", rtl), new XAttribute("cancel", cancel));
            Action<string> capture = phase =>
            {
                manager.UpdateLayout();
                var a = vertical ? first.DockHeight : first.DockWidth;
                var b = vertical ? second.DockHeight : second.DockWidth;
                scene.Add(new XElement("State", new XAttribute("phase", phase),
                    new XAttribute("before", a.Value.ToString("R", CultureInfo.InvariantCulture)), new XAttribute("beforeUnit", a.GridUnitType),
                    new XAttribute("after", b.Value.ToString("R", CultureInfo.InvariantCulture)), new XAttribute("afterUnit", b.GridUnitType)));
            };
            capture("initial");
            splitter.RaiseEvent(new DragStartedEventArgs(0, 0) { RoutedEvent = Thumb.DragStartedEvent });
            capture("started");
            splitter.RaiseEvent(new DragDeltaEventArgs(vertical ? 0 : 40, vertical ? 40 : 0) { RoutedEvent = Thumb.DragDeltaEvent });
            capture("delta40");
            splitter.RaiseEvent(new DragDeltaEventArgs(vertical ? 0 : 15, vertical ? 15 : 0) { RoutedEvent = Thumb.DragDeltaEvent });
            capture("delta15");
            splitter.RaiseEvent(new DragCompletedEventArgs(vertical ? 0 : 55, vertical ? 55 : 0, cancel) { RoutedEvent = Thumb.DragCompletedEvent });
            capture("completed");
            observations.Add(scene);
        }
        new XDocument(observations).Save(Path.Combine(directory, "splitter-observations.xml"));
        Console.WriteLine("Captured eight public splitter drag-protocol observations.");
    }
    private static System.Collections.Generic.IEnumerable<DependencyObject> Descendants(DependencyObject node)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++)
        {
            var child = VisualTreeHelper.GetChild(node, i); yield return child;
            foreach (var nested in Descendants(child)) yield return nested;
        }
    }
}
