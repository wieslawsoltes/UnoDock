using Microsoft.UI.Xaml.Controls;
using UnoDock.Controls;
using DockButton = UnoDock.Controls.DropDownButton;

namespace UnoDock.Testing;
internal static class DropDownTransitionTests
{
    private sealed class Trigger
    {
        private readonly DockButton? _button;
        private readonly DropDownControlArea? _area;
        internal Control View => (Control? )_button ?? _area!;

        internal Trigger(bool area)
        {
            if (area)
                _area = new()
                {
                    Content = new TextBlock
                    {
                        Text = "Context area"
                    }
                };
            else
                _button = new()
                {
                    Content = "Dropdown"
                };
            View.Width = 180;
            View.Height = 50;
        }

        internal MenuFlyout Menu
        {
            get => (_button != null ? _button.DropDownContextMenu : _area!.DropDownContextMenu)!;
            set
            {
                if (_button != null)
                    _button.DropDownContextMenu = value;
                else
                    _area!.DropDownContextMenu = value;
            }
        }

        internal object? Context
        {
            set
            {
                if (_button != null)
                    _button.DropDownContextMenuDataContext = value;
                else
                    _area!.DropDownContextMenuDataContext = value;
            }
        }

        internal void Open()
        {
            if (_button != null)
                _button.OpenDropDown();
            else
                _area!.OpenDropDown();
        }

        internal void Close()
        {
            if (_button != null)
                _button.CloseDropDown();
            else
                _area!.CloseDropDown();
        }

        internal void AssertState(bool open)
        {
            if (_button != null)
                Check.Equal(open, _button.IsChecked);
        }
    }

    internal static void Register(TestRunner tests, StackPanel root, Window window)
    {
        foreach (var area in new[]
        {
            false,
            true
        }

        )
        {
            Add("replacement remains open after the old native Closed finishes", async t =>
            {
                var old = t.Menu;
                t.Open();
                await Wait(() => old.IsOpen);
                t.Close();
                var next = NewMenu(out var row);
                t.Menu = next;
                var context = new object ();
                t.Context = context;
                t.Open();
                await Wait(() => next.IsOpen);
                await Task.Delay(120);
                Check.True(next.IsOpen);
                Check.False(old.IsOpen);
                Check.Same(context, row.DataContext);
                t.AssertState(true);
            });
            Add("close withdraws a deferred reopen", async t =>
            {
                t.Open();
                await Wait(() => t.Menu.IsOpen);
                t.Close();
                t.Open();
                t.Close();
                await Task.Delay(120);
                Check.False(t.Menu.IsOpen);
                t.AssertState(false);
            });
            Add("menu replacement invalidates a deferred reopen", async t =>
            {
                var old = t.Menu;
                t.Open();
                await Wait(() => old.IsOpen);
                t.Close();
                t.Open();
                var next = NewMenu(out _);
                t.Menu = next;
                await Task.Delay(120);
                Check.False(old.IsOpen);
                Check.False(next.IsOpen);
                t.AssertState(false);
            });
            Add("disable invalidates a deferred reopen", async t =>
            {
                t.Open();
                await Wait(() => t.Menu.IsOpen);
                t.Close();
                t.Open();
                t.View.IsEnabled = false;
                await Task.Delay(120);
                Check.False(t.Menu.IsOpen);
                t.AssertState(false);
                t.View.IsEnabled = true;
                t.Open();
                await Wait(() => t.Menu.IsOpen);
            });
            Add("unload invalidates a deferred reopen", async t =>
            {
                t.Open();
                await Wait(() => t.Menu.IsOpen);
                t.Close();
                t.Open();
                root.Children.Remove(t.View);
                await Task.Delay(120);
                Check.False(t.Menu.IsOpen);
                t.AssertState(false);
            });
            Add("only the last shared waiter can reopen or refresh", async t =>
            {
                var second = new Trigger(!area);
                var third = new Trigger(area);
                root.Children.Add(second.View);
                root.Children.Add(third.View);
                try
                {
                    await Wait(() => second.View.IsLoaded && third.View.IsLoaded);
                    t.Menu = NewMenu(out var row);
                    second.Menu = third.Menu = t.Menu;
                    t.Context = "first";
                    second.Context = "second";
                    third.Context = "third";
                    t.Open();
                    await Wait(() => t.Menu.IsOpen);
                    t.Close();
                    second.Open();
                    third.Open();
                    await Wait(() => t.Menu.IsOpen && Equals(row.DataContext, "third"));
                    second.Context = "obsolete refresh";
                    await Task.Delay(120);
                    Check.Equal("third", row.DataContext as string);
                    second.AssertState(false);
                    third.AssertState(true);
                }
                finally
                {
                    second.Close();
                    third.Close();
                    root.Children.Remove(second.View);
                    root.Children.Remove(third.View);
                }
            });
            Add("native closing veto preserves context and checked state", async t =>
            {
                t.Menu = NewMenu(out var row);
                var context = new object ();
                t.Context = context;
                var cancel = true;
                t.Menu.Closing += (_, e) => e.Cancel = cancel;
                try
                {
                    t.Open();
                    await Wait(() => t.Menu.IsOpen);
                    t.Close();
                    await Task.Delay(120);
                    Check.True(t.Menu.IsOpen);
                    Check.Same(context, row.DataContext);
                    t.AssertState(true);
                }
                finally
                {
                    cancel = false;
                    t.Close();
                }

                await Wait(() => !t.Menu.IsOpen);
                Check.Same(DependencyProperty.UnsetValue, row.ReadLocalValue(FrameworkElement.DataContextProperty));
            });
            Add("another trigger cannot steal a close-vetoed menu", async t =>
            {
                var next = new Trigger(!area);
                root.Children.Add(next.View);
                var cancel = true;
                try
                {
                    await Wait(() => next.View.IsLoaded);
                    t.Menu = NewMenu(out var row);
                    next.Menu = t.Menu;
                    t.Context = "original";
                    next.Context = "candidate";
                    t.Menu.Closing += (_, e) => e.Cancel = cancel;
                    t.Open();
                    await Wait(() => t.Menu.IsOpen);
                    next.Open();
                    await Task.Delay(120);
                    Check.True(t.Menu.IsOpen);
                    Check.Equal("original", row.DataContext as string);
                    t.AssertState(true);
                    next.AssertState(false);
                    next.Context = "late refresh";
                    await Task.Delay(80);
                    Check.Equal("original", row.DataContext as string);
                }
                finally
                {
                    cancel = false;
                    next.Close();
                    t.Close();
                    root.Children.Remove(next.View);
                }
            });
            Add("one-shot native closing exception cleans up and permits reopening", async t =>
            {
                t.Menu = NewMenu(out var row);
                t.Context = new object ();
                var fail = true;
                t.Menu.Closing += (_, _) =>
                {
                    if (fail)
                    {
                        fail = false;
                        throw new InvalidOperationException("close fault");
                    }
                };
                t.Open();
                await Wait(() => t.Menu.IsOpen);
                Check.Throws<InvalidOperationException>(t.Close);
                await Wait(() => !t.Menu.IsOpen);
                t.AssertState(false);
                Check.Same(DependencyProperty.UnsetValue, row.ReadLocalValue(FrameworkElement.DataContextProperty));
                t.Open();
                await Wait(() => t.Menu.IsOpen);
            });
            Add("persistent native closing failures preserve scope and do not poison future openings", async t =>
            {
                t.Menu = NewMenu(out var row);
                var context = new object ();
                t.Context = context;
                var fail = true;
                t.Menu.Closing += (_, _) =>
                {
                    if (fail)
                        throw new InvalidOperationException("persistent close fault");
                };
                try
                {
                    t.Open();
                    await Wait(() => t.Menu.IsOpen);
                    var error = Check.Throws<AggregateException>(t.Close);
                    Check.True(error.Flatten().InnerExceptions.Count >= 2);
                    Check.True(t.Menu.IsOpen);
                    Check.Same(context, row.DataContext);
                    t.AssertState(true);
                }
                finally
                {
                    fail = false;
                    t.Close();
                }

                await Wait(() => !t.Menu.IsOpen);
                t.Menu = NewMenu(out _);
                t.Open();
                await Wait(() => t.Menu.IsOpen);
            });
            Add("source-generated menu rows receive and release trigger context", async t =>
            {
                var row = new MenuFlyoutItem
                {
                    Text = "Source-owned row"
                };
                t.Menu = new ContextMenuEx
                {
                    ItemsSource = new[]
                    {
                        row
                    }
                };
                var context = new object ();
                t.Context = context;
                t.Open();
                await Wait(() => t.Menu.IsOpen);
                Check.Same(row, t.Menu.Items[0]);
                Check.Same(context, row.DataContext);
                t.Close();
                await Wait(() => !t.Menu.IsOpen);
                Check.Same(DependencyProperty.UnsetValue, row.ReadLocalValue(FrameworkElement.DataContextProperty));
            });
            Add("explicit menu context has precedence over later trigger refresh", async t =>
            {
                var row = new MenuFlyoutItem
                {
                    Text = "Explicit context"
                };
                var context = new object ();
                t.Menu = new ContextMenuEx
                {
                    ItemsSource = new[]
                    {
                        row
                    },
                    MenuDataContext = context
                };
                t.Context = new object ();
                t.Open();
                await Wait(() => t.Menu.IsOpen);
                Check.Same(context, row.DataContext);
                t.Context = new object ();
                Check.Same(context, row.DataContext);
            });
            void Add(string name, Func<Trigger, Task> body) => tests.Test((area ? "area fence: " : "button fence: ") + name, async () =>
            {
                var trigger = new Trigger(area)
                {
                    Menu = NewMenu(out _)
                };
                root.Children.Add(trigger.View);
                window.Activate();
                try
                {
                    await Wait(() => trigger.View.IsLoaded);
                    root.UpdateLayout();
                    await body(trigger);
                }
                finally
                {
                    trigger.Close();
                    root.Children.Remove(trigger.View);
                    await Task.Delay(35);
                }
            });
        }
    }

    private static MenuFlyout NewMenu(out MenuFlyoutItem row)
    {
        var menu = new MenuFlyout();
        row = new()
        {
            Text = "Document action"
        };
        menu.Items.Add(row);
        return menu;
    }

    private static async Task Wait(Func<bool> condition)
    {
        for (var i = 0; i < 80; i++)
        {
            if (condition())
                return;
            await Task.Delay(20);
        }

        Check.True(condition(), "Deferred dropdown transition did not converge.");
    }
}
