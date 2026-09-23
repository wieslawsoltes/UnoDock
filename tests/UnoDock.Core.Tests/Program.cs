using System.Xml;
using UnoDock.Core;
using UnoDock.Testing;

var tests = new TestRunner();
InteractionGeometryTests.Register(tests);
ChromeTests.Register(tests);
DockGuideGeometryTests.Register(tests);
var star = DockLengthUnit.Star; var pixel = DockLengthUnit.Pixel; var auto = DockLengthUnit.Auto;
tests.Test("empty allocation", () => Check.Equal(0, DockSplitSolver.Allocate(100, 4, []).Length));
tests.Test("weighted allocation", () => { var x = DockSplitSolver.Allocate(306, 6, [new(1, star), new(2, star)]); Check.Near(100, x[0]); Check.Near(200, x[1]); });
tests.Test("pixel and auto", () => { var x = DockSplitSolver.Allocate(300, 0, [new(100, pixel), new(0, auto, Desired: 75)]); Check.Near(100, x[0]); Check.Near(75, x[1]); });
tests.Test("minimum water filling", () => { var x = DockSplitSolver.Allocate(300, 0, [new(1, star, 200), new(1, star)]); Check.Near(200, x[0]); Check.Near(100, x[1]); });
tests.Test("impossible minima compress proportionally", () => { var x = DockSplitSolver.Allocate(150, 0, [new(1, star, 200), new(1, star, 100)]); Check.Near(100, x[0]); Check.Near(50, x[1]); });
tests.Test("fixed budget compression", () => { var x = DockSplitSolver.Allocate(200, 0, [new(250, pixel, 50), new(1, star, 50)]); Check.Near(150, x[0]); Check.Near(50, x[1]); });
tests.Test("zero star weights remain finite", () => { var x = DockSplitSolver.Allocate(100, 0, [new(0, star), new(0, star)]); Check.Near(50, x[0]); Check.Near(50, x[1]); });
foreach (var value in new[] { -1d, double.NaN, double.PositiveInfinity })
{
    var bad = value;
    tests.Test("reject available " + bad, () => Check.Throws<ArgumentOutOfRangeException>(() => DockSplitSolver.Allocate(bad, 1, [])));
    tests.Test("reject dimension " + bad, () => Check.Throws<ArgumentOutOfRangeException>(() => DockSplitSolver.Allocate(100, 1, [new(bad, star)])));
}
tests.Test("randomized allocation invariants (5000 cases)", () =>
{
    var random = new Random(77119);
    for (var iteration = 0; iteration < 5000; iteration++)
    {
        var items = Enumerable.Range(0, random.Next(1, 32)).Select(_ => new DockMeasure(random.NextDouble() * 100 + .01, (DockLengthUnit)random.Next(3), random.NextDouble() * 50, random.NextDouble() * 100)).ToArray();
        var available = random.NextDouble() * 4000; var separator = random.NextDouble() * 10;
        var x = DockSplitSolver.Allocate(available, separator, items); var space = Math.Max(0, available - separator * (items.Length - 1));
        Check.True(x.All(v => v >= 0 && double.IsFinite(v))); Check.True(x.Sum() <= space + 1e-6);
        if (items.Any(m => m.Unit == star)) Check.Near(space, x.Sum(), 1e-5);
        if (items.Sum(m => m.Minimum) <= space) for (var i = 0; i < x.Length; i++) Check.True(x[i] + 1e-6 >= items[i].Minimum);
    }
});
tests.Test("resize clamps and conserves extent", () => { var x = DockSplitSolver.ResizePair(100, 200, 500, 50, 60); Check.Near(240, x.Before); Check.Near(60, x.After); });
tests.Test("resize impossible minima", () => { var x = DockSplitSolver.ResizePair(50, 50, 5, 300, 100); Check.Near(75, x.Before); Check.Near(25, x.After); });
tests.Test("randomized resize conservation (5000 cases)", () => { var r = new Random(319); for (var i = 0; i < 5000; i++) { var a = r.NextDouble() * 1000; var b = r.NextDouble() * 1000; var x = DockSplitSolver.ResizePair(a, b, r.NextDouble() * 4000 - 2000, r.NextDouble() * 200, r.NextDouble() * 200); Check.Near(a + b, x.Before + x.After); Check.True(x.Before >= 0 && x.After >= 0); } });
var area = new DockRect(10, 20, 400, 200);
foreach (var (point, expected) in new[] { (new DockPoint(11, 120), DockPosition.Left), (new DockPoint(409, 120), DockPosition.Right), (new DockPoint(210, 21), DockPosition.Top), (new DockPoint(210, 219), DockPosition.Bottom), (new DockPoint(210, 120), DockPosition.Inside) })
    tests.Test("hit-test " + expected, () => Check.Equal(expected, DockSplitSolver.HitTest(area, point)));
foreach (var position in Enum.GetValues<DockPosition>())
    tests.Test("preview " + position, () => { var p = DockSplitSolver.Preview(area, position); Check.True(p.Width <= area.Width && p.Height <= area.Height); Check.True(area.Contains(new(p.X, p.Y))); });
tests.Test("drag threshold and pointer identity", () => { var d = new DockDragSession(); d.Arm(1, new(0, 0), true); Check.False(d.Move(2, new(100, 0), [])); Check.False(d.Move(1, new(2, 0), [])); Check.Equal(DockDragState.Armed, d.State); Check.True(d.Move(1, new(10, 0), [])); Check.False(d.Commit(2)); Check.True(d.Commit(1)); Check.False(d.Commit(1)); });
tests.Test("click is not a dock operation", () => { var d = new DockDragSession(); d.Arm(1, new(0, 0), false); Check.False(d.Commit(1)); });
tests.Test("cancelled drag cannot commit", () => { var d = new DockDragSession(); d.Arm(1, new(0, 0), true); d.Move(1, new(100, 0), []); d.Cancel(); Check.False(d.Commit(1)); Check.Equal<DockDropTarget?>(null, d.Target); });
tests.Test("target type filter priority and deterministic ties", () => { var d = new DockDragSession(); d.Arm(1, new(0, 0), true); d.Move(1, new(200, 100), [new("tool", area, false, true, 99), new("z", area, true, true, 10), new("a", area, true, true, 10)]); Check.Equal("a", d.Target?.Id); });
tests.Test("batch nested flush and idempotent dispose", () => { var count = 0; var b = new UpdateBatch(() => count++); var s = b.Begin(); using (b.Begin()) { b.Invalidate(); b.Invalidate(); } Check.Equal(0, count); s.Dispose(); s.Dispose(); Check.Equal(1, count); });
tests.Test("batch supports nonrecursive invalidation", () => { var count = 0; UpdateBatch? b = null; b = new(() => { if (++count == 1) b!.Invalidate(); }); b.Invalidate(); Check.Equal(2, count); });
tests.Test("batch recovers after callback failure", () => { var fail = true; var count = 0; var b = new UpdateBatch(() => { if (fail) throw new InvalidOperationException(); count++; }); Check.Throws<InvalidOperationException>(b.Invalidate); fail = false; b.Invalidate(); Check.Equal(1, count); });
tests.Test("XML canonical attributes and roundtrip", () => { var node = new LayoutSnapshotNode("LayoutRoot"); node.Attributes["z"] = "<&\""; node.Attributes["a"] = "first"; node.Children.Add(new("RootPanel")); var text = new StringWriter(); using (var writer = XmlWriter.Create(text, new() { OmitXmlDeclaration = true })) LayoutSnapshotXml.Write(node, writer); Check.True(text.ToString().IndexOf("a=") < text.ToString().IndexOf("z=")); var copy = LayoutSnapshotXml.Read(new StringReader(text.ToString())); Check.Equal("<&\"", copy.Attributes["z"]); Check.Equal("RootPanel", copy.Children[0].Name); });
tests.Test("XML rejects DTD", () => Check.Throws<XmlException>(() => LayoutSnapshotXml.Read(new StringReader("<!DOCTYPE a [<!ENTITY x 'boom'>]><a>&x;</a>"))));
tests.Test("XML rejects depth overflow", () => Check.Throws<XmlException>(() => LayoutSnapshotXml.Read(new StringReader("<a><b><c/></b></a>"), new(MaxDepth: 2))));
tests.Test("XML rejects node overflow", () => Check.Throws<XmlException>(() => LayoutSnapshotXml.Read(new StringReader("<a><b/><c/></a>"), new(MaxNodes: 2))));
tests.Test("XML rejects oversized attributes", () => Check.Throws<XmlException>(() => LayoutSnapshotXml.Read(new StringReader("<a b='1' c='2'/>"), new(MaxAttributesPerNode: 1))));
tests.Test("XML rejects character overflow", () => Check.Throws<XmlException>(() => LayoutSnapshotXml.Read(new StringReader("<a title='" + new string('x', 100) + "'/>"), new(MaxCharacters: 30))));
tests.Test("XML rejects text content", () => Check.Throws<XmlException>(() => LayoutSnapshotXml.Read(new StringReader("<a>not a layout</a>"))));
tests.Test("XML rejects extra roots", () => Check.Throws<XmlException>(() => LayoutSnapshotXml.Read(new StringReader("<a/><b/>"))));
tests.Test("XML rejects cyclic writes", () => { var n = new LayoutSnapshotNode("a"); n.Children.Add(n); using var writer = XmlWriter.Create(new StringWriter()); Check.Throws<InvalidOperationException>(() => LayoutSnapshotXml.Write(n, writer)); });
tests.Test("XML accepts serializer namespace declarations", () => Check.Equal("LayoutRoot", LayoutSnapshotXml.Read(new StringReader("<LayoutRoot xmlns:xsi='http://www.w3.org/2001/XMLSchema-instance'/>" )).Name));
tests.Test("XML preserves caller stream ownership", () => { using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("<a/>")); LayoutSnapshotXml.Read(stream); Check.True(stream.CanRead); });
return await tests.Run(args.FirstOrDefault() ?? "artifacts/test-results", "core");
