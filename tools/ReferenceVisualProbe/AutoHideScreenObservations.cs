// Black-box screen observations. Capture public displayed pixels, not native
// window internals, template definitions, resources, fonts or implementation IL.
using System;
using System.Drawing;
using System.Drawing.Imaging;
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
using Point = System.Windows.Point;

internal static class AutoHideScreenObservations
{
    public static void Run(DockingManager manager, string directory)
    {
        var owner = Window.GetWindow(manager);
        owner.Left = owner.Top = 20; owner.Activate();
        var observations = new XElement("AutoHideScreens", new XAttribute("method", "screen-copy-of-public-flyout-bounds-after-public-mouse-enter-not-native-pointer-acceptance"));
        foreach (var side in new[] { AnchorableShowStrategy.Left, AnchorableShowStrategy.Right, AnchorableShowStrategy.Top, AnchorableShowStrategy.Bottom })
        {
            manager.FlowDirection = FlowDirection.LeftToRight;
            var doc = new LayoutDocument { ContentId = "editor", Title = "Workspace.cs", Content = "Application-owned document" };
            manager.Layout = new LayoutRoot { RootPanel = new LayoutPanel(new LayoutDocumentPane(doc)) };
            var tool = new LayoutAnchorable { ContentId = "tool", Title = "Solution Explorer", AutoHideWidth = 320, AutoHideHeight = 320,
                Content = new System.Windows.Controls.TextBox { Text = "Solution\n  Sources\n    Workspace.cs\n    Layout.cs\n  Tests\n    AutoHideQuality.cs", AcceptsReturn = true, Padding = new Thickness(10), BorderThickness = new Thickness(0) } };
            tool.AddToLayout(manager, side | AnchorableShowStrategy.Most);
            manager.UpdateLayout(); tool.ToggleAutoHide(); doc.IsActive = true; manager.UpdateLayout();
            var anchor = Walk(manager).OfType<LayoutAnchorControl>().Single(c => ReferenceEquals(c.Model, tool));
            anchor.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, Environment.TickCount) { RoutedEvent = Mouse.MouseEnterEvent });
            var frame = new DispatcherFrame(); var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(650) };
            timer.Tick += (_, __) => { timer.Stop(); frame.Continue = false; };
            timer.Start(); Dispatcher.PushFrame(frame); manager.UpdateLayout();
            var flyout = manager.AutoHideWindow;
            if (flyout == null || !flyout.IsVisible) throw new InvalidOperationException("Reference auto-hide view was not revealed.");
            var first = flyout.PointToScreen(new Point());
            var last = flyout.PointToScreen(new Point(flyout.ActualWidth, flyout.ActualHeight));
            var x = (int)Math.Floor(first.X); var y = (int)Math.Floor(first.Y);
            var width = (int)Math.Ceiling(last.X) - x; var height = (int)Math.Ceiling(last.Y) - y;
            if (width <= 0 || height <= 0) throw new InvalidOperationException("Empty public flyout screen bounds.");
            var name = "auto-hide-screen-" + side.ToString().ToLowerInvariant();
            using (var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb))
            {
                using (var graphics = Graphics.FromImage(bitmap)) graphics.CopyFromScreen(x, y, 0, 0, new System.Drawing.Size(width, height), CopyPixelOperation.SourceCopy);
                bitmap.Save(Path.Combine(directory, name + ".png"), ImageFormat.Png);
            }
            observations.Add(new XElement("Window", new XAttribute("name", name), new XAttribute("side", side),
                new XAttribute("xPixels", x), new XAttribute("yPixels", y), new XAttribute("widthPixels", width), new XAttribute("heightPixels", height),
                new XAttribute("width", flyout.ActualWidth.ToString("R", CultureInfo.InvariantCulture)),
                new XAttribute("height", flyout.ActualHeight.ToString("R", CultureInfo.InvariantCulture)), new XAttribute("active", tool.IsActive)));
            tool.ToggleAutoHide(); manager.UpdateLayout();
        }
        new XDocument(observations).Save(Path.Combine(directory, "auto-hide-screen-observations.xml"));
        Console.WriteLine("Captured four public auto-hide screen views; screen-copy evidence is not native input acceptance.");
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
