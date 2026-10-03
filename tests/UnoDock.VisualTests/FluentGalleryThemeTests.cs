using System.Reflection;
using UnoDock.Gallery;
using UnoDock.Layout;

namespace UnoDock.Testing;

internal static class FluentGalleryThemeTests
{
    internal static void Add(TestRunner tests, GalleryPage page)
    {
        foreach (var localOverride in new[]
        {
            false,
            true
        }

        )
        {
            tests.Test("Fluent resources: Gallery frame matches theme and retains local values, override=" + localOverride, async () =>
            {
                try
                {
                    await Wait(() => page.IsLoaded && page.Dock.ActualWidth > 0);
                    page.Dock.Refresh();
                    var root = page.Dock.Layout;
                    var editors = root.Descendents().OfType<LayoutDocument>().Select(doc => doc.Content).ToArray();
                    var template = page.Dock.Template;
                    var background = new SolidColorBrush(Microsoft.UI.Colors.DarkSlateBlue);
                    var border = new SolidColorBrush(Microsoft.UI.Colors.Goldenrod);
                    if (localOverride)
                    {
                        page.Dock.Background = background;
                        page.Dock.BorderBrush = border;
                    }

                    foreach (var mode in new[]
                    {
                        SampleTheme.Light,
                        SampleTheme.Dark,
                        SampleTheme.Dark,
                        SampleTheme.Generic,
                        SampleTheme.Dark,
                        SampleTheme.Light
                    }

                    )
                    {
                        page.SetSampleTheme(mode);
                        page.Dock.Refresh();
                        page.UpdateLayout();
                        await Task.Delay(30);
                        if (localOverride)
                        {
                            Check.Same(background, page.Dock.Background);
                            Check.Same(border, page.Dock.BorderBrush);
                        }
                        else if (mode != SampleTheme.Generic)
                        {
                            var palette = typeof(DockingManager).Assembly.GetType("UnoDock.Internal.DockChrome", true)!.GetMethod("Palette", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [page.Dock])!;
#if HAS_UNO
                            Check.Same(palette.GetType().GetProperty("Surface")!.GetValue(palette), page.Dock.Background);
                            Check.Same(palette.GetType().GetProperty("Border")!.GetValue(palette), page.Dock.BorderBrush);
#else
                            // Native WinUI resolves a style setter's ThemeResource from the dictionary that
                            // defines the style: the frame follows the theme with that dictionary's brushes.
                            static bool IsDark(Brush? brush) => brush is SolidColorBrush { Color: var c } && c.R + c.G + c.B < 3 * 128;
                            var dark = mode == SampleTheme.Dark;
                            Check.Equal(dark, IsDark(page.Dock.Background));
                            Check.Equal(dark, IsDark((Brush)palette.GetType().GetProperty("Surface")!.GetValue(palette)!));
#endif
                        }

                        Check.Same(root, page.Dock.Layout);
                        Check.Same(template, page.Dock.Template);
                        Check.True(editors.SequenceEqual(root.Descendents().OfType<LayoutDocument>().Select(doc => doc.Content)));
                    }
                }
                finally
                {
                    if (localOverride)
                    {
                        page.Dock.ClearValue(Control.BackgroundProperty);
                        page.Dock.ClearValue(Control.BorderBrushProperty);
                    }

                    page.SetSampleTheme(SampleTheme.Light);
                    page.Dock.Refresh();
                }
            });
        }
    }

    private static async Task Wait(Func<bool> condition)
    {
        for (var i = 0; i < 100 && !condition(); i++)
            await Task.Delay(20);
        Check.True(condition(), "The Gallery did not load for theme acceptance.");
    }
}
