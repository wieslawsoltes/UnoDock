using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Input;
using UnoDock.Controls;
using UnoDock.Core;
using UnoDock.Layout;
using Windows.Foundation;

namespace UnoDock.Testing;

internal static partial class FloatingChromeTests
{
    internal static async Task<int> RunResizePolicy(string output, bool tools)
    {
        var tests = new TestRunner(); var kind = tools ? "tools" : "document";
        tests.Test($"resize-policy/{kind}: RTL grips remain on physical native edges", async () =>
        {
            using var f = new Fixture(tools); await f.Show();
            f.Control.FlowDirection = FlowDirection.RightToLeft; await Task.Delay(80);
            using var coordinates = new DesktopWindowCoordinates();
            var bounds = FloatingChromeProbe.Bounds(f.Native);
            foreach (var edge in Edges)
            {
                var grip = f.Grip(edge);
                var center = coordinates.ToScreen(grip, new(grip.ActualWidth / 2, grip.ActualHeight / 2));
                if (OperatingSystem.IsMacOS()) center.Y = -center.Y;
                if (edge is ChromeHit.Left or ChromeHit.TopLeft or ChromeHit.BottomLeft)
                    Check.True(center.X < bounds.X + bounds.Width / 4, "A physical left grip was mirrored to the right: " + edge);
                if (edge is ChromeHit.Right or ChromeHit.TopRight or ChromeHit.BottomRight)
                    Check.True(center.X > bounds.Right - bounds.Width / 4, "A physical right grip was mirrored to the left: " + edge);
                if (edge is ChromeHit.Top or ChromeHit.TopLeft or ChromeHit.TopRight)
                    Check.True(center.Y < bounds.Y + bounds.Height / 4);
                if (edge is ChromeHit.Bottom or ChromeHit.BottomLeft or ChromeHit.BottomRight)
                    Check.True(center.Y > bounds.Bottom - bounds.Height / 4);
            }
            Check.Equal(FlowDirection.RightToLeft, f.Caption.FlowDirection); f.AssertEditors();
        });
        tests.Test($"resize-policy/{kind}: zero border sides cannot resize through their corners", async () =>
        {
            using var f = new Fixture(tools); await f.Show();
            f.Control.ResizeBorderThickness = new(0, 5, 5, 0); await Task.Delay(60);
            foreach (var edge in new[] { ChromeHit.Left, ChromeHit.Bottom, ChromeHit.TopLeft, ChromeHit.BottomLeft, ChromeHit.BottomRight })
            {
                Check.Equal(Visibility.Collapsed, f.Grip(edge).Visibility);
                Check.True(Call(f.Control, "BeginFrameResize", edge, new Point(0, 0), null, null) == null,
                    "An absent border still accepts a resize: " + edge);
            }
            foreach (var edge in new[] { ChromeHit.Top, ChromeHit.Right, ChromeHit.TopRight })
            { Check.Equal(Visibility.Visible, f.Grip(edge).Visibility); Check.True(BeginResize(f, edge) != null); Call(f.Control, "CancelFrameResize", true); }
            f.AssertEditors();
        });
        tests.Test($"resize-policy/{kind}: a stationary resize does not clamp an existing oversized frame", async () =>
        {
            using var f = new Fixture(tools); await f.Show();
            f.Control.MaxWidth = 300; f.Control.MaxHeight = 220; await Task.Delay(60);
            var before = FloatingChromeProbe.Bounds(f.Native);
            var resize = BeginResize(f, ChromeHit.BottomRight);
            Call(f.Control, "MoveFrameResize", resize, new Point(0, 0)); await Task.Delay(80);
            Near(before, FloatingChromeProbe.Bounds(f.Native));
            Call(f.Control, "EndFrameResize", resize, false); f.AssertEditors();
        });
        foreach (var policy in new[] { "MinWidth", "MinHeight", "MaxWidth", "MaxHeight", "FlowDirection", "ResizeBorderThickness", "IsEnabled", "Manager.IsEnabled" })
            tests.Test($"resize-policy/{kind}: policy mutation revokes the capture permanently: {policy}", async () =>
            {
                using var f = new Fixture(tools); await f.Show();
                var resize = BeginResize(f, ChromeHit.BottomRight);
                Call(f.Control, "MoveFrameResize", resize, new Point(20, 15)); await Task.Delay(80);
                switch (policy)
                {
                    case "MinWidth": f.Control.MinWidth = 200; f.Control.MinWidth = 160; break;
                    case "MinHeight": f.Control.MinHeight = 120; f.Control.MinHeight = 100; break;
                    case "MaxWidth": f.Control.MaxWidth = 900; f.Control.MaxWidth = double.PositiveInfinity; break;
                    case "MaxHeight": f.Control.MaxHeight = 900; f.Control.MaxHeight = double.PositiveInfinity; break;
                    case "FlowDirection": f.Control.FlowDirection = FlowDirection.RightToLeft; f.Control.FlowDirection = FlowDirection.LeftToRight; break;
                    case "ResizeBorderThickness": f.Control.ResizeBorderThickness = new(8); f.Control.ResizeBorderThickness = new(5); break;
                    case "IsEnabled": f.Control.IsEnabled = false; f.Control.IsEnabled = true; break;
                    case "Manager.IsEnabled": f.Manager.IsEnabled = false; f.Manager.IsEnabled = true; break;
                }
                Check.False(f.Control.IsResizing);
                var afterPolicy = FloatingChromeProbe.Bounds(f.Native);
                Call(f.Control, "MoveFrameResize", resize, new Point(200, 160));
                Call(f.Control, "EndFrameResize", resize, true); await Task.Delay(80);
                Near(afterPolicy, FloatingChromeProbe.Bounds(f.Native)); f.AssertEditors();
                var successor = BeginResize(f, ChromeHit.Bottom);
                Call(f.Control, "EndFrameResize", successor, false);
            });
        foreach (var cancel in new[] { false, true })
            tests.Test($"resize-policy/{kind}: an application native move wins over resize, cancel={cancel}", async () =>
            {
                using var f = new Fixture(tools); await f.Show();
                var resize = BeginResize(f, ChromeHit.BottomRight);
                Call(f.Control, "MoveFrameResize", resize, new Point(20, 15)); await Task.Delay(100);
                var native = f.Native.AppWindow;
                native.Move(new() { X = native.Position.X + 107, Y = native.Position.Y + 83 });
                native.Resize(new() { Width = native.Size.Width + 47, Height = native.Size.Height + 31 });
                await Task.Delay(120); var applicationBounds = FloatingChromeProbe.Bounds(f.Native);
                if (cancel) Call(f.Control, "EndFrameResize", resize, true);
                else Call(f.Control, "MoveFrameResize", resize, new Point(120, 110));
                await Task.Delay(100); Check.False(f.Control.IsResizing);
                Near(applicationBounds, FloatingChromeProbe.Bounds(f.Native)); f.AssertEditors();
            });
        tests.Test($"resize-policy/{kind}: source parent ABA cannot revive the old resize", async () =>
        {
            using var f = new Fixture(tools); await f.Show(); var resize = BeginResize(f, ChromeHit.BottomRight);
            using (f.Manager.Layout.BeginUpdate())
            {
                if (f.Source[0].Parent is LayoutAnchorablePane pane)
                { var item = (LayoutAnchorable)f.Source[0]; pane.Children.Remove(item); pane.Children.Insert(0, item); }
                else
                { var model = (LayoutDocumentFloatingWindow)f.Control.Model; var item = model.RootDocument; model.RootDocument = null; model.RootDocument = item; }
            }
            Check.False(f.Control.IsResizing);
            var before = FloatingChromeProbe.Bounds(f.Native);
            Call(f.Control, "MoveFrameResize", resize, new Point(200, 100)); await Task.Delay(80);
            Near(before, FloatingChromeProbe.Bounds(f.Native)); f.AssertEditors();
        });
        tests.Test($"resize-policy/{kind}: presenter replacement withdraws resize ownership", async () =>
        {
            using var f = new Fixture(tools); await f.Show(); var resize = BeginResize(f, ChromeHit.BottomRight);
            f.Native.AppWindow.SetPresenter(OverlappedPresenter.Create()); await Task.Delay(80);
            var before = FloatingChromeProbe.Bounds(f.Native);
            Call(f.Control, "MoveFrameResize", resize, new Point(100, 90)); await Task.Delay(100);
            Check.False(f.Control.IsResizing); Near(before, FloatingChromeProbe.Bounds(f.Native));
        });
        tests.Test($"resize-policy/{kind}: invalid begin and released sessions do not retain policy observers", async () =>
        {
            using var f = new Fixture(tools); await f.Show();
            f.Manager.IsEnabled = false;
            Check.True(Call(f.Control, "BeginFrameResize", ChromeHit.Right, new Point(0, 0), null, null) == null);
            f.Manager.IsEnabled = true;
            for (var i = 0; i < 8; i++) { var resize = BeginResize(f, ChromeHit.Right); Call(f.Control, "EndFrameResize", resize, false); }
            Check.Throws<ArgumentOutOfRangeException>(() => Call(f.Control, "BeginFrameResize", ChromeHit.Right, new Point(double.NaN, 0), null, null));
            var next = BeginResize(f, ChromeHit.Bottom); var before = FloatingChromeProbe.Bounds(f.Native);
            Call(f.Control, "MoveFrameResize", next, new Point(0, 25)); await Task.Delay(80);
            Near(before.Height + 25, FloatingChromeProbe.Bounds(f.Native).Height);
            Call(f.Control, "EndFrameResize", next, true); await Task.Delay(80); Near(before, FloatingChromeProbe.Bounds(f.Native)); f.AssertEditors();
        });
        if ((OperatingSystem.IsLinux() || OperatingSystem.IsWindows()) && Environment.GetEnvironmentVariable("UNODOCK_NATIVE_INPUT_TESTS") == "1")
        {
            foreach (var edge in Edges)
                tests.Test($"resize-policy/{kind}: physical RTL pointer resizes {edge}", async () =>
                {
                    using var f = new Fixture(tools); await f.Show(); using var input = new PointerInput();
                    f.Control.FlowDirection = FlowDirection.RightToLeft; await Task.Delay(80); f.Native.Activate();
                    var before = FloatingChromeProbe.Bounds(f.Native); var grip = f.Grip(edge);
                    var down = new Point(grip.ActualWidth / 2, grip.ActualHeight / 2);
                    var ownerPoint = input.OwnerPoint(grip, down, f.Manager);
                    input.MoveTo(grip, down); await Task.Delay(60); input.Press(); await Task.Delay(60);
                    Check.True(f.Control.IsResizing);
                    input.MoveTo(f.Manager, new(ownerPoint.X + 22, ownerPoint.Y + 16)); await Task.Delay(100);
                    input.Release(); await Wait(() => !f.Control.IsResizing);
                    var scale = f.Control.XamlRoot!.RasterizationScale;
                    Near(ChromeResize.Apply(before, edge, 22 * scale, 16 * scale, 0, 0), FloatingChromeProbe.Bounds(f.Native), 2);
                    f.AssertEditors();
                });
            tests.Test($"resize-policy/{kind}: retained input from another grip cannot end a successor capture", async () =>
            {
                using var f = new Fixture(tools); await f.Show(); using var input = new PointerInput();
                var first = f.Grip(ChromeHit.Right); var second = f.Grip(ChromeHit.Bottom);
                PointerRoutedEventArgs? firstPress = null; PointerRoutedEventArgs? secondPress = null;
                PointerEventHandler rememberFirst = (_, args) => firstPress = args;
                PointerEventHandler rememberSecond = (_, args) => secondPress = args;
                first.AddHandler(UIElement.PointerPressedEvent, rememberFirst, true);
                second.AddHandler(UIElement.PointerPressedEvent, rememberSecond, true);
                try
                {
                    input.MoveTo(first, new(first.ActualWidth / 2, first.ActualHeight / 2)); await Task.Delay(60);
                    input.Press(); await Wait(() => firstPress != null && f.Control.IsResizing);
                    input.Release(); await Wait(() => !f.Control.IsResizing);
                    input.MoveTo(second, new(second.ActualWidth / 2, second.ActualHeight / 2)); await Task.Delay(60);
                    input.Press(); await Wait(() => secondPress != null && f.Control.IsResizing);
                    Check.Equal(firstPress!.Pointer.PointerId, secondPress!.Pointer.PointerId);
                    var before = FloatingChromeProbe.Bounds(f.Native);
                    // Re-deliver actual retained args, not a fabricated routed event.
                    // The mouse ID is reused, but the grip/session owner differs.
                    foreach (var handler in new[] { "ResizeMoved", "ResizeReleased", "ResizeCancelled" })
                    {
                        Call(f.Control, handler, first, firstPress);
                        Check.True(f.Control.IsResizing, "Stale grip input ended the new capture: " + handler);
                    }
                    Near(before, FloatingChromeProbe.Bounds(f.Native));
                    input.EscapeDown(); await Wait(() => !f.Control.IsResizing);
                    input.EscapeUp(); input.Release(); await Task.Delay(80); f.AssertEditors();
                }
                finally
                {
                    first.RemoveHandler(UIElement.PointerPressedEvent, rememberFirst);
                    second.RemoveHandler(UIElement.PointerPressedEvent, rememberSecond);
                }
            });
            tests.Test($"resize-policy/{kind}: policy revocation releases a physical capture and clock", async () =>
            {
                using var f = new Fixture(tools); await f.Show(); using var input = new PointerInput();
                var grip = f.Grip(ChromeHit.BottomRight); var down = new Point(grip.ActualWidth / 2, grip.ActualHeight / 2);
                var ownerPoint = input.OwnerPoint(grip, down, f.Manager);
                input.MoveTo(grip, down); await Task.Delay(60); input.Press(); await Task.Delay(60); Check.True(f.Control.IsResizing);
                f.Control.MinWidth = 200; Check.False(f.Control.IsResizing);
                var before = FloatingChromeProbe.Bounds(f.Native);
                input.MoveTo(f.Manager, new(ownerPoint.X + 100, ownerPoint.Y + 80)); await Task.Delay(100);
                input.Release(); await Task.Delay(100); Near(before, FloatingChromeProbe.Bounds(f.Native)); f.AssertEditors();
            });
        }
        return await tests.Run(output, tools ? "floating-resize-policy-tools" : "floating-resize-policy-documents");
    }
    private static object BeginResize(Fixture fixture, ChromeHit edge) =>
        Call(fixture.Control, "BeginFrameResize", edge, new Point(0, 0), null, null)
        ?? throw new InvalidOperationException($"The valid native resize was rejected: {edge}; custom={fixture.Control.IsCustomTitleBar}; native={fixture.Control.NativeWindow != null}; resizing={fixture.Control.IsResizing}; enabled={fixture.Control.IsEnabled}; max={fixture.Control.IsMaximized}; presenter={((OverlappedPresenter)fixture.Native.AppWindow.Presenter).State}; canresize={((OverlappedPresenter)fixture.Native.AppWindow.Presenter).IsResizable}; root={fixture.Control.Model.Root != null}");
}
