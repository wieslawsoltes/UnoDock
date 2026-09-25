using System.Reflection;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls.Primitives;
using UnoDock.Controls;
using UnoDock.Gallery;
using Windows.Foundation;

namespace UnoDock.Testing;

internal static class PresentationQualityTests
{
    internal static async Task<int> Run(string output)
    {
        var tests = new TestRunner();
        var enclose = typeof(DockingManager).Assembly.GetType("UnoDock.Internal.DockCoordinates", true)!.GetMethod("Enclose", BindingFlags.Static | BindingFlags.NonPublic)!.CreateDelegate<Func<Point, Point, Point, Point, Rect>>();
        tests.Test("bounds: integral rectangles stay exact without arbitrary inflation", () =>
        {
            var rectangle = enclose(new(-20, 10), new(480, 10), new(-20, 210), new(480, 210));
            Check.Equal(-20d, rectangle.X);
            Check.Equal(10d, rectangle.Y);
            Check.Equal(500d, rectangle.Width);
            Check.Equal(200d, rectangle.Height);
        });
        tests.Test("bounds: collapsed finite extents remain zero-sized", () =>
        {
            var point = new Point(-.1, 300.7);
            var rectangle = enclose(point, point, point, point);
            Check.Equal(point.X, rectangle.X);
            Check.Equal(point.Y, rectangle.Y);
            Check.Equal(0d, rectangle.Width);
            Check.Equal(0d, rectangle.Height);
        });
        tests.Test("bounds: 50000 deterministic rotated and reflected point sets remain enclosed", () =>
        {
            var random = new Random(147021);
            for (var i = 0; i < 50000; i++)
            {
                var angle = random.NextDouble() * Math.PI * 2;
                var cos = Math.Cos(angle);
                var sin = Math.Sin(angle);
                var x = random.NextDouble() * 200000 - 100000;
                var y = random.NextDouble() * 200000 - 100000;
                var width = random.NextDouble() * 9000 * (i % 2 == 0 ? 1 : -1);
                var height = random.NextDouble() * 7000;
                var a = new Point(x, y);
                var b = new Point(x + width * cos, y + width * sin);
                var c = new Point(x - height * sin, y + height * cos);
                var d = new Point(x + width * cos - height * sin, y + width * sin + height * cos);
                var rectangle = enclose(a, b, c, d);
                AssertContains(rectangle, a);
                AssertContains(rectangle, b);
                AssertContains(rectangle, c);
                AssertContains(rectangle, d);
            }
        });
        foreach (var value in new[]
        {
            double.NaN,
            double.PositiveInfinity,
            double.NegativeInfinity
        }

        )
            tests.Test("bounds: nonfinite corner rejected: " + value, () => Check.Throws<InvalidOperationException>(() => enclose(new(value, 0), new(0, 1), new(1, 0), new(1, 1))));
        tests.Test("bounds: overflowing WinRT span is rejected instead of infinite hit region", () => Check.Throws<InvalidOperationException>(() => enclose(new(-float.MaxValue, 0), new(float.MaxValue, 0), new(0, 1), new(1, 1))));
        using var page = new GalleryPage
        {
            Width = 1000,
            Height = 720
        };
        var window = new Window
        {
            Content = page,
            Title = "UnoDock presentation regression"
        };
        window.AppWindow.Resize(new()
        {
            Width = 1100,
            Height = 830
        });
        window.Activate();
        try
        {
            await Wait(() => page.IsLoaded && page.Dock.ActualWidth > 0);
            foreach (var scenario in new[]
            {
                "rotate25",
                "rotate-negative",
                "reflect",
                "skew",
                "offset"
            }

            )
                tests.Test("bounds: public DropArea contains transformed corners: " + scenario, async () =>
                {
                    var saved = page.Content;
                    var target = new Grid();
                    var source = new Grid
                    {
                        Width = 719.37,
                        Height = 613.19,
                        HorizontalAlignment = HorizontalAlignment.Left,
                        VerticalAlignment = VerticalAlignment.Top,
                        Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent)
                    };
                    target.Children.Add(source);
                    page.Content = target;
                    try
                    {
                        await Wait(() => source.IsLoaded && source.ActualWidth > 0);
                        source.RenderTransform = scenario switch
                        {
                            "rotate25" => new RotateTransform
                            {
                                Angle = 25
                            },
                            "rotate-negative" => new RotateTransform
                            {
                                Angle = -133.7,
                                CenterX = 150.1,
                                CenterY = 70.7
                            },
                            "reflect" => new MatrixTransform
                            {
                                Matrix = new(-.9, .25, .4, 1.3, 410.2, -27.3)
                            },
                            "skew" => new SkewTransform
                            {
                                AngleX = 17.3,
                                AngleY = -11.9
                            },
                            _ => new TranslateTransform
                            {
                                X = -11001.17,
                                Y = 4703.29
                            }
                        };
                        var area = new DropArea<Grid>(source, DropAreaType.DocumentPane, target);
                        var transform = source.TransformToVisual(target);
                        foreach (var point in new[]
                        {
                            new Point(0, 0),
                            new Point(source.ActualWidth, 0),
                            new Point(0, source.ActualHeight),
                            new Point(source.ActualWidth, source.ActualHeight)
                        }

                        )
                            AssertContains(area.DetectionRect, transform.TransformPoint(point));
                        source.Visibility = Visibility.Collapsed;
                        area.Refresh();
                        Check.Equal(0d, area.DetectionRect.Width);
                    }
                    finally
                    {
                        target.Children.Clear();
                        page.Content = saved;
                        await Wait(() => page.Dock.IsLoaded);
                    }
                });
            foreach (var theme in Enum.GetValues<SampleTheme>())
                tests.Test("menu: native header geometry and title visibility in " + theme, async () =>
                {
                    page.SetSampleTheme(theme);
                    page.UpdateLayout();
                    await Task.Delay(40);
                    var menu = page.FindVisualChildren<MenuBar>().Single();
                    Check.Equal(5, menu.Items.Count);
                    Check.Near(28, menu.ActualHeight);
                    foreach (var item in menu.Items)
                    {
                        Check.True(item.ActualHeight > 0 && item.ActualHeight <= 28, "Native menu item escaped the sample row.");
                        var button = item.FindVisualChildren<Button>().Single(child => child.Name == "ContentButton");
                        Check.True(button.ActualHeight > 0 && button.ActualHeight <= item.ActualHeight);
                        Check.Equal(item.Title, button.Content as string);
                        var text = button.FindVisualChildren<TextBlock>().First(child => child.Text == item.Title);
                        Check.True(text.ActualHeight > 0 && text.ActualHeight <= button.ActualHeight, "Compact template clipped the native menu title.");
                    }

                    Check.True(page.Dock.ActualHeight >= page.ActualHeight - 90);
                });
            if (OperatingSystem.IsLinux() && Environment.GetEnvironmentVariable("UNODOCK_NATIVE_INPUT_TESTS") == "1")
            {
                tests.Test("XTEST: compact File menu opens and invokes its real New document row", async () =>
                {
                    window.Activate();
                    page.SwitchSample(SampleKind.Classic);
                    page.UpdateLayout();
                    await Task.Delay(80);
                    using var input = new X11TestInput();
                    var file = page.FindVisualChildren<MenuBar>().Single().Items[0];
                    await input.Click(file);
                    await Wait(() => FindCommand(page, "new") is { ActualHeight: > 0 });
                    var command = FindCommand(page, "new")!;
                    await input.Click(command);
                    await Wait(() => page.Dock.Layout.Descendents().OfType<LayoutDocument>().Count() == 3);
                    input.Escape();
                });
                tests.Test("XTEST: Escape closes the compact native menu without invoking a command", async () =>
                {
                    window.Activate();
                    page.SwitchSample(SampleKind.Classic);
                    page.UpdateLayout();
                    await Task.Delay(80);
                    using var input = new X11TestInput();
                    var file = page.FindVisualChildren<MenuBar>().Single().Items[0];
                    await input.Click(file);
                    await Wait(() => FindCommand(page, "new") is { ActualHeight: > 0 });
                    input.Escape();
                    await Wait(() => FindCommand(page, "new") == null);
                    Check.Equal(2, page.Dock.Layout.Descendents().OfType<LayoutDocument>().Count());
                });
            }

            return await tests.Run(output, "presentation-quality");
        }
        finally
        {
            window.Content = null;
            window.Close();
        }
    }

    private static MenuFlyoutItem? FindCommand(GalleryPage page, string id)
    {
        if (page.XamlRoot == null)
            return null;
        foreach (var popup in VisualTreeHelper.GetOpenPopupsForXamlRoot(page.XamlRoot))
            if (popup.IsOpen && popup.Child is FrameworkElement root)
                foreach (var item in root.FindVisualChildren<MenuFlyoutItem>())
                    if (AutomationProperties.GetAutomationId(item) == "SampleCommand-" + id)
                        return item;
        return null;
    }

    private static void AssertContains(Rect rectangle, Point point)
    {
        Check.True(point.X >= rectangle.Left && point.X <= rectangle.Right && point.Y >= rectangle.Top && point.Y <= rectangle.Bottom, $"Corner {point} is outside {rectangle}; conservative bounds may not exclude an endpoint.");
    }

    private static async Task Wait(Func<bool> condition)
    {
        for (var i = 0; i < 100 && !condition(); i++)
            await Task.Delay(20);
        Check.True(condition(), "Presentation condition did not converge in the bounded wait.");
    }
}
