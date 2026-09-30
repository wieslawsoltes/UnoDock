using System.Text;
using Microsoft.UI.Xaml.Controls;
using UnoDock;
using UnoDock.Controls;
using UnoDock.Layout;
using UnoDock.Layout.Serialization;

namespace UnoDock.Testing;

using LayoutPanel = UnoDock.Layout.LayoutPanel;

/// <summary>Layout operations checked against layout trees recorded from the
/// reference implementation driven through its public API (same layouts, same
/// calls). Expected trees use the recorded dump format.</summary>
public static class ReferenceBehaviorTests
{
    public static async Task<int> Run(string output)
    {
        var tests = new TestRunner();
        tests.Test("auto-hide moves only the toggled tool and restore appends it", () =>
        {
            using var f = new Fixture(Standard);
            var output = f.Tool("output");
            var errors = f.Tool("errors");
            output.ToggleAutoHide();
            Check.True(output.Parent is LayoutAnchorGroup && errors.Parent is LayoutAnchorablePane);
            Check.Equal("Panel(H)[AnchorablePane[Anchorable:explorer Anchorable:toolbox] Panel(V)[DocumentPaneGroup(H)[DocumentPane[Document:d1 Document:d2 Document:d3]] AnchorablePane[Anchorable:errors]] AnchorablePane[Anchorable:props]] side:AnchorSide[AnchorGroup[Anchorable:output]]", Dump(f.Root));
            output.ToggleAutoHide();
            Check.Equal("Panel(H)[AnchorablePane[Anchorable:explorer Anchorable:toolbox] Panel(V)[DocumentPaneGroup(H)[DocumentPane[Document:d1 Document:d2 Document:d3]] AnchorablePane[Anchorable:errors Anchorable:output]] AnchorablePane[Anchorable:props]]", Dump(f.Root));
        });
        tests.Test("closing a floating tool window hides its tools and Show floats them again", () =>
        {
            using var f = new Fixture(Standard);
            var output = f.Tool("output");
            output.Float();
            var window = f.Root.FloatingWindows.Single();
            output.Hide();
            Check.True(output.IsHidden && !output.IsVisible);
            Check.Equal(1, f.Root.FloatingWindows.Count);
            Check.True(output.PreviousContainer is LayoutAnchorablePane);
            output.Show();
            Check.True(output.IsFloating);
            Check.Same(window, f.Root.FloatingWindows.Single());
            Check.True(output.Parent is LayoutAnchorablePane);
        });
        tests.Test("new vertical tab group wraps a panel-level document pane in a group", () =>
        {
            using var f = new Fixture(Flat);
            var second = f.Document("d2");
            var item = (LayoutDocumentItem)f.Manager.GetLayoutItemFromModel(second);
            Check.True(item.NewVerticalTabGroupCommand!.CanExecute(null));
            item.NewVerticalTabGroupCommand!.Execute(null);
            Check.Equal("Panel(H)[AnchorablePane[Anchorable:explorer] DocumentPaneGroup(H)[DocumentPane[Document:d1 Document:d3] DocumentPane[Document:d2]]]", Dump(f.Root));
            var first = (LayoutDocumentItem)f.Manager.GetLayoutItemFromModel(f.Document("d1"));
            Check.True(first.MoveToNextTabGroupCommand!.CanExecute(null));
            Check.True(item.MoveToPreviousTabGroupCommand!.CanExecute(null));
            first.MoveToNextTabGroupCommand!.Execute(null);
            Check.Equal("Panel(H)[AnchorablePane[Anchorable:explorer] DocumentPaneGroup(H)[DocumentPane[Document:d3] DocumentPane[Document:d1 Document:d2]]]", Dump(f.Root));
        });
        tests.Test("new horizontal tab group reorients a single-pane group", () =>
        {
            using var f = new Fixture(Standard);
            ((LayoutDocumentItem)f.Manager.GetLayoutItemFromModel(f.Document("d2"))).NewHorizontalTabGroupCommand!.Execute(null);
            Check.Equal("Panel(H)[AnchorablePane[Anchorable:explorer Anchorable:toolbox] Panel(V)[DocumentPaneGroup(V)[DocumentPane[Document:d1 Document:d3] DocumentPane[Document:d2]] AnchorablePane[Anchorable:output Anchorable:errors]] AnchorablePane[Anchorable:props]]", Dump(f.Root));
        });
        tests.Test("closing a split's documents removes its pane and keeps the last empty pane", () =>
        {
            using var f = new Fixture(Flat);
            var second = f.Document("d2");
            ((LayoutDocumentItem)f.Manager.GetLayoutItemFromModel(second)).NewVerticalTabGroupCommand!.Execute(null);
            second.Close();
            Check.Equal("Panel(H)[AnchorablePane[Anchorable:explorer] DocumentPaneGroup(H)[DocumentPane[Document:d1 Document:d3]]]", Dump(f.Root));
            foreach (var document in f.Root.Descendents().OfType<LayoutDocument>().ToArray())
                document.Close();
            Check.Equal("Panel(H)[AnchorablePane[Anchorable:explorer] DocumentPaneGroup(H)[DocumentPane[]]]", Dump(f.Root));
        });
        tests.Test("closing the first split's documents removes that pane", () =>
        {
            using var f = new Fixture(Flat);
            var second = f.Document("d2");
            ((LayoutDocumentItem)f.Manager.GetLayoutItemFromModel(second)).NewVerticalTabGroupCommand!.Execute(null);
            f.Document("d1").Close();
            f.Document("d3").Close();
            Check.Equal("Panel(H)[AnchorablePane[Anchorable:explorer] DocumentPaneGroup(H)[DocumentPane[Document:d2]]]", Dump(f.Root));
        });
        tests.Test("a tool docked as document keeps no previous position; Dock uses the default side", () =>
        {
            using var f = new Fixture(Standard);
            var output = f.Tool("output");
            output.DockAsDocument();
            Check.True(output.Parent is LayoutDocumentPane && output.PreviousContainer == null);
            Check.False(((LayoutAnchorableItem)f.Manager.GetLayoutItemFromModel(output)).DockCommand!.CanExecute(null));
            output.Dock();
            Check.Equal("Panel(H)[AnchorablePane[Anchorable:explorer Anchorable:toolbox] Panel(V)[DocumentPaneGroup(H)[DocumentPane[Document:d1 Document:d2 Document:d3]] AnchorablePane[Anchorable:errors]] AnchorablePane[Anchorable:props Anchorable:output]]", Dump(f.Root));
        });
        tests.Test("a floated document docks as document into its previous pane and index", () =>
        {
            using var f = new Fixture(Flat);
            ((LayoutDocumentItem)f.Manager.GetLayoutItemFromModel(f.Document("d2"))).NewVerticalTabGroupCommand!.Execute(null);
            var third = f.Document("d3");
            third.Float();
            third.DockAsDocument();
            var pane = (LayoutDocumentPane)third.Parent!;
            Check.Equal("d1,d3", string.Join(",", pane.Children.Select(c => c.ContentId)));
            Check.Equal(1, pane.IndexOf(third));
        });
        tests.Test("CanRepositionItems does not prevent float, dock-as-document or auto-hide", () =>
        {
            using var f = new Fixture(Standard);
            var output = f.Tool("output");
            ((LayoutAnchorablePane)output.Parent!).CanRepositionItems = false;
            var item = (LayoutAnchorableItem)f.Manager.GetLayoutItemFromModel(output);
            Check.True(item.FloatCommand!.CanExecute(null) && item.DockAsDocumentCommand!.CanExecute(null) && item.AutoHideCommand!.CanExecute(null) && item.HideCommand!.CanExecute(null));
            output.Float();
            Check.True(output.IsFloating);
        });
        tests.Test("CanRepositionItems prevents reordering within the pane", () =>
        {
            using var f = new Fixture(Standard);
            var output = f.Tool("output");
            var pane = (LayoutAnchorablePane)output.Parent!;
            pane.CanRepositionItems = false;
            DockOperations.Dock(output, pane, DockPosition.Inside, 2);
            Check.Equal(0, pane.IndexOf(output));
        });
        tests.Test("floating tools cannot auto-hide but can dock, hide and dock as document", () =>
        {
            using var f = new Fixture(Standard);
            var output = f.Tool("output");
            output.Float();
            var item = (LayoutAnchorableItem)f.Manager.GetLayoutItemFromModel(output);
            Check.False(item.AutoHideCommand!.CanExecute(null));
            Check.True(item.DockCommand!.CanExecute(null) && item.HideCommand!.CanExecute(null) && item.DockAsDocumentCommand!.CanExecute(null));
        });
        tests.Test("tab group moves require an adjacent sibling document pane", () =>
        {
            using var f = new Fixture(manager =>
            {
                var group = new LayoutDocumentPaneGroup
                {
                    Orientation = Orientation.Horizontal
                };
                var first = new LayoutDocumentPane(Doc("a"));
                first.Children.Add(Doc("a2"));
                var inner = new LayoutDocumentPaneGroup
                {
                    Orientation = Orientation.Vertical
                };
                inner.Children.Add(new LayoutDocumentPane(Doc("b")));
                inner.Children.Add(new LayoutDocumentPane(Doc("c")));
                group.Children.Add(first);
                group.Children.Add(inner);
                group.Children.Add(new LayoutDocumentPane(Doc("d")));
                return new LayoutRoot
                {
                    RootPanel = new(group)
                };
            });
            Check.False(((LayoutDocumentItem)f.Manager.GetLayoutItemFromModel(f.Document("a"))).MoveToNextTabGroupCommand!.CanExecute(null));
            Check.False(((LayoutDocumentItem)f.Manager.GetLayoutItemFromModel(f.Document("d"))).MoveToPreviousTabGroupCommand!.CanExecute(null));
        });
        tests.Test("deselecting the selected content clears the pane selection", () =>
        {
            using var f = new Fixture(Standard);
            var first = f.Document("d1");
            first.IsSelected = true;
            first.IsSelected = false;
            Check.Equal(-1, ((LayoutDocumentPane)first.Parent!).SelectedContentIndex);
        });
        tests.Test("restoring without content hides tools and drops documents", () =>
        {
            using var f = new Fixture(Standard);
            var serializer = new XmlLayoutSerializer(f.Manager);
            using var saved = new StringWriter();
            serializer.Serialize(saved);
            f.Manager.Layout = new LayoutRoot
            {
                RootPanel = new(new LayoutDocumentPane(Doc("d1")))
            };
            using (var reader = new StringReader(saved.ToString()))
                new XmlLayoutSerializer(f.Manager).Deserialize(reader);
            var root = f.Manager.Layout;
            Check.Equal("explorer,toolbox,output,errors,props", string.Join(",", root.Hidden.Select(h => h.ContentId)));
            Check.Equal("d1", string.Join(",", root.Descendents().OfType<LayoutDocument>().Select(d => d.ContentId)));
        });
        tests.Test("restore keeps presentation values set by the callback", () =>
        {
            using var f = new Fixture(Standard);
            var serializer = new XmlLayoutSerializer(f.Manager);
            using var saved = new StringWriter();
            serializer.Serialize(saved);
            var restore = new XmlLayoutSerializer(f.Manager);
            restore.LayoutSerializationCallback += (_, e) =>
            {
                e.Content ??= e.Model.ContentId;
                e.Model.ToolTip = "from callback";
            };
            using (var reader = new StringReader(saved.ToString()))
                restore.Deserialize(reader);
            Check.True(f.Manager.Layout.Descendents().OfType<LayoutContent>().All(c => Equals(c.ToolTip, "from callback")));
        });
        tests.Test("Show calls the layout update strategy with the previous container", () =>
        {
            using var f = new Fixture(Standard);
            var strategy = new CountingStrategy();
            f.Manager.LayoutUpdateStrategy = strategy;
            var output = f.Tool("output");
            var pane = output.Parent;
            output.Hide();
            output.Show();
            Check.Equal(1, strategy.Before);
            Check.Equal(1, strategy.After);
            Check.Same(pane, strategy.Destination);
            Check.Same(pane, output.Parent);
            Check.True(output.PreviousContainer == null);
        });
        tests.Test("chrome close runs the item's command and honors CanExecute", () =>
        {
            using var f = new Fixture(Standard);
            var document = f.Document("d1");
            var item = f.Manager.GetLayoutItemFromModel(document);
            var calls = 0;
            var allowed = false;
            item.CloseCommand = new UnoDock.Internal.DelegateCommand(_ => calls++, _ => allowed);
            CloseOrHide(document);
            Check.Equal(0, calls);
            allowed = true;
            CloseOrHide(document);
            Check.Equal(1, calls);
            Check.Same(f.Root, document.Root);
        });
        tests.Test("close all runs each document's close command", () =>
        {
            using var f = new Fixture(Standard);
            var calls = new List<string>();
            foreach (var document in f.Root.Descendents().OfType<LayoutDocument>().ToArray())
            {
                var captured = document;
                f.Manager.GetLayoutItemFromModel(document).CloseCommand = new UnoDock.Internal.DelegateCommand(_ =>
                {
                    calls.Add(captured.ContentId!);
                    captured.Close();
                });
            }

            var first = f.Manager.GetLayoutItemFromModel(f.Document("d1"));
            first.CloseAllCommand!.Execute(null);
            Check.Equal("d1,d2,d3", string.Join(",", calls));
        });
        tests.Test("tool item visibility follows Hide and Show in both directions", () =>
        {
            using var f = new Fixture(Standard);
            var output = f.Tool("output");
            var item = f.Manager.GetLayoutItemFromModel(output);
            output.Hide();
            Check.Equal(Visibility.Collapsed, item.Visibility);
            output.Show();
            Check.Equal(Visibility.Visible, item.Visibility);
            item.Visibility = Visibility.Collapsed;
            Check.True(output.IsHidden);
            item.Visibility = Visibility.Visible;
            Check.True(output.IsVisible);
        });
        return await tests.Run(output, "reference-behavior");
    }

    // The chrome close button's action (internal to the library).
    private static void CloseOrHide(LayoutContent content) => typeof(DockingManager).Assembly.GetType("UnoDock.Internal.DockVisuals", true)!.GetMethod("CloseOrHide", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!.Invoke(null, [content]);
    private sealed class CountingStrategy : ILayoutUpdateStrategy
    {
        internal int Before, After;
        internal ILayoutContainer? Destination;
        public bool BeforeInsertAnchorable(LayoutRoot layout, LayoutAnchorable anchorableToShow, ILayoutContainer destinationContainer)
        {
            Before++;
            Destination = destinationContainer;
            return false;
        }

        public void AfterInsertAnchorable(LayoutRoot layout, LayoutAnchorable anchorableShown) => After++;
        public bool BeforeInsertDocument(LayoutRoot layout, LayoutDocument anchorableToShow, ILayoutContainer destinationContainer) => false;
        public void AfterInsertDocument(LayoutRoot layout, LayoutDocument anchorableShown)
        {
        }
    }

    private sealed class Fixture : IDisposable
    {
        internal readonly DockingManager Manager = new();
        internal LayoutRoot Root => Manager.Layout;

        internal Fixture(Func<DockingManager, LayoutRoot> layout) => Manager.Layout = layout(Manager);
        internal LayoutAnchorable Tool(string id) => Root.Descendents().OfType<LayoutAnchorable>().First(a => a.ContentId == id);
        internal LayoutDocument Document(string id) => Root.Descendents().OfType<LayoutDocument>().First(a => a.ContentId == id);
        public void Dispose() => Manager.Dispose();
    }

    private static LayoutDocument Doc(string id) => new()
    {
        Title = id,
        ContentId = id,
        Content = id
    };
    private static LayoutAnchorable ToolContent(string id) => new()
    {
        Title = id,
        ContentId = id,
        Content = id
    };
    private static LayoutRoot Standard(DockingManager manager)
    {
        var left = new LayoutAnchorablePane
        {
            DockWidth = new(240)
        };
        left.Children.Add(ToolContent("explorer"));
        left.Children.Add(ToolContent("toolbox"));
        var documents = new LayoutDocumentPane();
        documents.Children.Add(Doc("d1"));
        documents.Children.Add(Doc("d2"));
        documents.Children.Add(Doc("d3"));
        var bottom = new LayoutAnchorablePane
        {
            DockHeight = new(180)
        };
        bottom.Children.Add(ToolContent("output"));
        bottom.Children.Add(ToolContent("errors"));
        var vertical = new LayoutPanel
        {
            Orientation = Orientation.Vertical
        };
        vertical.Children.Add(new LayoutDocumentPaneGroup(documents));
        vertical.Children.Add(bottom);
        var right = new LayoutAnchorablePane
        {
            DockWidth = new(260)
        };
        right.Children.Add(ToolContent("props"));
        var panel = new LayoutPanel
        {
            Orientation = Orientation.Horizontal
        };
        panel.Children.Add(left);
        panel.Children.Add(vertical);
        panel.Children.Add(right);
        return new()
        {
            RootPanel = panel
        };
    }

    private static LayoutRoot Flat(DockingManager manager)
    {
        var left = new LayoutAnchorablePane
        {
            DockWidth = new(240)
        };
        left.Children.Add(ToolContent("explorer"));
        var documents = new LayoutDocumentPane();
        documents.Children.Add(Doc("d1"));
        documents.Children.Add(Doc("d2"));
        documents.Children.Add(Doc("d3"));
        var panel = new LayoutPanel
        {
            Orientation = Orientation.Horizontal
        };
        panel.Children.Add(left);
        panel.Children.Add(documents);
        return new()
        {
            RootPanel = panel
        };
    }

    /// <summary>The recorded dump format: type names without the Layout prefix,
        /// orientation, children in brackets, then non-empty sides, floating windows
        /// and hidden tools.</summary>
        private static string Dump(LayoutRoot root)
    {
        var text = new StringBuilder();
        void Write(ILayoutElement element)
        {
            text.Append(element.GetType().Name.Replace("Layout", "", StringComparison.Ordinal));
            if (element is LayoutContent content)
            {
                text.Append(':').Append(content.ContentId);
                return;
            }

            if (element is ILayoutOrientableGroup oriented)
                text.Append(oriented.Orientation == Orientation.Horizontal ? "(H)" : "(V)");
            if (element is ILayoutContainer container)
            {
                text.Append('[');
                var first = true;
                foreach (var child in container.Children)
                {
                    if (!first)
                        text.Append(' ');
                    first = false;
                    Write(child);
                }

                text.Append(']');
            }
        }

        Write(root.RootPanel);
        foreach (var side in new[]
        {
            root.LeftSide,
            root.RightSide,
            root.TopSide,
            root.BottomSide
        }

        )
            if (side is { ChildrenCount: > 0 })
            {
                text.Append(" side:");
                Write(side);
            }

        foreach (var floating in root.FloatingWindows)
        {
            text.Append(" float:");
            Write(floating);
        }

        if (root.Hidden.Count > 0)
            text.Append(" hidden:").Append(string.Join(",", root.Hidden.Select(h => h.ContentId)));
        return text.ToString();
    }
}
