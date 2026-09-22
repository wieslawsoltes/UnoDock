using System.Reflection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;
using Xceed.Wpf.AvalonDock;
using Xceed.Wpf.AvalonDock.Controls;
using Xceed.Wpf.AvalonDock.Layout;

namespace UnoDock.Testing;
using LayoutPanel = Xceed.Wpf.AvalonDock.Layout.LayoutPanel;

public static class InteractionTests
{
    public static async Task<int> Run(DockingManager host, string output)
    {
        var tests = new TestRunner();
        var original = host.Layout; var originalMode = host.FloatingWindowMode; var originalCoordinates = host.CrossWindowCoordinates;
        tests.Test("native coordinate provider is installed by default", () =>
        { using var manager = new DockingManager(); Check.True(manager.CrossWindowCoordinates is DesktopWindowCoordinates); });
        tests.Test("native coordinates reject detached visuals", () =>
        { Check.Throws<InvalidOperationException>(()=>new DesktopWindowCoordinates().Translate(new Grid(),default,new Grid())); });
        tests.Test("native coordinates reject nonfinite point", () =>
        { Check.Throws<ArgumentOutOfRangeException>(()=>new DesktopWindowCoordinates().Translate(new Grid(),new(double.NaN,0),new Grid())); });
        tests.Test("header hit creates insertion rather than top split", async () =>
        {
            var docs=await Setup(host,8);var pane=Pane(host);var scroll=Header(pane);var point=At(scroll,Surface(host),.5,.5);
            var plan=host.GetDropPlan(docs[0],point);
            Check.True(plan != null);Check.Equal(DropTargetType.DocumentPaneDockInside,plan!.Type);Check.True(plan.InsertionIndex>=0);
        });
        tests.Test("body center retains inside docking without header index", async () =>
        {
            var docs=await Setup(host,4);var point=At(Pane(host),Surface(host),.5,.65);
            var plan=host.GetDropPlan(docs[0],point);Check.True(plan != null);Check.Equal(-1,plan!.InsertionIndex);
        });
        tests.Test("hidden models do not shift visible insertion indices", async () =>
        {
            var docs=await Setup(host,4);typeof(LayoutDocument).GetProperty(nameof(LayoutDocument.IsVisible))!.SetValue(docs[0], false);host.Refresh();host.UpdateLayout();
            var tab=Pane(host).FindVisualChildren<LayoutDocumentTabItem>().First(t=>ReferenceEquals(t.Model,docs[1]));
            var point=At(tab,Surface(host),.05,.5);var plan=host.GetDropPlan(docs[3],point);
            Check.True(plan != null);Check.Equal(1,plan!.InsertionIndex);Check.True(plan.Execute());Check.Same(docs[3],((LayoutDocumentPane)docs[3].Parent!).Children[1]);
        });
        tests.Test("drag auto-scroll advances overflowing headers", async () =>
        {
            await Setup(host,40);var pane=Pane(host);var scroll=Header(pane);
            Check.True(scroll.ScrollableWidth>0);scroll.ChangeView(0,null,null,true);host.UpdateLayout();
            var point=At(scroll,Surface(host),.99,.4);var before=scroll.HorizontalOffset;
            Call(pane,"ScrollHeaderAt",point,Surface(host),.05);await Task.Delay(50);host.UpdateLayout();
            Check.True(scroll.HorizontalOffset>before,"Header scroll offset did not advance.");
        });
        tests.Test("drag auto-scroll ignores editor body", async () =>
        {
            await Setup(host,40);var pane=Pane(host);var scroll=Header(pane);var before=scroll.HorizontalOffset;
            Check.Equal(false,(bool)Call(pane,"ScrollHeaderAt",At(pane,Surface(host),.99,.7),Surface(host),.05)!);
            Check.Near(before,scroll.HorizontalOffset);
        });
        tests.Test("same-root translation retains transforms", async () =>
        {
            await Setup(host,3);var source=Header(Pane(host));var target=Surface(host);var p=new Point(13.25,4.75);
            var expected=source.TransformToVisual(target).TransformPoint(p);var actual=new DesktopWindowCoordinates().Translate(source,p,target);
            Check.Near(expected.X,actual.X);Check.Near(expected.Y,actual.Y);
        });
        tests.Test("transformed drop bounds enclose all four corners", async () =>
        {
            await Setup(host,3);var source=Pane(host);var target=Surface(host);
            source.RenderTransform=new RotateTransform { Angle=25 };
            try
            {
                var area=new DropArea<LayoutDocumentPaneControl>(source,DropAreaType.DocumentPane,target);
                foreach(var point in new[] {new Point(0,0),new Point(source.ActualWidth,0),new Point(0,source.ActualHeight),new Point(source.ActualWidth,source.ActualHeight)})
                {
                    var mapped=source.TransformToVisual(target).TransformPoint(point);
                    Check.True(mapped.X>=area.DetectionRect.Left-1e-6 && mapped.X<=area.DetectionRect.Right+1e-6);
                    Check.True(mapped.Y>=area.DetectionRect.Top-1e-6 && mapped.Y<=area.DetectionRect.Bottom+1e-6);
                }
            }
            finally {source.RenderTransform=null;}
        });
        tests.Test("in-surface activation updates both hit-test and visual stacking order", async () =>
        {
            var docs = await Setup(host, 3);
            docs[0].FloatingLeft = 100; docs[0].FloatingTop = 100; docs[0].Float();
            docs[1].FloatingLeft = 100; docs[1].FloatingTop = 100; docs[1].Float();
            host.Refresh(); host.UpdateLayout(); await Task.Delay(50);
            var first = host.FloatingWindows.First(w => ReferenceEquals(((LayoutDocumentFloatingWindow)w.Model).RootDocument, docs[0]));
            var second = host.FloatingWindows.First(w => ReferenceEquals(((LayoutDocumentFloatingWindow)w.Model).RootDocument, docs[1]));
            first.Activate(); host.UpdateLayout();
            Check.True(Canvas.GetZIndex(first) > Canvas.GetZIndex(second));
            second.Activate(); host.UpdateLayout();
            Check.True(Canvas.GetZIndex(second) > Canvas.GetZIndex(first));
        });
        tests.Test("floating single-content item supports the public dependency-property setter", () =>
        {
            using var manager = new DockingManager(); var content = new LayoutAnchorable();
            manager.Layout = new() { RootPanel = new(new LayoutAnchorablePane(content)) };
            var control = new LayoutAnchorableFloatingWindowControl(new LayoutAnchorableFloatingWindow());
            var item = manager.GetLayoutItemFromModel(content);
            control.SingleContentLayoutItem = item; Check.Same(item, control.SingleContentLayoutItem);
            control.ClearValue(LayoutAnchorableFloatingWindowControl.SingleContentLayoutItemProperty);
            Check.True(control.SingleContentLayoutItem == null);
        });
        if (OperatingSystem.IsLinux() && !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DISPLAY")))
        {
            tests.Test("X11 stale window ID returns failure without terminating host", () =>
            {
                var method=typeof(DesktopWindowCoordinates).GetMethod("TryTranslateX11Origins",BindingFlags.NonPublic|BindingFlags.Static)!;
                Check.Equal(false,(bool)method.Invoke(null,[unchecked((nint)uint.MaxValue),(nint)1,null])!);
            });
            tests.Test("X11 two-window translation tracks native moves and roundtrips", async () =>
            {
                var a=new Window {Content=new Grid {Padding=new Thickness(17),Children={new Button {Content="A"}}}};
                var b=new Window {Content=new Grid {Padding=new Thickness(31),Children={new Button {Content="B"}}}};
                try
                {
                    a.AppWindow.Move(new() { X=100, Y=100 });b.AppWindow.Move(new() { X=700, Y=180 });a.Activate();b.Activate();await Task.Delay(150);
                    var from=((Grid)a.Content).Children[0] as FrameworkElement;var to=((Grid)b.Content).Children[0] as FrameworkElement;
                    var coordinates=new DesktopWindowCoordinates();var p=new Point(10.25,21.75);var q=coordinates.Translate(from!,p,to!);var back=coordinates.Translate(to!,q,from!);
                    Check.Near(p.X,back.X,1e-4);Check.Near(p.Y,back.Y,1e-4);
                    var before=b.AppWindow.Position;b.AppWindow.Move(new() { X=before.X+70, Y=before.Y+40 });await Task.Delay(100);
                    var moved=coordinates.Translate(from!,p,to!);
                    Check.Near(q.X-70/to!.XamlRoot.RasterizationScale,moved.X,1);Check.Near(q.Y-40/to!.XamlRoot.RasterizationScale,moved.Y,1);
                }
                finally {a.Close();b.Close();await Task.Delay(100);}
            });
            tests.Test("X11 floating tool receives cross-window insertion and returns to main", async () =>
            {
                var (source,keep,target)=await NativeWorkspace(host);var floating=host.FloatingWindows.Single();
                var pane=floating.FindVisualChildren<LayoutAnchorablePaneControl>().Single();var point=new DesktopWindowCoordinates().Translate(Header(pane),new(40,15),Surface(host));
                var plan=host.GetDropPlan(source,point);Check.True(plan != null,"No native-window insertion plan.");
                Check.Equal(DropTargetType.AnchorablePaneDockInside,plan!.Type);Check.True(plan.Execute());Check.Same(target.Parent,source.Parent);
                host.Refresh();host.UpdateLayout();
                var mainPane=host.FindVisualChildren<LayoutAnchorablePaneControl>().Single(p=>ReferenceEquals(((ILayoutControl)p).Model,keep.Parent));
                var backPoint=At(Header(mainPane),Surface(host),.5,.5);var back=host.GetDropPlan(source,backPoint);
                Check.True(back != null);Check.True(back!.Execute());Check.Same(keep.Parent,source.Parent);
            });
            tests.Test("X11 native preview is painted in destination window", async () =>
            {
                var (source,_,target)=await NativeWorkspace(host);var floating=host.FloatingWindows.Single();var pane=floating.FindVisualChildren<LayoutAnchorablePaneControl>().Single();
                var coordinates=new DesktopWindowCoordinates();var point=coordinates.Translate(Header(pane),new(40,15),Surface(host));var plan=host.GetDropPlan(source,point)!;
                Check.True(plan != null);Call(floating,"ShowDropPreview",plan,Surface(host),new SolidColorBrush(Microsoft.UI.Colors.Blue));
                var overlay=floating.FindVisualChildren<OverlayWindow>().Single();Check.True(overlay.IsOpen);Check.Same(plan,overlay.CurrentPlan);
                Call(floating,"HideDropPreview");Check.False(overlay.IsOpen);
            });
            tests.Test("X11 unrelated native window blocks underlying drop targets", async () =>
            {
                var (source, _, target) = await NativeWorkspace(host);
                var floating = host.FloatingWindows.Single();
                var pane = floating.FindVisualChildren<LayoutAnchorablePaneControl>().Single();
                var point = new DesktopWindowCoordinates().Translate(Header(pane), new(40, 15), Surface(host));
                var blocker = new Window { Content = new Grid(), Title = "Drop occlusion test" };
                try
                {
                    blocker.AppWindow.Move(floating.NativeWindow!.AppWindow.Position);
                    blocker.AppWindow.Resize(floating.NativeWindow.AppWindow.Size);
                    blocker.Activate(); await Task.Delay(100);
                    Check.True(host.GetDropPlan(source, point) == null, "Drop plan targeted an occluded client.");
                    blocker.AppWindow.Move(new() { X = 2200, Y = 1100 }); await Task.Delay(100);
                    Check.True(host.GetDropPlan(source, point) != null, "Drop target did not recover after the occluding window moved away.");
                }
                finally { blocker.Close(); await Task.Delay(100); }

            });
            tests.Test("X11 raised main window wins over an overlapping native float", async () =>
            {
                var (source, _, target) = await NativeWorkspace(host);
                var floating = host.FloatingWindows.Single();
                floating.NativeWindow!.AppWindow.Move(new() { X = 300, Y = 100 });
                await Task.Delay(100);
                var pane = floating.FindVisualChildren<LayoutAnchorablePaneControl>().Single();
                var point = new DesktopWindowCoordinates().Translate(Header(pane), new(40, 15), Surface(host));
                var before = host.GetDropPlan(source, point);
                Check.True(before != null); Check.Same(target.Parent, before!.Target);
                var mainWindow = Uno.UI.ApplicationHelper.Windows.Single(w => ReferenceEquals(w.Content?.XamlRoot, host.XamlRoot));
                mainWindow.Activate(); await Task.Delay(100);
                var after = host.GetDropPlan(source, point);
                Check.True(after == null || !ReferenceEquals(after.Target, target.Parent), "Hit testing ignored native window stacking order.");
            });
            tests.Test("X11 docking closes and unmaps its native client before render teardown", async () =>
            {
                var (_, _, target) = await NativeWorkspace(host);
                var floating = host.FloatingWindows.Single();
                floating.NativeWindow!.AppWindow.Move(new() { X = 300, Y = 100 }); await Task.Delay(100);
                var point = new DesktopWindowCoordinates().Translate(floating, new(40, 80), Surface(host));
                target.Dock(); host.Refresh(); await Task.Delay(100);
                Check.True(floating.NativeWindow == null);
                object?[] query = [Surface(host), point, null];
                var method = typeof(DesktopWindowCoordinates).GetMethod("TryGetTopmostRoot", BindingFlags.NonPublic | BindingFlags.Instance)!;
                Check.Equal(true, (bool)method.Invoke(host.CrossWindowCoordinates, query)!);
                Check.Same(host.XamlRoot, query[2]);
            });
            tests.Test("unsupported cross-window provider omits areas and preserves layout", async () =>
            {
                var (source,_,_)=await NativeWorkspace(host);var before=source.Parent;host.CrossWindowCoordinates=new UnavailableCoordinates();
                Check.True(host.GetDropAreas().All(a=>double.IsFinite(a.DetectionRect.Width)));
                Check.Same(before,source.Parent);host.CrossWindowCoordinates=new DesktopWindowCoordinates();
            });
        }
        if (OperatingSystem.IsLinux() && Environment.GetEnvironmentVariable("UNODOCK_NATIVE_INPUT_TESTS") == "1")
        {
            tests.Test("X11 pointer capture docks a main tool into a native window", async () =>
            {
                var (source, _, target) = await NativeWorkspace(host);
                var pane = host.FindVisualChildren<LayoutAnchorablePaneControl>().Single();
                var tab = pane.FindVisualChildren<LayoutAnchorableTabItem>().First(t => ReferenceEquals(t.Model, source));
                var label = Label(tab);
                var floating = host.FloatingWindows.Single();
                var destination = Header(floating.FindVisualChildren<LayoutAnchorablePaneControl>().Single());
                using var input = new X11TestInput();
                await input.Begin(label, new(20, label.ActualHeight / 2));
                Check.Equal(UnoDock.Core.DockDragState.Dragging, DragState(host));
                await input.Drop(destination, new(40, destination.ActualHeight / 2));
                Check.Same(target.Parent, source.Parent);
                Check.Equal(UnoDock.Core.DockDragState.Committed, DragState(host));
                Check.False(DragTimer(host).IsEnabled);
            });
            tests.Test("X11 document caption pointer capture docks back to main", async () =>
            {
                var docs = await Setup(host, 3);
                host.FloatingWindowMode = FloatingWindowMode.Native;
                docs[0].FloatingLeft = 1600; docs[0].FloatingTop = 100;
                docs[0].Float(); host.Refresh(); await Task.Delay(200);
                var floating = host.FloatingWindows.Single();
                var caption = (TextBlock)typeof(LayoutFloatingWindowControl).GetField("_caption", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(floating)!;
                var destination = Header(Pane(host));
                using var input = new X11TestInput();
                await input.Begin(caption, new(20, caption.ActualHeight / 2));
                Check.Equal(UnoDock.Core.DockDragState.Dragging, DragState(host));
                await input.Drop(destination, new(50, destination.ActualHeight / 2));
                Check.Same(docs[1].Parent, docs[0].Parent);
                Check.False(docs[0].IsFloating);
            });
            tests.Test("X11 Escape cancels captured drag without reordering", async () =>
            {
                var docs = await Setup(host, 8); var pane = Pane(host);
                var label = Label(pane.FindVisualChildren<LayoutDocumentTabItem>().First(t => ReferenceEquals(t.Model, docs[0])));
                using var input = new X11TestInput();
                await input.Begin(label, new(20, label.ActualHeight / 2));
                Check.Equal(UnoDock.Core.DockDragState.Dragging, DragState(host));
                input.Escape(); await Task.Delay(100);
                Check.Equal(UnoDock.Core.DockDragState.Cancelled, DragState(host));
                Check.False(DragTimer(host).IsEnabled);
                input.Release(); await Task.Delay(50);
                Check.Same(docs[0], ((LayoutDocumentPane)docs[0].Parent!).Children[0]);
            });
            tests.Test("X11 stationary edge hover scrolls during capture and stops on release", async () =>
            {
                var docs = await Setup(host, 40); var pane = Pane(host); var scroll = Header(pane);
                var label = Label(pane.FindVisualChildren<LayoutDocumentTabItem>().First(t => ReferenceEquals(t.Model, docs[0])));
                using var input = new X11TestInput();
                await input.Begin(label, new(20, label.ActualHeight / 2));
                input.MoveTo(scroll, new(scroll.ActualWidth - 2, 15));
                await Task.Delay(100); var before = scroll.HorizontalOffset;
                await Task.Delay(250);
                Check.True(scroll.HorizontalOffset > before + 5, "Scrolling did not continue while the pointer was stationary.");
                input.Release(); await Task.Delay(100);
                Check.False(DragTimer(host).IsEnabled);
                var final = scroll.HorizontalOffset; await Task.Delay(100);
                Check.Near(final, scroll.HorizontalOffset);
            });
            tests.Test("X11 source removal cancels capture and timer", async () =>
            {
                var docs = await Setup(host, 4); var pane = Pane(host);
                var label = Label(pane.FindVisualChildren<LayoutDocumentTabItem>().First(t => ReferenceEquals(t.Model, docs[0])));
                using var input = new X11TestInput();
                await input.Begin(label, new(20, label.ActualHeight / 2));
                docs[0].Close(); host.Refresh(); await Task.Delay(100);
                Check.Equal(UnoDock.Core.DockDragState.Cancelled, DragState(host));
                Check.False(DragTimer(host).IsEnabled);
                Check.True(docs[0].Parent == null);
            });
        }
        try {return await tests.Run(output,"interaction");}
        finally {host.CrossWindowCoordinates=originalCoordinates;host.FloatingWindowMode=originalMode;host.Layout=original;host.Refresh();await Task.Delay(100);}
    }
    private static Button Label(LayoutTabItemBase tab) => (Button)typeof(LayoutTabItemBase).GetField("_label", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(tab)!;
    private static UnoDock.Core.DockDragState DragState(DockingManager host) => ((UnoDock.Core.DockDragSession)Surface(host).GetType().GetField("_drag", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(Surface(host))!).State;
    private static DispatcherTimer DragTimer(DockingManager host) => (DispatcherTimer)Surface(host).GetType().GetField("_dragScrollTimer", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(Surface(host))!;
    private static async Task<LayoutDocument[]> Setup(DockingManager host,int count)
    {
        host.FloatingWindowMode=FloatingWindowMode.InSurface;var docs=Enumerable.Range(0,count).Select(i=>new LayoutDocument {Title=$"Document {i:00} long header",ContentId=$"d{i}",Content=new TextBox {Text=$"Editor {i}"}}).ToArray();
        var pane=new LayoutDocumentPane();foreach(var doc in docs)pane.Children.Add(doc);host.Layout=new(){RootPanel=new(pane)};host.Refresh();host.UpdateLayout();await Task.Delay(50);host.UpdateLayout();return docs;
    }
    private static async Task<(LayoutAnchorable Source,LayoutAnchorable Keep,LayoutAnchorable Target)> NativeWorkspace(DockingManager host)
    {
        host.CrossWindowCoordinates=new DesktopWindowCoordinates();host.FloatingWindowMode=FloatingWindowMode.Native;
        var source=new LayoutAnchorable {Title="Source",ContentId="source"};var keep=new LayoutAnchorable {Title="Keep",ContentId="keep"};var target=new LayoutAnchorable {Title="Target",ContentId="target"};
        var pane=new LayoutAnchorablePane(source);pane.Children.Add(keep);pane.Children.Add(target);
        host.Layout=new(){RootPanel=new(pane){Children={new LayoutDocumentPane(new LayoutDocument {Title="Editor"})}}};
        host.Refresh();host.UpdateLayout();target.FloatingLeft=1600;target.FloatingTop=100;target.FloatingWidth=550;target.FloatingHeight=400;target.Float();host.Refresh();
        await Task.Delay(200);host.UpdateLayout();var floating=host.FloatingWindows.Single();Check.True(floating.NativeWindow != null);floating.UpdateLayout();return(source,keep,target);
    }
    private sealed class UnavailableCoordinates:ICrossWindowCoordinates {public Point Translate(FrameworkElement source,Point p,FrameworkElement destination)=>throw new PlatformNotSupportedException("Test unsupported host");}
    private static LayoutDocumentPaneControl Pane(DockingManager host)=>host.FindVisualChildren<LayoutDocumentPaneControl>().Single();
    private static ScrollViewer Header(LayoutCachePaneControl pane)=>(ScrollViewer)typeof(LayoutCachePaneControl).GetField("_scroll",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(pane)!;
    private static FrameworkElement Surface(DockingManager host)=>(FrameworkElement)typeof(DockingManager).GetField("_surface",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(host)!;
    private static Point At(FrameworkElement from,FrameworkElement to,double x,double y)=>from.TransformToVisual(to).TransformPoint(new(from.ActualWidth*x,from.ActualHeight*y));
    private static object? Call(object value,string method,params object?[] args)
    {
        for(var type=value.GetType();type!=null;type=type.BaseType)
            if(type.GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.DeclaredOnly) is { } member)
                try {return member.Invoke(value,args);}catch(TargetInvocationException e) when(e.InnerException!=null){System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(e.InnerException).Throw();}
        throw new MissingMethodException(method);
    }
}
