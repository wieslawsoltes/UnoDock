using UnoDock.Core;
using UnoDock.Testing;

internal static class InteractionGeometryTests
{
    internal static void Register(TestRunner tests)
    {
        tests.Test("coordinates preserve fractional DPI and negative monitor positions", () =>
        {
            var p = DockInteractionGeometry.TranslateClientPoint(new(10.25, 20.5), new(-1800, 75), 1.25, 2);
            Check.Near(-893.59375, p.X); Check.Near(50.3125, p.Y);
        });
        tests.Test("coordinates randomized roundtrips (10000 cases)", () =>
        {
            var r = new Random(347612);
            for (var i = 0; i < 10000; i++)
            {
                var p = new DockPoint(r.NextDouble() * 4000 - 2000, r.NextDouble() * 4000 - 2000);
                var offset = new DockPoint(r.Next(-10000,10000), r.Next(-10000,10000));
                var a = .5 + r.NextDouble() * 3; var b = .5 + r.NextDouble() * 3;
                var q = DockInteractionGeometry.TranslateClientPoint(p, offset, a, b);
                var back = DockInteractionGeometry.TranslateClientPoint(q, new(-offset.X,-offset.Y), b, a);
                Check.Near(p.X, back.X, 1e-8); Check.Near(p.Y, back.Y, 1e-8);
            }
        });
        foreach (var invalid in new[] { 0, -1, double.NaN, double.PositiveInfinity })
            tests.Test("coordinates reject invalid rasterization scale " + invalid, () =>
            {
                Check.Throws<ArgumentOutOfRangeException>(() => DockInteractionGeometry.TranslateClientPoint(default,default,invalid,1));
                Check.Throws<ArgumentOutOfRangeException>(() => DockInteractionGeometry.TranslateClientPoint(default,default,1,invalid));
            });
        tests.Test("coordinates reject nonfinite points and overflow", () =>
        {
            Check.Throws<ArgumentOutOfRangeException>(() => DockInteractionGeometry.TranslateClientPoint(new(double.NaN,0),default,1,1));
            Check.Throws<ArgumentOutOfRangeException>(() => DockInteractionGeometry.TranslateClientPoint(default,new(0,double.NegativeInfinity),1,1));
            Check.Throws<ArgumentOutOfRangeException>(() => DockInteractionGeometry.TranslateClientPoint(new(double.MaxValue,0),default,2,1));
        });
        tests.Test("auto-scroll center and outside are stationary", () =>
        { foreach (var x in new[] { -1d, 32, 100, 168, 201 }) Check.Near(0, Delta(x)); });
        tests.Test("auto-scroll speed is quadratic at the edge", () =>
        { Check.Near(-9,Delta(0)); Check.Near(-2.25,Delta(16)); Check.Near(9,Delta(200)); });
        tests.Test("auto-scroll clamps both ends", () =>
        {
            Check.Near(0,DockInteractionGeometry.AutoScrollDelta(0,200,0,300,.01));
            Check.Near(0,DockInteractionGeometry.AutoScrollDelta(200,200,300,300,.01));
            Check.Near(-2,DockInteractionGeometry.AutoScrollDelta(0,200,2,300,.01));
            Check.Near(2,DockInteractionGeometry.AutoScrollDelta(200,200,298,300,.01));
        });
        tests.Test("auto-scroll RTL reverses logical offsets", () => Check.Near(-Delta(0),Delta(0,true)));
        tests.Test("auto-scroll stalled frame is capped", () => Check.Near(-45,DockInteractionGeometry.AutoScrollDelta(0,200,150,300,10)));
        tests.Test("auto-scroll narrow viewport has disjoint edge regions", () =>
        { Check.Near(0,DockInteractionGeometry.AutoScrollDelta(10,20,150,300,.01)); });
        tests.Test("auto-scroll empty extent is stationary", () =>
        {
            Check.Near(0,DockInteractionGeometry.AutoScrollDelta(0,0,0,0,.01));
            Check.Near(0,DockInteractionGeometry.AutoScrollDelta(0,200,0,0,.01));
        });
        tests.Test("auto-scroll rejects invalid inputs", () =>
        {
            Check.Throws<ArgumentOutOfRangeException>(()=>DockInteractionGeometry.AutoScrollDelta(double.NaN,200,0,100,.01));
            Check.Throws<ArgumentOutOfRangeException>(()=>DockInteractionGeometry.AutoScrollDelta(0,-1,0,100,.01));
            Check.Throws<ArgumentOutOfRangeException>(()=>DockInteractionGeometry.AutoScrollDelta(0,200,-1,100,.01));
            Check.Throws<ArgumentOutOfRangeException>(()=>DockInteractionGeometry.AutoScrollDelta(0,200,0,-1,.01));
            Check.Throws<ArgumentOutOfRangeException>(()=>DockInteractionGeometry.AutoScrollDelta(0,200,0,100,-1));
            Check.Throws<ArgumentOutOfRangeException>(()=>DockInteractionGeometry.AutoScrollDelta(0,200,0,100,.01,edgeWidth:0));
            Check.Throws<ArgumentOutOfRangeException>(()=>DockInteractionGeometry.AutoScrollDelta(0,200,0,100,.01,maximumSpeed:double.PositiveInfinity));
        });
        tests.Test("auto-scroll randomized bounds and direction (10000 cases)", () =>
        {
            var r = new Random(8491);
            for(var i=0;i<10000;i++)
            {
                var width=1+r.NextDouble()*2000;var max=r.NextDouble()*5000;var offset=r.NextDouble()*max;
                var x=r.NextDouble()*width;var rtl=r.Next(2)==0;
                var delta=DockInteractionGeometry.AutoScrollDelta(x,width,offset,max,r.NextDouble(),rtl);
                Check.True(double.IsFinite(delta));Check.True(offset+delta >= -1e-9 && offset+delta <= max+1e-9);
                if(delta != 0) Check.Equal(x>width/2 != rtl,delta>0);
            }
        });
    }
    private static double Delta(double x,bool rtl=false)=>DockInteractionGeometry.AutoScrollDelta(x,200,150,300,.01,rtl);
}
