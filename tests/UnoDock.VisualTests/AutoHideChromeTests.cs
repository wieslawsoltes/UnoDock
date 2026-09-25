using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation.Provider;
using Microsoft.UI.Xaml.Media.Imaging;
using UnoDock.Controls;
using UnoDock.Layout;

namespace UnoDock.Testing;

internal static class AutoHideChromeTests
{
    internal static void Register(TestRunner tests, DockingManager dock, FrameworkElement scene, Func<LayoutAutoHideWindowControl> window, Func<LayoutAnchorable> model, Func<Task> reset, Func<Task> settle)
    {
        tests.Test("auto-hide caption dropdown uses current shared-menu context and retains expiry", async () =>
        {
            await reset();
            var entry = new MenuFlyoutItem
            {
                Text = "Application command"
            };
            var menu = new MenuFlyout();
            menu.Items.Add(entry);
            dock.AnchorableContextMenu = menu;
            try
            {
                await settle();
                var button = window().FindVisualChildren<Button>().Single(b => b.Name == "PART_AutoHideMenuButton");
                Check.Equal("Auto-hidden tool menu", AutomationProperties.GetName(button));
                var peer = FrameworkElementAutomationPeer.CreatePeerForElement(button)!;
                ((IInvokeProvider)peer.GetPattern(PatternInterface.Invoke)!).Invoke();
                await Task.Delay(50);
                Check.Same(button, menu.Target);
                Check.Same(dock.GetLayoutItemFromModel(model()), entry.DataContext);
                await Task.Delay(180);
                Check.True(dock.AutoHideWindow != null);
            }
            finally
            {
                menu.Hide();
                dock.AnchorableContextMenu = null;
                await Task.Delay(100);
            }
        });
        tests.Test("auto-hide caption keeps observed compact height and explicit palette overrides", async () =>
        {
            await reset();
            var title = window().FindVisualChildren<Grid>().Single(g => g.Name == "PART_AutoHideTitleBar");
            Check.Near(16, title.ActualHeight, .2);
            Check.Equal(Microsoft.UI.ColorHelper.FromArgb(255, 240, 240, 240), ((SolidColorBrush)title.Background).Color);
            var custom = new SolidColorBrush(Microsoft.UI.Colors.CornflowerBlue);
            try
            {
                dock.Resources["UnoDock.AutoHideTitleBrush"] = custom;
                dock.Resources["UnoDock.AutoHideTitleHeight"] = 30d;
                await settle();
                Check.Same(custom, title.Background);
                Check.Near(30, title.ActualHeight, .2);
            }
            finally
            {
                dock.Resources.Remove("UnoDock.AutoHideTitleBrush");
                dock.Resources.Remove("UnoDock.AutoHideTitleHeight");
            }
        });
        tests.Test("auto-hide capture paints the complete rail and all six editor text rows", async () =>
        {
            await reset();
            Check.Equal(6, ((TextBox)model().Content!).Text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n').Length);
            var bitmap = new RenderTargetBitmap();
            await bitmap.RenderAsync(scene, 1000, 640);
            var pixels = (await bitmap.GetPixelsAsync()).ToArray();
            Check.Equal((byte)255, pixels[(300 * 1000 + 10) * 4 + 3]);
            Check.Equal((byte)255, pixels[(300 * 1000 + 10) * 4]);
            var client = new RenderTargetBitmap();
            await client.RenderAsync(window(), 326, 640);
            pixels = (await client.GetPixelsAsync()).ToArray();
            var rows = 0;
            var inkBefore = false;
            // Restrict inspection to application-owned text, excluding caption,
            // border, resize gutter and caret. This detects the single-line fixture
            // regression that successful size/selection assertions did not reveal.
            for (var y = 24; y < 145; y++)
            {
                var ink = false;
                for (var x = 8; x < 250; x++)
                {
                    var offset = (y * 326 + x) * 4;
                    if (pixels[offset + 3] > 200 && pixels[offset] < 128 && pixels[offset + 1] < 128 && pixels[offset + 2] < 128)
                    {
                        ink = true;
                        break;
                    }
                }

                if (ink && !inkBefore)
                    rows++;
                inkBefore = ink;
            }

            Check.True(rows >= 6, $"Only {rows} text ink bands were rendered in the six-line editor.");
        });
    }
}
