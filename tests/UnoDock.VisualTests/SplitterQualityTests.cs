using System.Reflection;
using System.Globalization;
using System.Xml.Linq;
using System.Runtime.ExceptionServices;
using Microsoft.UI.Xaml.Controls.Primitives;
using Windows.Foundation;
using Windows.System;
using UnoDock.Controls;
using UnoDock.Themes;

namespace UnoDock.Testing;
/// <summary>Realized-grid regressions. Internal protocol tests are distinct from opt-in OS pointer tests.</summary>
public static class SplitterQualityTests
{
    public static async Task<int> Run(DockingManager templateSource, string output)
    {
        var tests = new TestRunner();
        using var dock = new DockingManager
        {
            Width = 1000,
            Height = 640,
            Template = templateSource.Template,
            RequestedTheme = ElementTheme.Light,
            FloatingWindowMode = FloatingWindowMode.InSurface
        };
        var scene = new Grid
        {
            Width = 1000,
            Height = 640
        };
        scene.Children.Add(dock);
        var window = new Window
        {
            Content = scene,
            Title = "UnoDock splitter transaction tests"
        };
        window.AppWindow.Resize(new() { Width = 1040, Height = 720 });
        window.Activate();
        using var registration = Microsoft.Windows.Shell.SystemCommands.RegisterWindow(window);
        LayoutAnchorablePane first = null!;
        LayoutDocumentPane second = null!;
        LayoutPanel panel = null!;
        LayoutGridResizerControl splitter = null!;
        LayoutPanelControl grid = null!;
        bool horizontal = true;
        Point pointerOrigin = default;
        try
        {
            foreach (var axis in new[]
            {
                true,
                false
            }

            )
                foreach (var rtl in new[]
                {
                    false,
                    true
                }

                )
                {
                    tests.Test($"deferred preview preserves model, pixel geometry and star weights H={axis}, RTL={rtl}", async () =>
                    {
                        await Reset(axis, rtl);
                        var pixels = Pixels();
                        var lengths = Lengths();
                        Begin();
                        Move(40);
                        Move(15);
                        await Settle();
                        Check.Equal(lengths, Lengths());
                        Check.Near(pixels, Pixels(), .1);
                        Check.True(splitter.IsDragging);
                        var ghost = Ghost();
                        Check.True(ghost.ActualWidth > 0 && ghost.ActualHeight > 0);
                        var origin = splitter.TransformToVisual(grid).TransformPoint(default);
                        Check.Near((axis ? origin.X : origin.Y) + 15, axis ? Canvas.GetLeft(ghost) : Canvas.GetTop(ghost), .1);
                        End(false);
                        await Settle();
                        Check.False(splitter.IsDragging);
                        Check.True(Ghosts().Length == 0);
                        Check.True(Lengths().Item1.IsStar && Lengths().Item2.IsStar);
                        Check.Near(3, Lengths().Item1.Value + Lengths().Item2.Value);
                        Check.Near(pixels + 15, Pixels(), .15);
                    });
                    tests.Test($"cancel restores exact original values without model notifications H={axis}, RTL={rtl}", async () =>
                    {
                        await Reset(axis, rtl);
                        var lengths = Lengths();
                        var writes = 0;
                        first.PropertyChanged += (_, e) =>
                        {
                            if (e.PropertyName is "DockWidth" or "DockHeight")
                                writes++;
                        };
                        Begin();
                        Move(70);
                        splitter.CancelDrag();
                        splitter.CancelDrag();
                        End(false);
                        await Settle();
                        Check.Equal(lengths, Lengths());
                        Check.Equal(0, writes);
                        Check.Equal(0, Ghosts().Length);
                        Check.False(splitter.IsDragging);
                    });
                    tests.Test($"keyboard axis and physical direction H={axis}, RTL={rtl}", async () =>
                    {
                        await Reset(axis, rtl);
                        var start = Pixels();
                        Check.False(Key(axis ? VirtualKey.Down : VirtualKey.Right));
                        Check.Near(start, Pixels());
                        Check.True(Key(axis ? VirtualKey.Right : VirtualKey.Down));
                        await Settle();
                        Check.Near(start + (axis && rtl ? -10 : 10), Pixels(), .15);
                    });
                }

            using (var observations = typeof(SplitterQualityTests).Assembly.GetManifestResourceStream("VisualFixtures.splitter-observations.xml")!)
            {
                var fixture = XDocument.Load(observations);
                foreach (var observation in fixture.Root!.Elements("Scenario").Where(o => (bool)o.Attribute("cancel")! == false))
                {
                    tests.Test("public protocol fixture: " + observation.Attribute("units")!.Value + " / " + observation.Attribute("orientation")!.Value + " / RTL=" + observation.Attribute("rtl")!.Value, async () =>
                    {
                        await Reset(observation.Attribute("orientation")!.Value == "Horizontal", (bool)observation.Attribute("rtl")!);
                        var initial = observation.Elements("State").First();
                        var final = observation.Elements("State").Last();
                        var a = ReadLength(initial, "before");
                        var b = ReadLength(initial, "after");
                        if (horizontal)
                        {
                            first.DockWidth = a;
                            second.DockWidth = b;
                        }
                        else
                        {
                            first.DockHeight = a;
                            second.DockHeight = b;
                        }

                        await Settle();
                        Check.Near((double)observation.Attribute("dragOpacity")!, splitter.OpacityWhileDragging);
                        Check.Equal(observation.Attribute("dragBrush")!.Value, ((SolidColorBrush)splitter.BackgroundWhileDragging!).Color.ToString());
                        Begin();
                        Move(40);
                        Move(15);
                        Check.Equal((a, b), Lengths());
                        End(false);
                        await Settle();
                        var actual = Lengths();
                        var expectedA = ReadLength(final, "before");
                        var expectedB = ReadLength(final, "after");
                        Check.Equal(expectedA.GridUnitType, actual.Item1.GridUnitType);
                        Check.Equal(expectedB.GridUnitType, actual.Item2.GridUnitType);
                        Check.Near(expectedA.Value, actual.Item1.Value, .00001);
                        Check.Near(expectedB.Value, actual.Item2.Value, .00001);
                    });
                }
            }

            tests.Test("no-op preserves Auto and pixel units exactly", async () =>
            {
                await Reset();
                first.DockWidth = GridLength.Auto;
                second.DockWidth = new(500);
                await Settle();
                var lengths = Lengths();
                Begin();
                Move(10);
                Move(0);
                End(false);
                await Settle();
                Check.Equal(lengths, Lengths());
            });
            tests.Test("delta overshoot then reversal does not accumulate clamp error", async () =>
            {
                await Reset();
                var start = Pixels();
                Begin();
                Move(100000);
                Move(20);
                End(false);
                await Settle();
                Check.Near(start + 20, Pixels(), .15);
            });
            tests.Test("minimum sizes bound both ends of the preview", async () =>
            {
                await Reset();
                var start = Pixels();
                var total = Total();
                Begin();
                Move(-10000);
                Check.Near(100 - start, Canvas.GetLeft(Ghost()) - splitter.TransformToVisual(grid).TransformPoint(default).X, .1);
                Move(10000);
                End(false);
                await Settle();
                Check.Near(total - 100, Pixels(), .15);
            });
            tests.Test("NaN and infinity cancel without persistent changes", async () =>
            {
                foreach (var value in new[]
                {
                    double.NaN,
                    double.PositiveInfinity,
                    double.NegativeInfinity
                }

                )
                {
                    await Reset();
                    var before = Lengths();
                    Begin();
                    Check.Throws<ArgumentOutOfRangeException>(() => Move(value));
                    Check.Equal(before, Lengths());
                    Check.False(splitter.IsDragging);
                    Check.Equal(0, Ghosts().Length);
                }
            });
            tests.Test("second Begin does not replace an active origin", async () =>
            {
                await Reset();
                var start = Pixels();
                Begin();
                Move(20);
                Begin();
                Move(35);
                End(false);
                await Settle();
                Check.Near(start + 35, Pixels(), .15);
            });
            tests.Test("completion can run only once", async () =>
            {
                await Reset();
                var start = Pixels();
                Begin();
                Move(25);
                End(false);
                End(false);
                await Settle();
                Check.Near(start + 25, Pixels(), .15);
            });
            tests.Test("external length edit cancels and preserves application ownership", async () =>
            {
                await Reset();
                Begin();
                Move(25);
                first.DockWidth = new(270);
                await Settle();
                Check.False(splitter.IsDragging);
                Check.Equal(new GridLength(270), first.DockWidth);
                Check.Equal(new GridLength(2, GridUnitType.Star), second.DockWidth);
                Check.Equal(0, Ghosts().Length);
            });
            tests.Test("minimum edit invalidates the pending transaction", async () =>
            {
                await Reset();
                var old = Lengths();
                Begin();
                Move(25);
                second.DockMinWidth = 300;
                await Settle();
                Check.False(splitter.IsDragging);
                Check.Equal(old, Lengths());
            });
            tests.Test("root replacement cleans the old preview and forbids commit", async () =>
            {
                await Reset();
                var old = Lengths();
                var stale = splitter;
                Begin();
                Move(25);
                dock.Layout = new();
                await Settle();
                Call(stale, "EndResize", false);
                Check.Equal(old, Lengths());
                Check.False(stale.IsDragging);
                Check.Equal(0, Ghosts().Length);
            });
            tests.Test("orientation switch cancels without writing either axis", async () =>
            {
                await Reset();
                var old = Lengths();
                var stale = splitter;
                Begin();
                Move(25);
                panel.Orientation = Orientation.Vertical;
                await Settle();
                Check.False(stale.IsDragging);
                Check.Equal(old, Lengths());
                Check.Equal(0, Ghosts().Length);
            });
            tests.Test("removing an endpoint cancels without mutating detached panes", async () =>
            {
                await Reset();
                var old = Lengths();
                var stale = splitter;
                Begin();
                Move(25);
                panel.Children.Remove(second);
                await Settle();
                Check.False(stale.IsDragging);
                Check.Equal(old, Lengths());
            });
            tests.Test("disabled splitter cancels and no longer consumes resize keys", async () =>
            {
                await Reset();
                var old = Lengths();
                Begin();
                Move(25);
                splitter.IsEnabled = false;
                await Settle();
                Check.False(splitter.IsDragging);
                Check.False(Key(VirtualKey.Right));
                Check.Equal(old, Lengths());
            });
            tests.Test("flow-direction change cancels the old coordinate space", async () =>
            {
                await Reset();
                var old = Lengths();
                Begin();
                Move(25);
                scene.FlowDirection = FlowDirection.RightToLeft;
                await Settle();
                Check.False(splitter.IsDragging);
                Check.Equal(old, Lengths());
            });
            tests.Test("viewport size change cancels rather than applying stale pixel ratios", async () =>
            {
                await Reset();
                var old = Lengths();
                Begin();
                Move(25);
                dock.Width = 900;
                await Settle();
                Check.False(splitter.IsDragging);
                Check.Equal(old, Lengths());
            });
            tests.Test("Escape cancels an internal session without resizing", async () =>
            {
                await Reset();
                var old = Lengths();
                Begin();
                Move(25);
                Check.True(Key(VirtualKey.Escape));
                Check.Equal(old, Lengths());
                Check.False(splitter.IsDragging);
            });
            tests.Test("unloading cancels and reloading permits a fresh session", async () =>
            {
                await Reset();
                var old = Lengths();
                Begin();
                Move(25);
                scene.Children.Remove(dock);
                await Task.Delay(50);
                Check.False(splitter.IsDragging);
                Check.Equal(old, Lengths());
                scene.Children.Add(dock);
                await Settle();
                Begin();
                Move(10);
                End(false);
                Check.False(splitter.IsDragging);
            });
            tests.Test("model callback replacing root stops second-endpoint publication", async () =>
            {
                await Reset();
                var old = Lengths();
                var invoked = false;
                first.PropertyChanged += (_, e) =>
                {
                    if (e.PropertyName == "DockWidth" && !invoked)
                    {
                        invoked = true;
                        dock.Layout = new();
                    }
                };
                Begin();
                Move(25);
                End(false);
                Check.True(invoked);
                Check.Equal(old, Lengths());
            });
            tests.Test("competing application edit of first endpoint is not overwritten", async () =>
            {
                await Reset();
                var invoked = false;
                var oldB = second.DockWidth;
                first.PropertyChanged += (_, e) =>
                {
                    if (e.PropertyName == "DockWidth" && !invoked)
                    {
                        invoked = true;
                        first.DockWidth = new(222);
                    }
                };
                Begin();
                Move(25);
                End(false);
                Check.Equal(new GridLength(222), first.DockWidth);
                Check.Equal(oldB, second.DockWidth);
            });
            tests.Test("competing second endpoint edit survives first-endpoint rollback", async () =>
            {
                await Reset();
                var invoked = false;
                var oldA = first.DockWidth;
                first.PropertyChanged += (_, e) =>
                {
                    if (e.PropertyName == "DockWidth" && !invoked)
                    {
                        invoked = true;
                        second.DockWidth = new(444);
                    }
                };
                Begin();
                Move(25);
                End(false);
                Check.Equal(oldA, first.DockWidth);
                Check.Equal(new GridLength(444), second.DockWidth);
            });
            tests.Test("throwing second-endpoint observer rolls back owned values", async () =>
            {
                await Reset();
                var old = Lengths();
                var invoked = false;
                second.PropertyChanged += (_, e) =>
                {
                    if (e.PropertyName == "DockWidth" && !invoked)
                    {
                        invoked = true;
                        throw new InvalidOperationException("observer");
                    }
                };
                Begin();
                Move(25);
                Check.Throws<InvalidOperationException>(() => End(false));
                Check.Equal(old, Lengths());
                Check.False(splitter.IsDragging);
                Check.Equal(0, Ghosts().Length);
                Begin();
                Move(12);
                End(false);
                Check.True(first.DockWidth != old.Item1);
            });
            tests.Test("custom preview brush and opacity paint the moving ghost", async () =>
            {
                await Reset();
                var brush = new SolidColorBrush(Microsoft.UI.Colors.Magenta);
                splitter.BackgroundWhileDragging = brush;
                splitter.OpacityWhileDragging = .35;
                Begin();
                Move(30);
                await Settle();
                Check.Same(brush, Ghost().Background);
                Check.Near(.35, Ghost().Opacity);
                splitter.CancelDrag();
            });
            foreach (var variant in new[]
            {
                "horizontal",
                "vertical",
                "rtl",
                "dark"
            }

            )
                tests.Test("capture deferred splitter preview: " + variant, async () =>
                {
                    await Reset(variant != "vertical", variant == "rtl");
                    if (variant == "dark")
                    {
                        dock.Theme = new FluentTheme(ElementTheme.Dark);
                        await Settle();
                    }

                    Begin();
                    Move(65);
                    await Settle();
                    Check.True(Ghost().ActualWidth > 0);
                    await VisualCapture.Save(scene, Path.Combine(output, "visuals", "splitter-" + variant + ".png"));
                    splitter.CancelDrag();
                });
            if (OperatingSystem.IsLinux() && Environment.GetEnvironmentVariable("UNODOCK_NATIVE_INPUT_TESTS") == "1")
            {
                foreach (var rtl in new[]
                {
                    false,
                    true
                }

                )
                    tests.Test("XTEST splitter physical drag commits once, RTL=" + rtl, async () =>
                    {
                        await Reset(true, rtl);
                        var start = Pixels();
                        var lengths = Lengths();
                        var begins = 0;
                        var ends = 0;
                        splitter.DragStarted += (_, _) => begins++;
                        splitter.DragCompleted += (_, _) => ends++;
                        using var input = new X11TestInput();
                        await PointerBegin(input);
                        await PointerMove(input, 55);
                        Check.True(splitter.IsDragging);
                        Check.Equal(lengths, Lengths());
                        Check.Equal(1, Ghosts().Length);
                        input.Release();
                        await Settle();
                        Check.Near(start + (rtl ? -55 : 55), Pixels(), 1.1);
                        Check.Equal(1, begins);
                        Check.Equal(1, ends);
                        Check.False(splitter.IsDragging);
                    });
                tests.Test("XTEST Escape cancels captured resize and releases the native Thumb", async () =>
                {
                    await Reset();
                    var old = Lengths();
                    var canceled = false;
                    splitter.DragCompleted += (_, e) => canceled |= e.Canceled;
                    using var input = new X11TestInput();
                    await PointerBegin(input);
                    await PointerMove(input, 45);
                    Check.True(splitter.IsDragging);
                    input.Escape();
                    await Task.Delay(60);
                    input.Release();
                    await Settle();
                    Check.Equal(old, Lengths());
                    Check.True(canceled);
                    Check.False(splitter.IsDragging);
                    Check.Equal(0, Ghosts().Length);
                });
                tests.Test("XTEST disable during capture cancels even with later pointer release", async () =>
                {
                    await Reset();
                    var old = Lengths();
                    using var input = new X11TestInput();
                    await PointerBegin(input);
                    await PointerMove(input, 45);
                    splitter.IsEnabled = false;
                    input.Release();
                    await Settle();
                    Check.Equal(old, Lengths());
                    Check.False(splitter.IsDragging);
                });
                tests.Test("XTEST unloading during capture never commits stale dimensions", async () =>
                {
                    await Reset();
                    var old = Lengths();
                    using var input = new X11TestInput();
                    await PointerBegin(input);
                    await PointerMove(input, 45);
                    scene.Children.Remove(dock);
                    input.Release();
                    await Task.Delay(60);
                    Check.Equal(old, Lengths());
                    Check.False(splitter.IsDragging);
                    scene.Children.Add(dock);
                    await Settle();
                });
                tests.Test("XTEST native capture loss abandons the preview", async () =>
                {
                    await Reset();
                    var old = Lengths();
                    using var input = new X11TestInput();
                    await PointerBegin(input);
                    await PointerMove(input, 45);
                    splitter.FindVisualChildren<Thumb>().Single().ReleasePointerCaptures();
                    await Task.Delay(40);
                    input.Release();
                    await Settle();
                    Check.Equal(old, Lengths());
                    Check.False(splitter.IsDragging);
                    Check.Equal(0, Ghosts().Length);
                });
                tests.Test("XTEST vertical resize uses Y displacement and defers geometry", async () =>
                {
                    await Reset(false);
                    var start = Pixels();
                    using var input = new X11TestInput();
                    await PointerBegin(input);
                    await PointerMove(input, 38);
                    Check.True(splitter.IsDragging);
                    Check.Near(start, Pixels(), .1);
                    input.Release();
                    await Settle();
                    Check.Near(start + 38, Pixels(), 1.1);
                });
            }

            return await tests.Run(output, "splitter-quality");
        }
        finally
        {
            splitter?.CancelDrag();
            window.Content = null;
            window.Close();
        }

        (GridLength, GridLength) Lengths() => horizontal ? (first.DockWidth, second.DockWidth) : (first.DockHeight, second.DockHeight);
        double Pixels() => horizontal ? grid.ColumnDefinitions[0].ActualWidth : grid.RowDefinitions[0].ActualHeight;
        double Total() => Pixels() + (horizontal ? grid.ColumnDefinitions[2].ActualWidth : grid.RowDefinitions[2].ActualHeight);
        Border[] Ghosts() => dock.FindVisualChildren<Border>().Where(b => b.Name == "PART_SplitterPreview").ToArray();
        Border Ghost() => Ghosts().Single();
        void Begin() => Call(splitter, "BeginResize");
        void Move(double delta) => Call(splitter, "UpdateResize", delta);
        void End(bool canceled) => Call(splitter, "EndResize", canceled);
        bool Key(VirtualKey key) => (bool)Call(splitter, "ResizeFromKey", key)!;
        async Task Settle()
        {
            dock.Refresh();
            scene.UpdateLayout();
            await Task.Delay(45);
            scene.UpdateLayout();
        }

        async Task Reset(bool axis = true, bool rtl = false)
        {
            splitter?.CancelDrag();
            if (!scene.Children.Contains(dock))
                scene.Children.Add(dock);
            horizontal = axis;
            dock.Width = 1000;
            dock.Theme = null;
            scene.FlowDirection = rtl ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
            first = new(new LayoutAnchorable { ContentId = "tools", Title = "Tools", Content = new TextBox { Text = "The editor stays in place while the splitter preview moves.", AcceptsReturn = true, TextWrapping = TextWrapping.Wrap } })
            {
                DockWidth = new(1, GridUnitType.Star),
                DockHeight = new(1, GridUnitType.Star),
                DockMinWidth = 100,
                DockMinHeight = 100
            };
            second = new(new LayoutDocument { ContentId = "editor", Title = "Workspace.cs", Content = new TextBox { Text = "Release to commit. Escape to cancel. Arrow keys resize along the divider axis.", AcceptsReturn = true, TextWrapping = TextWrapping.Wrap } })
            {
                DockWidth = new(2, GridUnitType.Star),
                DockHeight = new(2, GridUnitType.Star),
                DockMinWidth = 100,
                DockMinHeight = 100
            };
            panel = new LayoutPanel(first)
            {
                Orientation = axis ? Orientation.Horizontal : Orientation.Vertical
            };
            panel.Children.Add(second);
            dock.Layout = new()
            {
                RootPanel = panel
            };
            window.Activate();
            await Settle();
            splitter = dock.FindVisualChildren<LayoutGridResizerControl>().Single();
            grid = dock.FindVisualChildren<LayoutPanelControl>().Single(v => ReferenceEquals(v.Model, panel));
            Check.True(splitter.IsLoaded && splitter.ActualWidth > 0 && splitter.ActualHeight > 0);
        }

        async Task PointerBegin(X11TestInput input)
        {
            window.Activate();
            await Task.Delay(80);
            pointerOrigin = splitter.TransformToVisual(scene).TransformPoint(new(splitter.ActualWidth / 2, splitter.ActualHeight / 2));
            input.MoveTo(scene, pointerOrigin);
            await Task.Delay(40);
            input.Press();
            await Task.Delay(60);
        }

        async Task PointerMove(X11TestInput input, double physical)
        {
            var logical = horizontal && scene.FlowDirection == FlowDirection.RightToLeft ? -physical : physical;
            input.MoveTo(scene, horizontal ? new(pointerOrigin.X + logical, pointerOrigin.Y) : new(pointerOrigin.X, pointerOrigin.Y + logical));
            await Task.Delay(100);
        }
    }

    private static GridLength ReadLength(XElement node, string name) => new(double.Parse(node.Attribute(name)!.Value, CultureInfo.InvariantCulture), Enum.Parse<GridUnitType>(node.Attribute(name + "Unit")!.Value));
    private static object? Call(object target, string method, params object[] args)
    {
        try
        {
            return target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(target, args);
        }
        catch (TargetInvocationException e)when (e.InnerException != null)
        {
            ExceptionDispatchInfo.Capture(e.InnerException).Throw();
            throw;
        }
    }
}
