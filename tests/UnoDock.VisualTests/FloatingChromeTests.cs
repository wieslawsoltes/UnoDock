using System.Reflection;
using System.Runtime.ExceptionServices;
using Microsoft.UI.Windowing;
using UnoDock.Controls;
using UnoDock.Core;
using UnoDock.Themes;
using Windows.Foundation;

namespace UnoDock.Testing;
/// <summary>Real-host chrome acceptance. Independent native frame probes,
/// deterministic resize boundaries and physical input are reported separately.</summary>
internal static partial class FloatingChromeTests
{
    private static readonly ChromeHit[] Edges = [ChromeHit.Left, ChromeHit.Top, ChromeHit.Right, ChromeHit.Bottom, ChromeHit.TopLeft, ChromeHit.TopRight, ChromeHit.BottomLeft, ChromeHit.BottomRight];
    internal static async Task<int> Run(string output, bool toolsOnly)
    {
        // Each window kind is a registered suite and executes once in its own
        // native process. This bounds cumulative host resources without retries
        // or a larger X-server client limit, and retains every acceptance case.
        var tests = new TestRunner();
        if (!toolsOnly)
            tests.Test("chrome: invalid direct mode value restores the previous setting", () =>
            {
                using var manager = new DockingManager();
                Check.Equal(FloatingWindowTitleBarMode.Custom, manager.FloatingWindowTitleBarMode);
                var failed = false;
                try
                {
                    manager.SetValue(DockingManager.FloatingWindowTitleBarModeProperty, (FloatingWindowTitleBarMode)999);
                }
                catch (ArgumentOutOfRangeException)
                {
                    failed = true;
                }

                Check.True(failed);
                Check.Equal(FloatingWindowTitleBarMode.Custom, manager.FloatingWindowTitleBarMode);
                return Task.CompletedTask;
            });
        foreach (var tools in new[]
        {
            toolsOnly
        }

        )
        {
            var kind = tools ? "tools" : "document";
            RegisterInitialLayout(tests, tools);
            tests.Test($"chrome/{kind}: native frame is replaced and caption controls remain real", async () =>
            {
                using var f = new Fixture(tools);
                await f.Show();
                Check.True(f.Control.IsCustomTitleBar);
                FloatingChromeProbe.AssertCustom(f.Native, f.Owner);
                foreach (var id in new[]
                {
                    "Dock",
                    "Minimize",
                    "Maximize",
                    "Close"
                }

                )
                {
                    var button = f.Button(id);
                    Check.True(button.IsLoaded && button.Visibility == Visibility.Visible && button.ActualWidth > 0);
                }

                foreach (var edge in Edges)
                {
                    var grip = f.Grip(edge);
                    Check.True(grip.IsLoaded && grip.ActualWidth > 0 && grip.ActualHeight > 0);
                }

                f.AssertEditors();
            });
            tests.Test($"chrome/{kind}: system/custom switching retains window, caption and editors", async () =>
            {
                using var f = new Fixture(tools);
                await f.Show();
                var native = f.Native;
                var caption = f.Caption;
                for (var i = 0; i < 2; i++)
                {
                    f.Manager.FloatingWindowTitleBarMode = FloatingWindowTitleBarMode.System;
                    await Task.Delay(120);
                    Check.False(f.Control.IsCustomTitleBar);
                    FloatingChromeProbe.AssertSystem(native);
                    f.Manager.FloatingWindowTitleBarMode = FloatingWindowTitleBarMode.Custom;
                    await Task.Delay(120);
                    Check.True(f.Control.IsCustomTitleBar);
                    FloatingChromeProbe.AssertCustom(native, f.Owner);
                    Check.Same(native, f.Native);
                    Check.Same(caption, f.Caption);
                    f.AssertEditors();
                }
            });
            foreach (var edge in Edges)
                foreach (var cancel in new[]
                {
                    false,
                    true
                }

                )
                    tests.Test($"chrome/{kind}: actual native resize {edge}, cancel={cancel}", async () =>
                    {
                        using var f = new Fixture(tools);
                        await f.Show();
                        var before = FloatingChromeProbe.Bounds(f.Native);
                        var resize = Call(f.Control, "BeginFrameResize", edge, new Point(0, 0), null, null)!;
                        Check.True(resize != null && f.Control.IsResizing);
                        Call(f.Control, "MoveFrameResize", resize!, new Point(24, 18));
                        await Task.Delay(80);
                        var expected = ChromeResize.Apply(before, edge, 24, 18, 0, 0);
                        Near(expected, FloatingChromeProbe.Bounds(f.Native));
                        Call(f.Control, "EndFrameResize", resize!, cancel);
                        await Task.Delay(80);
                        Check.False(f.Control.IsResizing);
                        Check.False(f.Control.IsDragging);
                        Near(cancel ? before : expected, FloatingChromeProbe.Bounds(f.Native));
                        var final = FloatingChromeProbe.Bounds(f.Native);
                        Call(f.Control, "MoveFrameResize", resize!, new Point(200, 200));
                        Call(f.Control, "EndFrameResize", resize!, false);
                        Near(final, FloatingChromeProbe.Bounds(f.Native));
                        f.AssertEditors();
                    });
            tests.Test($"chrome/{kind}: minimum clamp holds the opposite edge and Escape restores", async () =>
            {
                using var f = new Fixture(tools);
                await f.Show();
                f.Control.MinWidth = 230;
                f.Control.MinHeight = 180;
                var before = FloatingChromeProbe.Bounds(f.Native);
                var resize = Call(f.Control, "BeginFrameResize", ChromeHit.TopLeft, new Point(0, 0), null, null)!;
                Call(f.Control, "MoveFrameResize", resize, new Point(10000, 10000));
                await Task.Delay(80);
                var after = FloatingChromeProbe.Bounds(f.Native);
                var scale = OperatingSystem.IsMacOS() ? 1 : f.Control.XamlRoot!.RasterizationScale;
                Near(230 * scale, after.Width);
                Near(180 * scale, after.Height);
                Near(before.Right, after.Right);
                Near(before.Bottom, after.Bottom);
                Call(f.Control, "CancelFrameResize", true);
                await Task.Delay(80);
                Near(before, FloatingChromeProbe.Bounds(f.Native));
                Check.False(f.Control.IsResizing);
            });
            tests.Test($"chrome/{kind}: non-resizable presenter cannot begin custom resize", async () =>
            {
                using var f = new Fixture(tools);
                await f.Show();
                var presenter = (OverlappedPresenter)f.Native.AppWindow.Presenter;
                presenter.IsResizable = false;
                f.Manager.Refresh();
                await Task.Delay(50);
                Check.True(Call(f.Control, "BeginFrameResize", ChromeHit.Right, new Point(0, 0), null, null) == null);
                Check.False(f.Control.IsResizing);
                presenter.IsResizable = true;
                f.Manager.Refresh();
                var resize = Call(f.Control, "BeginFrameResize", ChromeHit.Right, new Point(0, 0), null, null)!;
                Check.True(resize != null);
                Call(f.Control, "EndFrameResize", resize!, true);
            });
            tests.Test($"chrome/{kind}: mode switch cancels a pending resize before restoring decorations", async () =>
            {
                using var f = new Fixture(tools);
                await f.Show();
                var native = f.Native;
                var resize = Call(f.Control, "BeginFrameResize", ChromeHit.BottomRight, new Point(0, 0), null, null)!;
                Call(f.Control, "MoveFrameResize", resize, new Point(60, 40));
                f.Manager.FloatingWindowTitleBarMode = FloatingWindowTitleBarMode.System;
                await Task.Delay(100);
                Check.False(f.Control.IsResizing);
                Check.False(f.Control.IsCustomTitleBar);
                var beforeLate = FloatingChromeProbe.Bounds(native);
                Call(f.Control, "MoveFrameResize", resize, new Point(300, 300));
                Near(beforeLate, FloatingChromeProbe.Bounds(native));
                f.AssertEditors();
            });
            tests.Test($"chrome/{kind}: light/dark switching retains focused editor and chrome controls", async () =>
            {
                using var f = new Fixture(tools);
                await f.Show();
                var caption = f.Caption;
                var native = f.Native;
                var grips = Edges.Select(f.Grip).ToArray();
                var editor = (TextBox)f.Source[0].Content!;
                native.Activate();
                Check.True(editor.Focus(FocusState.Keyboard));
                await Task.Delay(60);
                Check.True(editor.FocusState != FocusState.Unfocused);
                f.Manager.Theme = new FluentTheme(ElementTheme.Dark);
                f.Manager.Refresh();
                await Task.Delay(60);
                FloatingChromeProbe.AssertCustom(native, f.Owner);
                Check.Same(native, f.Native);
                Check.Same(caption, f.Caption);
                for (var i = 0; i < grips.Length; i++)
                    Check.Same(grips[i], f.Grip(Edges[i]));
                f.Manager.Theme = new FluentTheme(ElementTheme.Light);
                f.Manager.Refresh();
                f.AssertEditors();
            });
            tests.Test($"chrome/{kind}: late resize events cannot cancel an independently started successor", async () =>
            {
                using var f = new Fixture(tools);
                await f.Show();
                var first = Call(f.Control, "BeginFrameResize", ChromeHit.Right, new Point(0, 0), null, null)!;
                Call(f.Control, "EndFrameResize", first, false);
                var second = Call(f.Control, "BeginFrameResize", ChromeHit.Bottom, new Point(0, 0), null, null)!;
                Check.True(second != null && !ReferenceEquals(first, second));
                var before = FloatingChromeProbe.Bounds(f.Native);
                Call(f.Control, "MoveFrameResize", first, new Point(200, 200));
                Call(f.Control, "EndFrameResize", first, true);
                Check.True(f.Control.IsResizing);
                Near(before, FloatingChromeProbe.Bounds(f.Native));
                Call(f.Control, "MoveFrameResize", second!, new Point(0, 20));
                await Task.Delay(80);
                Near(before with
                {
                    Height = before.Height + 20
                }, FloatingChromeProbe.Bounds(f.Native));
                Call(f.Control, "EndFrameResize", second!, true);
                await Task.Delay(80);
                Check.False(f.Control.IsResizing);
                Near(before, FloatingChromeProbe.Bounds(f.Native));
                f.AssertEditors();
            });
            if ((OperatingSystem.IsLinux() || OperatingSystem.IsWindows()) && Environment.GetEnvironmentVariable("UNODOCK_NATIVE_INPUT_TESTS") == "1")
                RegisterPhysical(tests, tools);
        }

        return await tests.Run(output, toolsOnly ? "floating-chrome-tools" : "floating-chrome-documents");
    }

    private static object? Call(object target, string name, params object?[] args)
    {
        var method = typeof(LayoutFloatingWindowControl).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic) ?? throw new MissingMethodException(name);
        try
        {
            return method.Invoke(target, args);
        }
        catch (TargetInvocationException e) when (e.InnerException != null)
        {
            ExceptionDispatchInfo.Capture(e.InnerException).Throw();
            throw;
        }
    }

    private static void Near(double expected, double actual, double tolerance = 1) => Check.True(Math.Abs(expected - actual) <= tolerance, $"Expected {expected}, got {actual}, tolerance {tolerance}.");
    private static void Near(DockRect expected, DockRect actual, double tolerance = 1)
    {
        Near(expected.X, actual.X, tolerance);
        Near(expected.Y, actual.Y, tolerance);
        Near(expected.Width, actual.Width, tolerance);
        Near(expected.Height, actual.Height, tolerance);
    }

    private static async Task Wait(Func<bool> ready)
    {
        for (var i = 0; i < 160 && !ready(); i++)
            await Task.Delay(25);
        Check.True(ready(), "Native chrome transition did not settle.");
    }
}
