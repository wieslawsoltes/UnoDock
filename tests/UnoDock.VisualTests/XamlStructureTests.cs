using Microsoft.UI.Xaml.Data;
using UnoDock.Controls;
using UnoDock.Layout;

namespace UnoDock.Testing;

internal static class XamlStructureTests
{
    internal static void Add(TestRunner tests)
    {
        tests.Test("XAML structure: native root endpoints publish distinct instance defaults", () =>
        {
            var first = new LayoutRoot();
            var second = new LayoutRoot();
            Check.Same(first.RootPanel, first.GetValue(LayoutRoot.RootPanelProperty));
            Check.Same(second.RootPanel, second.GetValue(LayoutRoot.RootPanelProperty));
            Check.False(ReferenceEquals(first.RootPanel, second.RootPanel));
            Check.Same(first.LeftSide, first.GetValue(LayoutRoot.LeftSideProperty));
            Check.Same(first.RightSide, first.GetValue(LayoutRoot.RightSideProperty));
            Check.Same(first.TopSide, first.GetValue(LayoutRoot.TopSideProperty));
            Check.Same(first.BottomSide, first.GetValue(LayoutRoot.BottomSideProperty));
        });
        tests.Test("XAML structure: root Binding and model writes preserve the binding expression", () =>
        {
            var root = new LayoutRoot();
            var first = new LayoutPanel(new LayoutDocumentPane(new LayoutDocument { Title = "First" }));
            var source = new ContentControl
            {
                Content = first
            };
            var binding = new Binding
            {
                Source = source,
                Path = new("Content"),
                Mode = BindingMode.TwoWay
            };
            BindingOperations.SetBinding(root, LayoutRoot.RootPanelProperty, binding);
            Check.Same(first, root.RootPanel);
            Check.Same(root, first.Parent);
            var second = new LayoutPanel(new LayoutDocumentPane());
            source.Content = second;
            Check.Same(second, root.RootPanel);
            Check.True(first.Parent == null);
            var third = new LayoutPanel(new LayoutDocumentPane());
            root.RootPanel = third;
            Check.Same(third, source.Content);
            Check.True(second.Parent == null);
            var fourth = new LayoutPanel(new LayoutDocumentPane());
            source.Content = fourth;
            Check.Same(fourth, root.RootPanel);
            Check.Same(root, fourth.Parent);
        });
        tests.Test("XAML structure: null root panel is rejected without desynchronizing the DP", () =>
        {
            var root = new LayoutRoot();
            var panel = root.RootPanel;
            Check.Throws<ArgumentNullException>(() => root.SetValue(LayoutRoot.RootPanelProperty, null));
            Check.Same(panel, root.RootPanel);
            Check.Same(panel, root.GetValue(LayoutRoot.RootPanelProperty));
            Check.Same(root, panel.Parent);
        });
        tests.Test("XAML structure: floating document and tool slots use existing ownership contracts", () =>
        {
            var document = new LayoutDocument();
            var documents = new LayoutDocumentFloatingWindow();
            documents.SetValue(LayoutDocumentFloatingWindow.RootDocumentProperty, document);
            Check.Same(document, documents.RootDocument);
            Check.Same(documents, document.Parent);
            documents.ClearValue(LayoutDocumentFloatingWindow.RootDocumentProperty);
            Check.True(document.Parent == null && documents.RootDocument == null);
            var tools = new LayoutAnchorableFloatingWindow();
            var group = new LayoutAnchorablePaneGroup();
            tools.SetValue(LayoutAnchorableFloatingWindow.RootPanelProperty, group);
            Check.Same(group, tools.RootPanel);
            Check.Same(tools, group.Parent);
            tools.ClearValue(LayoutAnchorableFloatingWindow.RootPanelProperty);
            Check.True(group.Parent == null && tools.RootPanel == null);
        });
        tests.Test("XAML structure: root ActiveContent DP follows reentrant activation", () =>
        {
            var a = new LayoutDocument();
            var b = new LayoutDocument();
            var c = new LayoutDocument();
            var pane = new LayoutDocumentPane(a);
            pane.Children.Add(b);
            pane.Children.Add(c);
            var root = new LayoutRoot
            {
                RootPanel = new(pane)
            };
            a.IsActive = true;
            var once = false;
            a.IsActiveChanged += (_, _) =>
            {
                if (once || a.IsActive)
                    return;
                once = true;
                root.SetValue(LayoutRoot.ActiveContentProperty, c);
            };
            root.SetValue(LayoutRoot.ActiveContentProperty, b);
            Check.Same(c, root.ActiveContent);
            Check.Same(c, root.GetValue(LayoutRoot.ActiveContentProperty));
            Check.False(a.IsActive || b.IsActive);
            Check.True(c.IsActive);
        });
        tests.Test("XAML structure: anchorable visibility Binding respects a hide veto", () =>
        {
            var tool = new LayoutAnchorable
            {
                Title = "Tool"
            };
            var root = new LayoutRoot
            {
                RootPanel = new(new LayoutAnchorablePane(tool))
            };
            Check.Equal(true, tool.GetValue(LayoutAnchorable.IsVisibleProperty));
            tool.Hiding += (_, args) => args.Cancel = true;
            tool.SetValue(LayoutAnchorable.IsVisibleProperty, false);
            Check.True(tool.IsVisible);
            Check.Equal(true, tool.GetValue(LayoutAnchorable.IsVisibleProperty));
            Check.Same(root, tool.Root);
        });
        tests.Test("XAML style: a superseding container style cannot publish stale capability values", () =>
        {
            using var manager = new DockingManager();
            var document = new LayoutDocument
            {
                Title = "Initial"
            };
            manager.Layout.RootPanel = new(new LayoutDocumentPane(document));
            var item = manager.GetLayoutItemFromModel(document);
            var old = new Style(typeof(LayoutItem))
            {
                Setters =
                {
                    new Setter(LayoutItem.TitleProperty, "Intermediate"),
                    new Setter(LayoutItem.CanCloseProperty, false)
                }
            };
            var final = new Style(typeof(LayoutItem))
            {
                Setters =
                {
                    new Setter(LayoutItem.TitleProperty, "Final"),
                    new Setter(LayoutItem.CanCloseProperty, true)
                }
            };
            var once = false;
            document.PropertyChanged += (_, args) =>
            {
                if (once || args.PropertyName != "Title" || document.Title != "Intermediate")
                    return;
                once = true;
                manager.LayoutItemContainerStyle = final;
            };
            manager.LayoutItemContainerStyle = old;
            Check.True(once);
            Check.Same(final, item.Style);
            Check.Equal("Final", document.Title);
            Check.True(document.CanClose && item.CloseCommand!.CanExecute(null));
        });
    }
}
