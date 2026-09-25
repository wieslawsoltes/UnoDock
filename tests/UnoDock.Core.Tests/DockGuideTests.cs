using UnoDock.Core;
using UnoDock.Testing;

internal static class DockGuideGeometryTests
{
    internal static void Register(TestRunner tests)
    {
        var host = new DockRect(0, 0, 1000, 640);
        var pane = new DockRect(200, 0, 800, 640);
        tests.Test("guide layout has independent root, pane and outer tool targets", () =>
        {
            var slots = DockGuideLayout.Create(host, pane, true, true, true);
            Check.Equal(13, slots.Count);
            Check.Equal(4, slots.Count(s => s.Scope == DockGuideScope.Workspace));
            Check.Equal(5, slots.Count(s => s.Scope == DockGuideScope.Pane));
            Check.Equal(4, slots.Count(s => s.Scope == DockGuideScope.ToolBesideDocument));
        });
        tests.Test("guide layout workspace can be omitted independently", () => Check.Equal(9, DockGuideLayout.Create(host, pane, false, true, true).Count));
        tests.Test("guide layout empty pane leaves root targets", () => Check.Equal(4, DockGuideLayout.Create(host, default, true, false, false).Count));
        tests.Test("guide layout tiny host never scales glyphs below hit size", () => Check.Equal(0, DockGuideLayout.Create(new(0, 0, 31, 100), pane, true, true, true).Count));
        tests.Test("guide layout avoids ambiguous collisions on compact hosts", () =>
        {
            var slots = DockGuideLayout.Create(new(0, 0, 32, 32), new(0, 0, 32, 32), true, true, true);
            Check.Equal(1, slots.Count);
            Check.Equal(DockGuideScope.Workspace, slots[0].Scope);
        });
        tests.Test("guide layout clips partially visible panes before centering", () =>
        {
            var slot = DockGuideLayout.Create(host, new(-800, 100, 1200, 400), false, true, false).Single(s => s.Position == DockPosition.Inside);
            Check.Near(184, slot.Bounds.X);
            Check.Near(284, slot.Bounds.Y);
        });
        tests.Test("guide hit rectangles are half open", () =>
        {
            var r = new DockRect(10, 20, 32, 32);
            Check.True(DockGuideLayout.HitTest(r, new(10, 20)));
            Check.False(DockGuideLayout.HitTest(r, new(42, 30)));
            Check.False(DockGuideLayout.HitTest(r, new(20, 52)));
            Check.False(DockGuideLayout.HitTest(r, new(double.NaN, 25)));
        });
        tests.Test("guide layout translation covariance", () =>
        {
            var a = DockGuideLayout.Create(host, pane, true, true, true);
            var b = DockGuideLayout.Create(host with
            {
                X = -1500,
                Y = 930
            }, pane with
            {
                X = pane.X - 1500,
                Y = pane.Y + 930
            }, true, true, true);
            Check.Equal(a.Count, b.Count);
            for (var i = 0; i < a.Count; i++)
            {
                Check.Near(a[i].Bounds.X - 1500, b[i].Bounds.X);
                Check.Near(a[i].Bounds.Y + 930, b[i].Bounds.Y);
            }
        });
        tests.Test("guide geometry randomized clipping collision and hit invariants (20000 layouts)", () =>
        {
            var random = new Random(94207);
            for (var n = 0; n < 20000; n++)
            {
                var bounds = new DockRect(random.Next(-3000, 3000), random.Next(-2000, 2000), random.Next(0, 2500), random.Next(0, 1800));
                var p = new DockRect(bounds.X + random.Next(-500, 500), bounds.Y + random.Next(-300, 300), random.Next(0, 1800), random.Next(0, 1400));
                var size = random.Next(16, 129);
                var a = DockGuideLayout.Create(bounds, p, true, true, true, size, random.Next(0, 65));
                Check.True(a.Count <= 13);
                for (var i = 0; i < a.Count; i++)
                {
                    var r = a[i].Bounds;
                    Check.Near(size, r.Width);
                    Check.Near(size, r.Height);
                    Check.True(r.X >= bounds.X && r.Y >= bounds.Y && r.Right <= bounds.Right && r.Bottom <= bounds.Bottom);
                    Check.Equal(1, a.Count(s => DockGuideLayout.HitTest(s.Bounds, new(r.X + size / 2, r.Y + size / 2))));
                    for (var j = i + 1; j < a.Count; j++)
                    {
                        var b = a[j].Bounds;
                        Check.False(r.X < b.Right && b.X < r.Right && r.Y < b.Bottom && b.Y < r.Bottom);
                    }
                }
            }
        });
        tests.Test("stock compass has five adjacent fractional-DIP cells and an 88-DIP envelope", () =>
        {
            var slots = DockGuideLayout.CreateStock(host, pane, false, true);
            Check.Equal(5, slots.Count);
            Check.Near(88, slots.Max(s => s.Bounds.Right) - slots.Min(s => s.Bounds.X));
            Check.Near(88, slots.Max(s => s.Bounds.Bottom) - slots.Min(s => s.Bounds.Y));
            var center = slots.Single(s => s.Position == DockPosition.Inside).Bounds;
            var right = slots.Single(s => s.Position == DockPosition.Right).Bounds;
            Check.Equal(center.Right, right.X);
            Check.False(DockGuideLayout.HitTest(center, new(right.X, center.Y + 1)));
            Check.True(DockGuideLayout.HitTest(right, new(right.X, center.Y + 1)));
        });
        tests.Test("stock workspace glyphs retain observed rectangular aspect and edge placement", () =>
        {
            var slots = DockGuideLayout.CreateStock(host, pane, true, true);
            Check.Equal(9, slots.Count);
            var left = slots.Single(s => s.Scope == DockGuideScope.Workspace && s.Position == DockPosition.Left).Bounds;
            var top = slots.Single(s => s.Scope == DockGuideScope.Workspace && s.Position == DockPosition.Top).Bounds;
            Check.Near(32, left.Width);
            Check.Near(29, left.Height);
            Check.Near(host.X, left.X);
            Check.Near(29, top.Width);
            Check.Near(32, top.Height);
            Check.Near(host.Y, top.Y);
        });
        tests.Test("extended stock tool placements are explicitly opt in", () =>
        {
            Check.Equal(9, DockGuideLayout.CreateStock(host, pane, true, true).Count);
            Check.Equal(13, DockGuideLayout.CreateStock(host, pane, true, true, true).Count);
        });
        tests.Test("stock fractional geometry stays bounded and unambiguous over 10000 translated layouts", () =>
        {
            var random = new Random(92023);
            for (var n = 0; n < 10000; n++)
            {
                var box = new DockRect(random.Next(-4000, 4000) + .125, random.Next(-3000, 3000) + .375, random.Next(1, 2400), random.Next(1, 1800));
                var slots = DockGuideLayout.CreateStock(box, box, true, true, true, n % 2 == 0 ? 88d / 3 : 44);
                foreach (var slot in slots)
                {
                    var rect = slot.Bounds;
                    Check.True(rect.X >= box.X && rect.Y >= box.Y && rect.Right <= box.Right && rect.Bottom <= box.Bottom);
                    Check.Equal(1, slots.Count(s => DockGuideLayout.HitTest(s.Bounds, new(rect.X + rect.Width / 2, rect.Y + rect.Height / 2))));
                }
            }
        });
        foreach (var bad in new[]
        {
            double.NaN,
            double.PositiveInfinity,
            -1d
        }

        )
        {
            var value = bad;
            tests.Test("guide layout rejects invalid host " + value, () => Check.Throws<ArgumentOutOfRangeException>(() => DockGuideLayout.Create(host with { Width = value }, pane, true, true, true)));
            tests.Test("guide layout rejects invalid glyph size " + value, () => Check.Throws<ArgumentOutOfRangeException>(() => DockGuideLayout.Create(host, pane, true, true, true, value)));
            tests.Test("guide layout rejects invalid gap " + value, () => Check.Throws<ArgumentOutOfRangeException>(() => DockGuideLayout.Create(host, pane, true, true, true, gap: value)));
        }

        tests.Test("guide layout rejects overflowing extents", () => Check.Throws<ArgumentOutOfRangeException>(() => DockGuideLayout.Create(new(double.MaxValue, 0, double.MaxValue, 100), pane, true, true, true)));
    }
}
