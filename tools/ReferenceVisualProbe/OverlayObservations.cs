// Independent public-API and Win32-message observation of application-owned layouts.
// Hosted-desktop pointer attempts did not establish a drag session; this probe records
// its narrower message-injection method explicitly and does not claim pointer parity.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
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
        _ = owner.Dispatcher.BeginInvoke(new Action(async () =>
        {
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
                    var point = target.PointToScreen(new Point(target.ActualWidth / 2 + 90, target.ActualHeight / 2 + 90));
                    var name = tool ? "guides-tool" : "guides-document";
                    try
                    {
                        OverlayMessageObservation.TryObserve(floating, point, output, name);
                        if (!File.Exists(Path.Combine(output, name + "-message.xml"))) throw new InvalidOperationException("Native-message probe did not realize the reference overlay.");
                    }
                    finally { content.Dock(); }
                    await Task.Delay(100);
                }
            }
            catch (Exception error) { failure = error; }
            finally { frame.Continue = false; }
        }));
        Dispatcher.PushFrame(frame);
        if (failure != null) throw new InvalidOperationException("Public overlay observation failed.", failure);
    }
    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        { var child = VisualTreeHelper.GetChild(parent, i); yield return child; foreach (var nested in Descendants(child)) yield return nested; }
    }
}
