using UnoDock.Controls;

namespace UnoDock.Testing;

internal static class DropDownKeyboardTests
{
    internal static void Register(TestRunner tests, StackPanel root, Window window)
    {
        if (!OperatingSystem.IsLinux() || Environment.GetEnvironmentVariable("UNODOCK_NATIVE_INPUT_TESTS") != "1")
            return;
        foreach (var key in new nuint[]
        {
            0xffe9,
            0xffea
        }

        )
            Add("XTEST Alt must not be interpreted as the context-menu key: " + key, async (area, menu, input) =>
            {
                var openings = 0;
                menu.Opened += (_, _) => openings++;
                input.KeyPress(key);
                await Task.Delay(140);
                Check.Equal(0, openings);
                Check.False(menu.IsOpen);
            });
        Add("XTEST Shift+F10 opens the context menu and Escape dismisses", async (area, menu, input) =>
        {
            input.KeyDown(0xffe1);
            await Task.Delay(30);
            input.KeyPress(0xffc7);
            input.KeyUp(0xffe1);
            await Wait(() => menu.IsOpen);
            await Task.Delay(80);
            input.Escape();
            await Wait(() => !menu.IsOpen);
        });
        void Add(string name, Func<DropDownControlArea, MenuFlyout, X11TestInput, Task> body) => tests.Test(name, async () =>
        {
            var menu = new MenuFlyout();
            menu.Items.Add(new MenuFlyoutItem { Text = "Document action" });
            var area = new DropDownControlArea
            {
                IsTabStop = true,
                Width = 360,
                Height = 60,
                DropDownContextMenu = menu,
                Content = new TextBlock
                {
                    Text = "Native keyboard target",
                    Margin = new(12)
                }
            };
            root.Children.Add(area);
            try
            {
                await Wait(() => area.IsLoaded);
                window.Activate();
                root.UpdateLayout();
                await Task.Delay(80);
                Check.True(area.Focus(FocusState.Keyboard));
                await Wait(() => ReferenceEquals(Microsoft.UI.Xaml.Input.FocusManager.GetFocusedElement(area.XamlRoot!), area));
                using var input = new X11TestInput();
                await body(area, menu, input);
            }
            finally
            {
                area.CloseDropDown();
                root.Children.Remove(area);
                await Task.Delay(35);
            }
        });
    }

    private static async Task Wait(Func<bool> condition)
    {
        for (var i = 0; i < 80; i++)
        {
            if (condition())
                return;
            await Task.Delay(20);
        }

        Check.True(condition(), "Native dropdown keyboard operation did not converge.");
    }
}
