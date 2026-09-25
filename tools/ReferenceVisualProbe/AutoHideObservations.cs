using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xceed.Wpf.AvalonDock;
using Xceed.Wpf.AvalonDock.Layout;
using Xceed.Wpf.AvalonDock.Layout.Serialization;

// A public entry used from the visual probe; all data is observed via public APIs.
internal static class AutoHideObservations
{
    public static void Run(string output)
    {
        foreach (var scenario in new[]
        {
            "one",
            "two",
            "pin-one",
            "ineligible-sibling"
        }

        )
        {
            var first = new LayoutAnchorable
            {
                ContentId = "first",
                Title = "First"
            };
            var second = new LayoutAnchorable
            {
                ContentId = "second",
                Title = "Second",
                CanAutoHide = scenario != "ineligible-sibling"
            };
            var third = new LayoutAnchorable
            {
                ContentId = "third",
                Title = "Third"
            };
            var pane = new LayoutAnchorablePane(first);
            pane.Children.Add(second);
            pane.Children.Add(third);
            var rootPanel = new LayoutPanel(pane);
            rootPanel.Children.Add(new LayoutDocumentPane(new LayoutDocument { ContentId = "doc" }));
            var manager = new DockingManager
            {
                Layout = new LayoutRoot
                {
                    RootPanel = rootPanel
                }
            };
            first.ToggleAutoHide();
            if (scenario == "two" || scenario == "pin-one")
                second.ToggleAutoHide();
            if (scenario == "pin-one")
                first.ToggleAutoHide();
            var result = new XElement("AutoHide", new XAttribute("scenario", scenario), new XElement("Docked", string.Join(",", pane.Children.Select(c => c.ContentId))), manager.Layout.LeftSide.Children.Select(g => new XElement("Group", string.Join(",", g.Children.Select(c => c.ContentId)))));
            new XDocument(result).Save(Path.Combine(output, "behavior-" + scenario + ".xml"));
        }
    }
}
