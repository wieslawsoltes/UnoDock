// Independent black-box probes. Application-owned content, public model/window APIs,
// native pointer input and public arranged rectangles only; no template/path extraction.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Xml.Linq;
using Xceed.Wpf.AvalonDock;
using Xceed.Wpf.AvalonDock.Controls;
using Xceed.Wpf.AvalonDock.Layout;

internal static class OverlayObservations
{
    public static void Run(DockingManager manager, Window owner, string output)
    {
        if (Environment.GetEnvironmentVariable("UNODOCK_REFERENCE_INPUT") != "1") return;
        Exception failure = null;
        var frame = new DispatcherFrame();
        owner.Dispatcher.BeginInvoke(new Action(async () =>
        {
            GetCursorPos(out var previous);
            try
            {
                owner.Left = 20; owner.Top = 20;
                foreach (var tool in new[] { false, true })
                {
                    var doc = new LayoutDocument { Title = "Dragged document", ContentId = "dragged", Content = new TextBox { Text = "Owned content" } };
                    var docs = new LayoutDocumentPane(new LayoutDocument { Title = "Target", ContentId = "target", Content = new TextBox { Text = "Target editor" } }); docs.Children.Add(doc);
                    var anchor = new LayoutAnchorable { Title = "Dragged tool", ContentId = "tool", Content = new TextBox { Text = "Owned tool" } };
                    var tools = new LayoutAnchorablePane(anchor) { DockWidth = new GridLength(200) }; tools.Children.Add(new LayoutAnchorable { Title = "Other tool", ContentId = "other" });
                    var panel = new LayoutPanel(tools); panel.Children.Add(docs);
                    manager.FlowDirection = FlowDirection.LeftToRight; manager.Layout = new LayoutRoot { RootPanel = panel };
                    var content = tool ? (LayoutContent)anchor : doc;
                    content.FloatingLeft = 40; content.FloatingTop = 60; content.FloatingWidth = 280; content.FloatingHeight = 180;
                    content.Float(); manager.UpdateLayout(); await Task.Delay(250);
                    var floating = manager.FloatingWindows.Single(); floating.Left = 40; floating.Top = 60; floating.Width = 280; floating.Height = 180;
                    floating.Activate(); floating.UpdateLayout(); await Task.Delay(150);
                    var target = Descendants(manager).OfType<LayoutDocumentPaneControl>().First(e => ReferenceEquals(e.Model, docs));
                    var start = floating.PointToScreen(new Point(20, 8));
                    var end = target.PointToScreen(new Point(target.ActualWidth / 2 + 90, target.ActualHeight / 2 + 90));
                    Console.WriteLine($"Observed drag start={start}; end={end}; floating={floating.IsVisible}; target={target.IsVisible}");
                    SetCursorPos((int)start.X, (int)start.Y); MouseEvent(2, 0, 0, 0, UIntPtr.Zero); await Task.Delay(80);
                    // DragMove is a public Window method. Its nested native message loop
                    // allows delayed pointer moves and public visual observations below.
                    var ended = new TaskCompletionSource<bool>();
                    floating.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        try { floating.DragMove(); ended.TrySetResult(true); }
                        catch (Exception error) { ended.TrySetException(error); }
                    }));
                    await Task.Delay(150);
                    for (var step = 1; step <= 12; step++)
                    {
                        SetCursorPos((int)(start.X + (end.X - start.X) * step / 12), (int)(start.Y + (end.Y - start.Y) * step / 12));
                        await Task.Delay(35);
                    }
                    await Task.Delay(250);
                    var overlays = Application.Current.Windows.OfType<OverlayWindow>().Where(w => w.IsVisible).ToArray();
                    Console.WriteLine("Visible windows: " + string.Join(", ", Application.Current.Windows.OfType<Window>().Select(w => w.GetType().Name + ":" + w.IsVisible)));
                    if (overlays.Length == 0) throw new InvalidOperationException("Public DragMove did not realize the reference overlay.");
                    var index = 0;
                    foreach (var overlay in overlays)
                    {
                        overlay.UpdateLayout();
                        var root = VisualTreeHelper.GetChildrenCount(overlay) > 0 ? VisualTreeHelper.GetChild(overlay, 0) as FrameworkElement : null;
                        if (root == null) throw new InvalidOperationException("Reference overlay has no arranged client root.");
                        var name = (tool ? "guides-tool" : "guides-document") + (index++ == 0 ? "" : "-" + index);
                        var bmp = new RenderTargetBitmap((int)Math.Ceiling(root.ActualWidth), (int)Math.Ceiling(root.ActualHeight), 96, 96, PixelFormats.Pbgra32);
                        bmp.Render(root); var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bmp));
                        using (var file = File.Create(Path.Combine(output, name + ".png"))) png.Save(file);
                        var xml = new XElement("Overlay", new XAttribute("name", name), new XAttribute("width", root.ActualWidth), new XAttribute("height", root.ActualHeight));
                        foreach (var element in Descendants(root).OfType<FrameworkElement>().Where(e => e.IsVisible && e.ActualWidth > 0 && e.ActualHeight > 0))
                        {
                            var bounds = element.TransformToAncestor(root).TransformBounds(new Rect(0, 0, element.ActualWidth, element.ActualHeight));
                            xml.Add(new XElement("Element", new XAttribute("type", element.GetType().Name), new XAttribute("name", element.Name ?? ""),
                                new XAttribute("x", bounds.X), new XAttribute("y", bounds.Y), new XAttribute("width", bounds.Width), new XAttribute("height", bounds.Height)));
                        }
                        new XDocument(xml).Save(Path.Combine(output, name + ".xml")); Console.WriteLine("Captured public drag overlay: " + name);
                    }
                    KeyEvent(0x1B, 0, 0, UIntPtr.Zero); KeyEvent(0x1B, 0, 2, UIntPtr.Zero); MouseEvent(4, 0, 0, 0, UIntPtr.Zero);
                    if (await Task.WhenAny(ended.Task, Task.Delay(1500)) != ended.Task) throw new TimeoutException("Native drag loop did not terminate.");
                    await ended.Task; content.Dock(); await Task.Delay(100);
                }
            }
            catch (Exception error) { failure = error; }
            finally { MouseEvent(4, 0, 0, 0, UIntPtr.Zero); SetCursorPos(previous.X, previous.Y); frame.Continue = false; }
        }));
        Dispatcher.PushFrame(frame);
        if (failure != null) throw new InvalidOperationException("Public overlay input observation failed.", failure);
    }
    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        { var child = VisualTreeHelper.GetChild(parent, i); yield return child; foreach (var nested in Descendants(child)) yield return nested; }
    }
    [StructLayout(LayoutKind.Sequential)] private struct ScreenPoint { public int X, Y; }
    [DllImport("user32.dll")] private static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out ScreenPoint point);
    [DllImport("user32.dll", EntryPoint = "mouse_event")] private static extern void MouseEvent(uint flags, uint x, uint y, uint data, UIntPtr extra);
    [DllImport("user32.dll", EntryPoint = "keybd_event")] private static extern void KeyEvent(byte key, byte scan, uint flags, UIntPtr extra);
}
