using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Collections.ObjectModel;
using System.Xml;
using UnoDock.Core;
using Xceed.Wpf.AvalonDock;
using Xceed.Wpf.AvalonDock.Layout;
using Xceed.Wpf.AvalonDock.Layout.Serialization;
using LayoutPanel = Xceed.Wpf.AvalonDock.Layout.LayoutPanel;

namespace UnoDock.Testing;

public static class RuntimeTests
{
    public static async Task<int> Run(DockingManager host, string output)
    {
        var tests = new TestRunner();
        tests.Test("root defaults and unique ownership", () => { var r = new LayoutRoot(); Check.Equal(5, r.ChildrenCount); Check.Same(r, r.RootPanel.Parent); Check.Equal(AnchorSide.Left, r.LeftSide.Side); AssertTree(r); });
        tests.Test("insert and reparent preserve identity", () => { var d = new LayoutDocument(); var a = new LayoutDocumentPane(d); var b = new LayoutDocumentPane(); var r = Root(a, b); b.Children.Add(d); Check.Equal(0, a.ChildrenCount); Check.Same(b, d.Parent); Check.Same(r, d.Root); AssertTree(r); });
        tests.Test("cycle rejected without mutation", () => { var r = new LayoutRoot(); var nested = new LayoutPanel(); r.RootPanel.Children.Add(nested); Check.Throws<InvalidOperationException>(() => nested.Children.Add(r.RootPanel)); AssertTree(r); });
        tests.Test("duplicate child rejected", () => { var d = new LayoutDocument(); var p = new LayoutDocumentPane(d); Check.Throws<InvalidOperationException>(() => p.Children.Add(d)); Check.Equal(1, p.ChildrenCount); });
        tests.Test("replacement detaches previous child", () => { var a = new LayoutDocument(); var b = new LayoutDocument(); var p = new LayoutDocumentPane(a); p.Children[0] = b; Check.Equal<ILayoutContainer?>(null, a.Parent); Check.Same(p, b.Parent); });
        tests.Test("collection clear detaches all children", () => { var d = new LayoutDocument(); var p = new LayoutDocumentPane(d); p.Children.Clear(); Check.Equal<ILayoutContainer?>(null, d.Parent); });
        tests.Test("single selection per pane", () => { var a = new LayoutDocument(); var b = new LayoutDocument(); var p = new LayoutDocumentPane(a); p.Children.Add(b); a.IsSelected = true; b.IsSelected = true; Check.False(a.IsSelected); Check.True(b.IsSelected); Check.Same(b, p.SelectedContent); });
        tests.Test("active content uniqueness", () => { var a = new LayoutDocument(); var b = new LayoutDocument(); var r = Root(new LayoutDocumentPane(a), new LayoutDocumentPane(b)); a.IsActive = true; b.IsActive = true; Check.False(a.IsActive); Check.Same(b, r.ActiveContent); Check.True(b.IsSelected); });
        tests.Test("active removal repairs selection", () => { var a = new LayoutDocument(); var b = new LayoutDocument(); var p = new LayoutDocumentPane(a); p.Children.Add(b); var r = Root(p); a.IsActive = true; a.Close(); Check.Same(b, r.ActiveContent); });
        tests.Test("close cancellation preserves model", () => { var d = new LayoutDocument(); var p = new LayoutDocumentPane(d); d.Closing += (_, e) => e.Cancel = true; d.Close(); Check.Same(p, d.Parent); });
        tests.Test("document manager cancellation", () => { using var m = new DockingManager(); var d = new LayoutDocument(); m.Layout = Root(new LayoutDocumentPane(d)); m.DocumentClosing += (_, e) => e.Cancel = true; d.Close(); Check.Same(m.Layout, d.Root); });
        tests.Test("close event ordering", () => { using var m = new DockingManager(); var d = new LayoutDocument(); m.Layout = Root(new LayoutDocumentPane(d)); var log = new List<string>(); d.Closing += (_, _) => log.Add("closing"); m.DocumentClosing += (_, _) => log.Add("manager-closing"); d.Closed += (_, _) => log.Add("closed"); m.DocumentClosed += (_, _) => log.Add("manager-closed"); d.Close(); Check.Equal("closing,manager-closing,closed,manager-closed", string.Join(',', log)); });
        tests.Test("disabled close capability", () => { var d = new LayoutDocument { CanClose = false }; var p = new LayoutDocumentPane(d); d.Close(); Check.Same(p, d.Parent); });
        tests.Test("float and dock restore original pane index", () => { var a = new LayoutDocument(); var b = new LayoutDocument(); var p = new LayoutDocumentPane(a); p.Children.Add(b); var r = Root(p); b.Float(); Check.True(b.IsFloating); Check.Equal(1, r.FloatingWindows.Count); b.Dock(); Check.False(b.IsFloating); Check.Same(p, b.Parent); Check.Equal(1, p.Children.IndexOf(b)); Check.Equal(0, r.FloatingWindows.Count); AssertTree(r); });
        tests.Test("float capability prevents move", () => { var d = new LayoutDocument { CanFloat = false }; var p = new LayoutDocumentPane(d); var r = Root(p); d.Float(); Check.Same(p, d.Parent); Check.Equal(0, r.FloatingWindows.Count); });
        tests.Test("preview float can cancel", () => { using var m = new DockingManager(); var d = new LayoutDocument(); var p = new LayoutDocumentPane(d); m.Layout = Root(p); m.PreviewFloat += (_, e) => ((DockEventArgs)e).Cancel = true; d.Float(); Check.Same(p, d.Parent); });
        tests.Test("hide and show restore original tool pane", () => { var a = new LayoutAnchorable(); var p = new LayoutAnchorablePane(a); var r = Root(p); a.Hide(); Check.True(a.IsHidden); Check.Same(r, a.Parent); a.Show(); Check.False(a.IsHidden); Check.Same(p, a.Parent); AssertTree(r); });
        tests.Test("hide cancellation", () => { var a = new LayoutAnchorable(); var p = new LayoutAnchorablePane(a); Root(p); a.Hiding += (_, e) => e.Cancel = true; a.Hide(); Check.Same(p, a.Parent); });
        tests.Test("auto-hide roundtrip preserves pane", () => { var a = new LayoutAnchorable(); var b = new LayoutAnchorable(); var p = new LayoutAnchorablePane(a); p.Children.Add(b); var r = Root(p); a.ToggleAutoHide(); Check.True(a.IsAutoHidden && b.IsAutoHidden); a.ToggleAutoHide(); Check.Same(p, a.Parent); Check.Same(p, b.Parent); AssertTree(r); });
        tests.Test("auto-hide honors group capability", () => { var a = new LayoutAnchorable(); var b = new LayoutAnchorable { CanAutoHide = false }; var p = new LayoutAnchorablePane(a); p.Children.Add(b); Root(p); a.ToggleAutoHide(); Check.Same(p, a.Parent); });
        foreach (var side in new[] { DockPosition.Left, DockPosition.Right, DockPosition.Top, DockPosition.Bottom })
            tests.Test("split " + side, () => { var a = new LayoutDocument(); var b = new LayoutDocument(); var p = new LayoutDocumentPane(a); p.Children.Add(b); var r = Root(p); DockOperations.Dock(b, p, side); Check.False(ReferenceEquals(a.Parent, b.Parent)); Check.Same(r, b.Root); AssertTree(r); });
        tests.Test("cross-root drop rejected", () => { var a = new LayoutDocument(); var p = new LayoutDocumentPane(a); var r = Root(p); var q = new LayoutDocumentPane(); Root(q); DockOperations.Dock(a, q, DockPosition.Inside); Check.Same(r, a.Root); });
        tests.Test("tool-as-document capability", () => { var a = new LayoutAnchorable { CanDockAsTabbedDocument = false }; var p = new LayoutAnchorablePane(a); Root(p, new LayoutDocumentPane()); a.DockAsDocument(); Check.Same(p, a.Parent); });
        tests.Test("XML restores content by stable identity", () => { using var m = new DockingManager(); var view = new object(); var d = new LayoutDocument { ContentId = "doc", Content = view, Title = "Hello<&" }; m.Layout = Root(new LayoutDocumentPane(d)); var s = new XmlLayoutSerializer(m); using var text = new StringWriter(); s.Serialize(text); s.Deserialize(new StringReader(text.ToString())); var copy = m.Layout.Descendents().OfType<LayoutDocument>().Single(); Check.Same(view, copy.Content); Check.Equal("Hello<&", copy.Title); AssertTree(m.Layout); });
        tests.Test("XML malformed input is atomic", () => { using var m = new DockingManager(); var original = m.Layout; Check.Throws<XmlException>(() => new XmlLayoutSerializer(m).Deserialize(new StringReader("<LayoutRoot><Broken/></LayoutRoot>"))); Check.Same(original, m.Layout); });
        tests.Test("XML callback failure is atomic", () => { using var m = new DockingManager(); m.Layout = Root(new LayoutDocumentPane(new LayoutDocument() { ContentId = "d" })); var old = m.Layout; var s = new XmlLayoutSerializer(m); using var text = new StringWriter(); s.Serialize(text); s.LayoutSerializationCallback += (_, _) => throw new InvalidOperationException("callback"); Check.Throws<InvalidOperationException>(() => s.Deserialize(new StringReader(text.ToString()))); Check.Same(old, m.Layout); });
        tests.Test("XML callback cancellation removes content", () => { using var m = new DockingManager(); m.Layout = Root(new LayoutDocumentPane(new LayoutDocument() { ContentId = "d" })); var s = new XmlLayoutSerializer(m); using var text = new StringWriter(); s.Serialize(text); s.LayoutSerializationCallback += (_, e) => e.Cancel = true; s.Deserialize(new StringReader(text.ToString())); Check.Equal(0, m.Layout.Descendents().OfType<LayoutContent>().Count()); });
        tests.Test("XML float restore references", () => { using var m = new DockingManager(); var d = new LayoutDocument { ContentId = "d" }; m.Layout = Root(new LayoutDocumentPane(d)); d.Float(); var s = new XmlLayoutSerializer(m); using var text = new StringWriter(); s.Serialize(text); s.Deserialize(new StringReader(text.ToString())); var copy = m.Layout.Descendents().OfType<LayoutDocument>().Single(); Check.True(copy.IsFloating); copy.Dock(); Check.False(copy.IsFloating); AssertTree(m.Layout); });
        tests.Test("observable document source add remove reset", () => { using var m = new DockingManager(); var source = new ObservableCollection<object>(); m.DocumentsSource = source; var a = new object(); var b = new object(); source.Add(a); source.Add(b); Check.Equal(2, m.Layout.Descendents().OfType<LayoutDocument>().Count()); source.Remove(a); Check.Same(b, m.Layout.Descendents().OfType<LayoutDocument>().Single().Content); source.Clear(); Check.Equal(0, m.Layout.Descendents().OfType<LayoutDocument>().Count()); });
        tests.Test("source observer detaches on replacement", () => { using var m = new DockingManager(); var old = new ObservableCollection<object>(); m.DocumentsSource = old; m.DocumentsSource = new ObservableCollection<object>(); old.Add(new()); Check.Equal(0, m.Layout.Descendents().OfType<LayoutDocument>().Count()); });
        tests.Test("model tree randomized moves (500 cases)", () => { var panes = Enumerable.Range(0, 5).Select(_ => new LayoutDocumentPane()).ToArray(); var r = Root(panes); var docs = Enumerable.Range(0, 30).Select(_ => new LayoutDocument()).ToArray(); foreach (var doc in docs) panes[0].Children.Add(doc); var rng = new Random(817); for (var i = 0; i < 500; i++) { var d = docs[rng.Next(docs.Length)]; var p = panes[rng.Next(panes.Length)]; if (!ReferenceEquals(d.Parent, p)) p.Children.Add(d); d.IsActive = true; AssertTree(r); Check.Equal(30, r.Descendents().OfType<LayoutDocument>().Count()); } });
        tests.Test("presenter cache survives selection and docking", () => { var d = new LayoutDocument { Content = new TextBox { Text = "retained" } }; var p = new LayoutDocumentPane(d); var q = new LayoutDocumentPane(new LayoutDocument()); host.Layout = Root(p, q); host.Refresh(); host.UpdateLayout(); var item = host.GetLayoutItemFromModel(d); var view = item.View; DockOperations.Dock(d, q, DockPosition.Inside); host.Refresh(); Check.Same(view, host.GetLayoutItemFromModel(d).View); Check.Same(d.Content, view.Content); });
        tests.Test("visual split creates live pane controls", () => { var a = new LayoutDocument(); var b = new LayoutDocument(); var p = new LayoutDocumentPane(a); p.Children.Add(b); host.Layout = Root(p); host.Refresh(); DockOperations.Dock(b, p, DockPosition.Right); host.Refresh(); host.UpdateLayout(); Check.True(host.LayoutRootPanel != null); Check.Equal(2, host.Layout.Descendents().OfType<LayoutDocumentPane>().Count()); });
        var original = host.Layout;
        try { return await tests.Run(output, "runtime"); }
        finally { host.Layout = original; host.Refresh(); }
    }
    private static LayoutRoot Root(params ILayoutPanelElement[] children)
    { var panel = new LayoutPanel(); foreach (var child in children) panel.Children.Add(child); return new() { RootPanel = panel }; }
    private static void AssertTree(LayoutRoot root)
    {
        var seen = new HashSet<ILayoutElement>(ReferenceEqualityComparer.Instance) { root };
        foreach (var element in root.Descendents())
        {
            Check.True(seen.Add(element), "Duplicate/cyclic node."); Check.Same(root, element.Root);
            Check.True(element.Parent != null && element.Parent.Children.Any(c => ReferenceEquals(c, element)), "Broken parent/child relationship.");
        }
        Check.True(seen.OfType<LayoutContent>().Count(c => c.IsActive) <= 1, "Multiple active items.");
    }
}
