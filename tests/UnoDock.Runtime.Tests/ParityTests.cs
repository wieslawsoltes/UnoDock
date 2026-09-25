using System.Collections;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation.Provider;
using Windows.Foundation;
using UnoDock.Core;
using UnoDock;
using UnoDock.Controls;
using UnoDock.Layout;

namespace UnoDock.Testing;

using LayoutPanel = UnoDock.Layout.LayoutPanel;

public static class ParityTests
{
    public static async Task<int> Run(DockingManager host, string output)
    {
        var tests = new TestRunner();
        foreach (var type in Enum.GetValues<DropTargetType>())
        {
            tests.Test("execute declared drop target: " + type, () =>
            {
                using var manager = new DockingManager
                {
                    AllowMixedOrientation = true
                };
                var documents = new LayoutDocumentPane(new LayoutDocument { ContentId = "existing" });
                var tools = new LayoutAnchorablePane(new LayoutAnchorable { ContentId = "existing-tool" });
                var empty = new LayoutDocumentPaneGroup();
                // Tool content intentionally exercises document-kind splits as well.
                var content = new LayoutAnchorable
                {
                    ContentId = "source"
                };
                manager.Layout = Root(documents, tools, empty, new LayoutAnchorablePane(content));
                ILayoutGroup target = (int)type <= 3 ? manager.Layout.RootPanel : type == DropTargetType.DocumentPaneGroupDockInside ? empty : type is >= DropTargetType.AnchorablePaneDockLeft and <= DropTargetType.AnchorablePaneDockInside ? tools : documents;
                var plan = DockDropPlan.Create(content, target, type, new(0, 0, 800, 600));
                Check.True(plan != null, "Target was rejected: " + type);
                Check.True(plan!.CanExecute);
                Check.True(plan.Execute());
                Check.Same(manager.Layout, content.Root);
                AssertTree(manager.Layout);
                if (type is >= DropTargetType.DocumentPaneDockLeft and <= DropTargetType.DocumentPaneGroupDockInside)
                    Check.True(content.Parent is LayoutDocumentPane, "Tool was not inserted as a tabbed document.");
                else
                    Check.True(content.Parent is LayoutAnchorablePane);
            });
        }

        tests.Test("drop rejects invalid enum and bounds", () =>
        {
            using var m = Workspace(out var content, out var target);
            Check.Throws<ArgumentOutOfRangeException>(() => DockDropPlan.Create(content, target, (DropTargetType)999, new(0, 0, 10, 10)));
            Check.True(DockDropPlan.Create(content, target, DropTargetType.DocumentPaneDockInside, new(0, 0, 0, 10)) == null);
        });
        tests.Test("plan rejects replaced manager root", () =>
        {
            using var m = Workspace(out var content, out var target);
            var plan = Plan(content, target);
            var old = m.Layout;
            m.Layout = new();
            Check.False(plan.CanExecute);
            Check.False(plan.Execute());
            Check.Same(old, content.Root);
        });
        tests.Test("plan rejects changed movement policy", () =>
        {
            using var m = Workspace(out var content, out var target);
            var plan = Plan(content, target);
            content.CanMove = false;
            Check.False(plan.CanExecute);
            Check.False(plan.Execute());
        });
        tests.Test("plan rejects detached target", () =>
        {
            using var m = Workspace(out var content, out var target);
            var plan = Plan(content, target);
            target.Parent!.RemoveChild(target);
            Check.False(plan.Execute());
        });
        tests.Test("plan rejects immutable floating content", () =>
        {
            using var m = Workspace(out var content, out var target);
            m.CreateFloatingWindow(content, true);
            Check.True(DockDropPlan.Create(content, target, DropTargetType.DocumentPaneDockInside, new(0, 0, 100, 100)) == null);
        });
        tests.Test("duplicate drop uses title and content ID", () =>
        {
            using var m = Workspace(out var content, out var target);
            content.ContentId = "duplicate";
            content.Title = "same";
            target.Children[0].ContentId = "duplicate";
            target.Children[0].Title = "same";
            target.AllowDuplicateContent = false;
            Check.True(DockDropPlan.Create(content, target, DropTargetType.DocumentPaneDockInside, new(0, 0, 100, 100)) == null);
            content.Title = "different";
            Check.True(Plan(content, target).Execute());
        });
        tests.Test("document-docking disabled tool has no document edge plan", () =>
        {
            using var m = Workspace(out _, out var target);
            var tool = new LayoutAnchorable
            {
                CanDockAsTabbedDocument = false
            };
            m.Layout.RootPanel.Children.Add(new LayoutAnchorablePane(tool));
            Check.True(DockDropPlan.Create(tool, target, DropTargetType.DocumentPaneDockLeft, new(0, 0, 100, 100)) == null);
            Check.True(DockDropPlan.Create(tool, target, DropTargetType.DocumentPaneDockAsAnchorableLeft, new(0, 0, 100, 100))!.Execute());
        });
        tests.Test("preview handler cannot remove target then commit", () =>
        {
            using var m = Workspace(out var content, out var target);
            var before = content.Parent;
            m.PreviewDock += (_, _) => target.Parent!.RemoveChild(target);
            Check.False(Plan(content, target).Execute());
            Check.Same(before, content.Parent);
        });
        tests.Test("overlay previews and cancellation", () =>
        {
            using var m = Workspace(out var content, out var target);
            var overlay = new CancelOverlay();
            overlay.ShowPreview(Plan(content, target), new SolidColorBrush(Microsoft.UI.Colors.Blue));
            Check.True(overlay.IsOpen);
            overlay.Close();
            Check.True(overlay.IsOpen);
            overlay.Hide();
            Check.False(overlay.IsOpen);
        });
        tests.Test("overlay area hit testing", () =>
        {
            var area = new DocumentPaneControlOverlayArea(new(10, 20, 40, 50));
            Check.True(area.HitTest(new(15, 25)));
            Check.False(area.HitTest(new(0, 0)));
        });
        tests.Test("menu container factory and source regeneration", () =>
        {
            var menu = new MenuProbe
            {
                ItemsSource = new[]
                {
                    "one",
                    "two"
                }
            };
            menu.PrepareItems();
            Check.Equal(2, menu.Generated);
            Check.Equal("two", ((MenuFlyoutItem)menu.Items[1]).Text);
            menu.ItemsSource = new[]
            {
                "three"
            };
            menu.PrepareItems();
            Check.Equal(1, menu.Items.Count);
        });
        tests.Test("menu iterator failure preserves previous items", () =>
        {
            var menu = new ContextMenuEx();
            var existing = new MenuFlyoutItem
            {
                Text = "keep"
            };
            menu.Items.Add(existing);
            menu.ItemsSource = ThrowingSequence();
            Check.Throws<InvalidOperationException>(menu.PrepareItems);
            Check.Same(existing, menu.Items.Single());
        });
        tests.Test("menu icon templates materialize IconElement", () =>
        {
            var item = new MenuItemEx
            {
                IconTemplate = (DataTemplate)XamlReader.Load("<DataTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'><FontIcon Glyph='A'/></DataTemplate>")
            };
            Check.True(item.Icon is FontIcon);
            var data = new object();
            item.DataContext = data;
            Check.Same(data, item.Icon.DataContext);
        });
        tests.Test("menu temporary context preserves local contexts", () =>
        {
            var local = new object();
            var context = new object();
            var menu = new ContextMenuEx();
            var first = new MenuFlyoutItem();
            var second = new MenuFlyoutItem
            {
                DataContext = local
            };
            menu.Items.Add(first);
            menu.Items.Add(second);
            menu.MenuDataContext = context;
            Check.Same(context, first.DataContext);
            Check.Same(local, second.DataContext);
            var changed = new object();
            first.DataContext = changed;
            menu.MenuDataContext = null;
            Check.Same(changed, first.DataContext);
        });
        tests.Test("logical traversal before realization", () =>
        {
            var child = new TextBlock();
            var root = new ContentControl
            {
                Content = new Border
                {
                    Child = child
                }
            };
            Check.Same(child, root.FindLogicalChildren<TextBlock>().Single());
            Check.Equal(0, root.FindVisualChildren<TextBlock>().Count());
        });
        tests.Test("tab automation selects and invokes", () =>
        {
            using var m = Workspace(out var content, out _);
            var tab = new LayoutDocumentTabItem
            {
                Model = content
            };
            var peer = new LayoutTabAutomationPeer(tab);
            ((ISelectionItemProvider)peer).Select();
            Check.True(content.IsSelected);
            Check.Same(content, m.Layout.ActiveContent);
            ((IInvokeProvider)peer).Invoke();
            Check.True(peer.IsSelected);
            content.IsEnabled = false;
            Check.Throws<InvalidOperationException>(() => peer.Select());
        });
        tests.Test("arranged drop areas and editor accessibility", () =>
        {
            host.Layout = Root(new LayoutDocumentPane(new LayoutDocument { ContentId = "view", Content = new TextBox() }));
            host.Refresh();
            host.UpdateLayout();
            Check.True(host.GetDropAreas().Any(a => a.Type == DropAreaType.DocumentPane && a.DetectionRect.Width > 0));
            var pane = host.FindVisualChildren<LayoutDocumentPaneControl>().Single();
            var peer = new LayoutPaneAutomationPeer(pane);
            Check.Equal(1, peer.GetSelection().Length);
            Check.False(peer.CanSelectMultiple);
        });
        tests.Test("tab waterline solver preserves short tabs", () =>
        {
            var widths = TabStripSolver.Allocate(130, [20, 200, 200]);
            Check.Near(20, widths[0]);
            Check.Near(55, widths[1]);
            Check.Near(55, widths[2]);
        });
        tests.Test("tab solver validates all insertion widths", () =>
        {
            Check.Throws<ArgumentOutOfRangeException>(() => TabStripSolver.InsertionIndex(-100, [20, double.NaN]));
        });
        tests.Test("tab waterline solver randomized conservation (5000 cases)", () =>
        {
            var random = new Random(14751);
            for (var k = 0; k < 5000; k++)
            {
                var desired = Enumerable.Range(0, random.Next(1, 40)).Select(_ => random.NextDouble() * 1000).ToArray();
                var available = random.NextDouble() * desired.Sum() * 1.5;
                var actual = TabStripSolver.Allocate(available, desired);
                Check.Near(Math.Min(available, desired.Sum()), actual.Sum(), 1e-6);
                for (var i = 0; i < actual.Length; i++)
                    Check.True(actual[i] >= 0 && actual[i] <= desired[i]);
            }
        });
        tests.Test("unrelated pointer cannot terminate drag", () =>
        {
            var session = new DockDragSession();
            session.Arm(1, new(0, 0), true);
            session.Move(1, new(20, 0), []);
            Check.False(session.OwnsPointer(2));
            Check.False(session.Commit(2));
            Check.Equal(DockDragState.Dragging, session.State);
            Check.True(session.Commit(1));
        });
        var original = host.Layout;
        try
        {
            return await tests.Run(output, "parity");
        }
        finally
        {
            host.Layout = original;
            host.Refresh();
        }
    }

    private sealed class CancelOverlay : OverlayWindow
    {
        protected override void OnClosing(System.ComponentModel.CancelEventArgs e) => e.Cancel = true;
    }

    private sealed class MenuProbe : ContextMenuEx
    {
        public int Generated;
        protected override DependencyObject GetContainerForItemOverride()
        {
            Generated++;
            return new MenuItemEx();
        }
    }

    private static IEnumerable ThrowingSequence()
    {
        yield return "partial";
        throw new InvalidOperationException("iterator");
    }

    private static DockDropPlan Plan(LayoutContent c, LayoutDocumentPane p) => DockDropPlan.Create(c, p, DropTargetType.DocumentPaneDockInside, new(0, 0, 800, 600))!;
    private static DockingManager Workspace(out LayoutDocument source, out LayoutDocumentPane target)
    {
        source = new LayoutDocument
        {
            ContentId = "source"
        };
        target = new(new LayoutDocument { ContentId = "target" });
        return new()
        {
            Layout = Root(new LayoutDocumentPane(source), target)
        };
    }

    private static LayoutRoot Root(params ILayoutPanelElement[] children)
    {
        var panel = new LayoutPanel();
        foreach (var child in children)
            panel.Children.Add(child);
        return new()
        {
            RootPanel = panel
        };
    }

    private static void AssertTree(LayoutRoot root)
    {
        var seen = new HashSet<ILayoutElement>(ReferenceEqualityComparer.Instance);
        foreach (var child in root.Descendents())
        {
            Check.True(seen.Add(child));
            Check.Same(root, child.Root);
            Check.True(child.Parent!.Children.Contains(child));
        }
    }
}
