using UnoDock.Core;
using UnoDock.Testing;

internal static class ChromeTests
{
    internal static void Register(TestRunner tests)
    {
        var bounds = new DockRect(0, 0, 800, 600);
        foreach (var (point, hit) in new[]
        {
            (new DockPoint(0, 0), ChromeHit.TopLeft),
            (new DockPoint(400, 0), ChromeHit.Top),
            (new DockPoint(799, 0), ChromeHit.TopRight),
            (new DockPoint(0, 300), ChromeHit.Left),
            (new DockPoint(799, 300), ChromeHit.Right),
            (new DockPoint(0, 599), ChromeHit.BottomLeft),
            (new DockPoint(400, 599), ChromeHit.Bottom),
            (new DockPoint(799, 599), ChromeHit.BottomRight),
            (new DockPoint(50, 15), ChromeHit.Caption),
            (new DockPoint(50, 100), ChromeHit.Client),
            (new DockPoint(-1, 10), ChromeHit.Client),
            (new DockPoint(800, 10), ChromeHit.Client)
        }

        )
            tests.Test("chrome hit region " + point, () => Check.Equal(hit, ChromeGeometry.HitTest(bounds, point, 32, 5, 5, 5, 5, true, false)));
        tests.Test("maximized chrome cannot resize", () => Check.Equal(ChromeHit.Caption, ChromeGeometry.HitTest(bounds, new(0, 0), 32, 5, 5, 5, 5, true, true)));
        tests.Test("interactive caption controls take precedence over border", () => Check.Equal(ChromeHit.Client, ChromeGeometry.HitTest(bounds, new(0, 0), 32, 5, 5, 5, 5, true, false, new[] { new DockRect(0, 0, 20, 20) })));
        tests.Test("zero caption has no drag region", () => Check.Equal(0, ChromeGeometry.CaptionRegions(bounds, 0, Array.Empty<DockRect>()).Count));
        tests.Test("covered caption has no drag region", () => Check.Equal(0, ChromeGeometry.CaptionRegions(bounds, 32, new[] { bounds }).Count));
        tests.Test("overlapping interactive holes are unioned", () =>
        {
            var regions = ChromeGeometry.CaptionRegions(bounds, 32, new[] { new DockRect(20, 0, 50, 32), new DockRect(40, 0, 70, 32) });
            Check.Equal(2, regions.Count);
            Check.Near(20, regions[0].Width);
            Check.Near(110, regions[1].X);
        });
        tests.Test("adjacent caption bands coalesce", () =>
        {
            var regions = ChromeGeometry.CaptionRegions(bounds, 32, new[] { new DockRect(20, 0, 10, 16), new DockRect(20, 16, 10, 16) });
            Check.Equal(2, regions.Count);
            Check.Near(32, regions[0].Height);
        });
        tests.Test("nonfinite chrome dimensions rejected", () => Check.Throws<ArgumentOutOfRangeException>(() => ChromeGeometry.CaptionRegions(bounds, double.NaN, Array.Empty<DockRect>())));
        tests.Test("negative chrome border rejected", () => Check.Throws<ArgumentOutOfRangeException>(() => ChromeGeometry.HitTest(bounds, new(), 32, -1, 0, 0, 0, true, false)));
        foreach (var hit in Enum.GetValues<ChromeHit>())
            tests.Test("frame drag constraints: " + hit, () =>
            {
                var start = new DockRect(10, 20, 400, 300);
                var result = ChromeResize.Apply(start, hit, 10000, -10000, 160, 100, 600, 500);
                if (hit is ChromeHit.Client or ChromeHit.Caption)
                {
                    Check.Near(start.Width, result.Width);
                    Check.Near(start.Height, result.Height);
                }
                else
                {
                    Check.True(result.Width is >= 160 and <= 600);
                    Check.True(result.Height is >= 100 and <= 500);
                }

                if (hit is ChromeHit.Left or ChromeHit.TopLeft or ChromeHit.BottomLeft)
                    Check.Near(start.Right, result.Right);
                if (hit is ChromeHit.Top or ChromeHit.TopLeft or ChromeHit.TopRight)
                    Check.Near(start.Bottom, result.Bottom);
                if (hit is ChromeHit.Right or ChromeHit.TopRight or ChromeHit.BottomRight)
                    Check.Near(start.X, result.X);
            });
        tests.Test("frame drag uses initial rather than cumulative delta", () =>
        {
            var start = new DockRect(20, 30, 400, 300);
            Check.Equal(new DockRect(27, 33, 393, 297), ChromeResize.Apply(start, ChromeHit.TopLeft, 7, 3, 160, 100));
        });
        tests.Test("frame drag rejects invalid enum", () => Check.Throws<ArgumentOutOfRangeException>(() => ChromeResize.Apply(bounds, (ChromeHit)999, 0, 0, 0, 0)));
        tests.Test("frame drag rejects nonfinite delta", () => Check.Throws<ArgumentOutOfRangeException>(() => ChromeResize.Apply(bounds, ChromeHit.Left, double.NaN, 0, 0, 0)));
        tests.Test("frame drag rejects reversed size range", () => Check.Throws<ArgumentOutOfRangeException>(() => ChromeResize.Apply(bounds, ChromeHit.Left, 0, 0, 100, 0, 50)));
        tests.Test("hit test rejects invalid interactive geometry", () => Check.Throws<ArgumentOutOfRangeException>(() => ChromeGeometry.HitTest(bounds, new(10, 10), 32, 5, 5, 5, 5, true, false, new[] { new DockRect(0, 0, double.NaN, 2) })));
        tests.Test("random caption partition equals pointwise set difference", () =>
        {
            var random = new Random(98147);
            for (var iteration = 0; iteration < 1500; iteration++)
            {
                var holes = Enumerable.Range(0, random.Next(0, 16)).Select(_ => new DockRect(random.Next(-10, 90), random.Next(-10, 40), random.Next(0, 50), random.Next(0, 50))).ToArray();
                var regions = ChromeGeometry.CaptionRegions(new(0, 0, 100, 80), 30, holes);
                for (var sample = 0; sample < 50; sample++)
                {
                    var x = random.NextDouble() * 100;
                    var y = random.NextDouble() * 30;
                    bool Contains(DockRect r) => x >= r.X && x < r.Right && y >= r.Y && y < r.Bottom;
                    Check.Equal(holes.Any(Contains) ? 0 : 1, regions.Count(Contains));
                }
            }
        });
    }
}
