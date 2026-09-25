using UnoDock.Core;
using UnoDock.Internal;
using UnoDock.Testing;

internal static class FloatingResizeBoundsTests
{
    internal static void Register(TestRunner tests)
    {
        var initial = new DockRect(10, 20, 400, 300); var target = new DockRect(30, 40, 420, 320);
        tests.Test("resize bounds: rejects unrequested application geometry", () =>
        {
            var ledger = new FloatingResizeBoundsTracker(initial, false);
            Check.True(ledger.Observe(initial)); Check.False(ledger.Observe(target));
            Check.True(ledger.Request(target)); Check.True(ledger.Observe(target));
            Check.False(ledger.Observe(initial)); Check.False(ledger.Observe(target with { X = target.X + 1 }));
        });
        tests.Test("resize bounds: atomic backend rejects split acknowledgements", () =>
        {
            var ledger = new FloatingResizeBoundsTracker(initial, false); Check.True(ledger.Request(target));
            Check.False(ledger.Observe(initial with { Width = target.Width, Height = target.Height }));
            Check.False(ledger.Observe(target with { Width = initial.Width, Height = initial.Height }));
            Check.True(ledger.Observe(target));
        });
        tests.Test("resize bounds: X11 size and position acknowledge independently", () =>
        {
            var ledger = new FloatingResizeBoundsTracker(initial, true); Check.True(ledger.Request(target));
            Check.True(ledger.Observe(initial with { Width = target.Width, Height = target.Height }));
            Check.True(ledger.Observe(target with { Width = initial.Width, Height = initial.Height }));
            Check.False(ledger.Observe(target with { Width = target.Width + 7 })); Check.True(ledger.Observe(target));
            Check.False(ledger.Observe(initial));
        });
        tests.Test("resize bounds: several delayed WM acknowledgements stay ordered", () =>
        {
            var ledger = new FloatingResizeBoundsTracker(initial, true); var second = new DockRect(70, 80, 500, 600);
            Check.True(ledger.Request(target)); Check.True(ledger.Request(second)); Check.True(ledger.Observe(initial));
            Check.True(ledger.Observe(target)); Check.True(ledger.Observe(target with { Width = second.Width, Height = second.Height }));
            Check.True(ledger.Observe(second)); Check.False(ledger.Observe(target));
        });
        tests.Test("resize bounds: revisiting a rectangle retires all older requests", () =>
        {
            var ledger = new FloatingResizeBoundsTracker(initial, true);
            Check.True(ledger.Request(target)); Check.True(ledger.Request(initial)); Check.True(ledger.Observe(initial));
            Check.False(ledger.Observe(target));
        });
        tests.Test("resize bounds: unacknowledged requests are bounded and recoverable", () =>
        {
            var ledger = new FloatingResizeBoundsTracker(initial, false);
            for (var i = 1; i <= 64; i++) Check.True(ledger.Request(initial with { Width = initial.Width + i }));
            Check.False(ledger.Request(target)); Check.True(ledger.Observe(initial with { Width = initial.Width + 64 }));
            Check.True(ledger.Request(target)); Check.True(ledger.Observe(target));
        });
        tests.Test("resize bounds: nonfinite native geometry cannot authorize a write", () =>
        {
            var ledger = new FloatingResizeBoundsTracker(initial, true); Check.True(ledger.Request(target));
            foreach (var value in new[] { double.NaN, double.NegativeInfinity, double.PositiveInfinity })
            { Check.False(ledger.Observe(target with { X = value })); Check.False(ledger.Observe(target with { Height = value })); }
        });
        tests.Test("resize bounds: 10000 deterministic delayed frame sequences", () =>
        {
            var random = new Random(193871);
            for (var trial = 0; trial < 10000; trial++)
            {
                var ledger = new FloatingResizeBoundsTracker(initial, true);
                var pending = Enumerable.Range(0, random.Next(1, 20)).Select(i => new DockRect(
                    1000 + i * 17, 2000 + i * 13, 500 + i * 11, 400 + i * 7)).ToArray();
                foreach (var next in pending) Check.True(ledger.Request(next));
                var previous = initial;
                foreach (var next in pending)
                {
                    Check.True(ledger.Observe(previous with { Width = next.Width, Height = next.Height }));
                    Check.False(ledger.Observe(next with { X = -9999 }));
                    Check.True(ledger.Observe(next)); previous = next;
                }
                Check.False(ledger.Observe(initial)); Check.True(ledger.Observe(pending[^1]));
            }
        });
    }
}
