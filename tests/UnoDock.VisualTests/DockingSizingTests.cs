using System.Reflection;
using System.Runtime.ExceptionServices;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation.Provider;
using UnoDock.Controls;

namespace UnoDock.Testing;
/// <summary>Arranged multi-pane sizing, independent of the original two-pane fixtures.</summary>
internal static class DockingSizingTests
{
    internal static async Task<int> Run(string output)
    {
        var tests = new TestRunner();
        var window = new Window
        {
            Title = "Dock sizing regressions"
        };
        window.AppWindow.Resize(new()
        {
            Width = 1100,
            Height = 1100
        });
        window.Activate();
        foreach (var horizontal in new[]
        {
            true,
            false
        }

        )
        {
            foreach (var rtl in new[]
            {
                false,
                true
            }

            )
            {
                var axis = $"H={horizontal}, RTL={rtl}";
                foreach (var fixedFirst in new[]
                {
                    true,
                    false
                }

                )
                {
                    Add($"sizing: mixed pixel/star resize does not steal from a third star ({fixedFirst}, {axis})", async f =>
                    {
                        f.Length(0, fixedFirst ? new(200) : new(2, GridUnitType.Star));
                        f.Length(1, fixedFirst ? new(2, GridUnitType.Star) : new(200));
                        f.Length(2, new(4, GridUnitType.Star));
                        await f.Settle();
                        var first = f.Pixels(0);
                        var second = f.Pixels(1);
                        var third = f.Pixels(2);
                        var otherLength = f.Length(2);
                        var editor = f.Dock.GetLayoutItemFromModel(f.Document).View;
                        f.Drag(30);
                        await f.Settle();
                        Check.Near(first + 30, f.Pixels(0), 1.1);
                        Check.Near(second - 30, f.Pixels(1), 1.1);
                        Check.Near(third, f.Pixels(2), 1.1);
                        Check.Equal(otherLength, f.Length(2));
                        Check.Same(editor, f.Dock.GetLayoutItemFromModel(f.Document).View);
                    });
                }

                Add($"sizing: constrained star pair follows the preview with a third star ({axis})", async f =>
                {
                    f.Length(0, new(1, GridUnitType.Star));
                    f.Length(1, new(1, GridUnitType.Star));
                    f.Length(2, new(1, GridUnitType.Star));
                    f.Minimum(0, 440);
                    await f.Settle();
                    var first = f.Pixels(0);
                    var second = f.Pixels(1);
                    var third = f.Pixels(2);
                    var other = f.Length(2);
                    f.Drag(25);
                    await f.Settle();
                    Check.Near(first + 25, f.Pixels(0), 1.1);
                    Check.Near(second - 25, f.Pixels(1), 1.1);
                    Check.Near(third, f.Pixels(2), 1.1);
                    Check.Equal(other, f.Length(2));
                });
                Add($"sizing: constrained mixed star grows without moving a sibling ({axis})", async f =>
                {
                    f.Length(0, new(200));
                    f.Length(1, new(.1, GridUnitType.Star));
                    f.Length(2, new(2, GridUnitType.Star));
                    f.Minimum(1, 360);
                    await f.Settle();
                    var first = f.Pixels(0);
                    var second = f.Pixels(1);
                    var third = f.Pixels(2);
                    f.Drag(-25);
                    await f.Settle();
                    Check.Near(first - 25, f.Pixels(0), 1.1);
                    Check.Near(second + 25, f.Pixels(1), 1.1);
                    Check.Near(third, f.Pixels(2), 1.1);
                });
                Add($"sizing: absolute automation sizes a mixed pair without moving a sibling ({axis})", async f =>
                {
                    f.Length(0, new(2, GridUnitType.Star));
                    f.Length(1, new(200));
                    f.Length(2, new(4, GridUnitType.Star));
                    await f.Settle();
                    var requested = f.Pixels(0) + 20;
                    var third = f.Pixels(2);
                    var provider = FrameworkElementAutomationPeer.CreatePeerForElement(f.Splitter)?.GetPattern(PatternInterface.RangeValue) as IRangeValueProvider;
                    Check.True(provider != null && !provider.IsReadOnly);
                    provider!.SetValue(requested);
                    await f.Settle();
                    Check.Near(requested, f.Pixels(0), 1.1);
                    Check.Near(third, f.Pixels(2), 1.1);
                });
                Add($"sizing: batched sibling edits revoke an in-flight resize ({axis})", async f =>
                {
                    f.Length(0, new(200));
                    f.Length(1, new(2, GridUnitType.Star));
                    f.Length(2, new(4, GridUnitType.Star));
                    await f.Settle();
                    var a = f.Length(0);
                    var b = f.Length(1);
                    Call(f.Splitter, "BeginResize");
                    Call(f.Splitter, "UpdateResize", 30d);
                    using (f.Dock.Layout.BeginUpdate())
                    {
                        f.Length(2, new(7, GridUnitType.Star));
                        Call(f.Splitter, "EndResize", false);
                    }

                    await f.Settle();
                    Check.Equal(a, f.Length(0));
                    Check.Equal(b, f.Length(1));
                    Check.Equal(new GridLength(7, GridUnitType.Star), f.Length(2));
                    Check.False(f.Splitter.IsDragging);
                });
                Add($"docking: orthogonal split preserves the outer slot dimensions ({axis})", async f =>
                {
                    f.Length(0, new(200));
                    f.Length(1, new(2, GridUnitType.Star));
                    f.Length(2, new(1, GridUnitType.Star));
                    f.Minimum(1, 150);
                    await f.Settle();
                    var target = f.Panes[1];
                    var original = (target.DockWidth, target.DockHeight, target.DockMinWidth, target.DockMinHeight);
                    var sibling = f.Pixels(2);
                    var source = new LayoutDocument
                    {
                        Title = "Floating",
                        ContentId = "sizing:float",
                        Content = new TextBox
                        {
                            Text = "Retained"
                        }
                    };
                    f.Dock.Layout.FloatingWindows.Add(new LayoutDocumentFloatingWindow { RootDocument = source });
                    var position = horizontal ? DockPosition.Top : DockPosition.Left;
                    DockOperations.Dock(source, (ILayoutGroup)target, position);
                    await f.Settle();
                    Check.True(source.Parent is LayoutDocumentPane);
                    Check.False(source.IsFloating);
                    var wrapper = ((ILayoutElement)target).Parent as ILayoutPositionableElement;
                    Check.True(wrapper != null && !ReferenceEquals(wrapper, f.Panel));
                    Check.Equal(original, (wrapper!.DockWidth, wrapper.DockHeight, wrapper.DockMinWidth, wrapper.DockMinHeight));
                    Check.Near(sibling, f.Pixels(2), 1.1);
                });
                Add($"sizing: resize keeps a minimum-bound outside star stationary ({axis})", async f =>
                {
                    f.Length(0, new(200));
                    f.Length(1, new(1, GridUnitType.Star));
                    f.Length(2, new(.001, GridUnitType.Star));
                    f.Minimum(2, 450);
                    await f.Settle();
                    var a = f.Pixels(0);
                    var b = f.Pixels(1);
                    var c = f.Pixels(2);
                    f.Drag(30);
                    await f.Settle();
                    Check.Near(a + 30, f.Pixels(0), 1.1);
                    Check.Near(b - 30, f.Pixels(1), 1.1);
                    Check.Near(c, f.Pixels(2), 1.1);
                });
                Add($"sizing: a constrained star pair can resize beside a pixel pane ({axis})", async f =>
                {
                    f.Length(0, new(1, GridUnitType.Star));
                    f.Length(1, new(1, GridUnitType.Star));
                    f.Length(2, new(150));
                    f.Minimum(0, 650);
                    await f.Settle();
                    var a = f.Pixels(0);
                    var b = f.Pixels(1);
                    var c = f.Pixels(2);
                    f.Drag(25);
                    await f.Settle();
                    Check.Near(a + 25, f.Pixels(0), 1.1);
                    Check.Near(b - 25, f.Pixels(1), 1.1);
                    Check.Near(c, f.Pixels(2), 1.1);
                });
                Add($"sizing: a minimum-sized zero-weight star can grow ({axis})", async f =>
                {
                    f.Length(0, new(0, GridUnitType.Star));
                    f.Length(1, new(1, GridUnitType.Star));
                    f.Length(2, new(1, GridUnitType.Star));
                    f.Minimum(0, 300);
                    await f.Settle();
                    var a = f.Pixels(0);
                    var b = f.Pixels(1);
                    var c = f.Pixels(2);
                    f.Drag(25);
                    await f.Settle();
                    Check.Near(a + 25, f.Pixels(0), 1.1);
                    Check.Near(b - 25, f.Pixels(1), 1.1);
                    Check.Near(c, f.Pixels(2), 1.1);
                });
                Add($"sizing: batched sibling detach and return revokes the old resize ({axis})", async f =>
                {
                    await f.Settle();
                    var a = f.Length(0);
                    var b = f.Length(1);
                    var splitter = f.Splitter;
                    Call(splitter, "BeginResize");
                    Call(splitter, "UpdateResize", 25d);
                    using (f.Dock.Layout.BeginUpdate())
                    {
                        var other = f.Panel.Children[2];
                        f.Panel.Children.RemoveAt(2);
                        f.Panel.Children.Add(other);
                        Call(splitter, "EndResize", false);
                    }

                    await f.Settle();
                    Check.Equal(a, f.Length(0));
                    Check.Equal(b, f.Length(1));
                    Check.False(splitter.IsDragging);
                });
                Add($"sizing: callback edits to an outside star retire the pair without overwriting the edit ({axis})", async f =>
                {
                    f.Length(0, new(200));
                    f.Length(1, new(2, GridUnitType.Star));
                    f.Length(2, new(4, GridUnitType.Star));
                    await f.Settle();
                    var a = f.Length(0);
                    var b = f.Length(1);
                    var invoked = false;
                    System.ComponentModel.PropertyChangedEventHandler handler = (_, args) =>
                    {
                        if (!invoked && args.PropertyName == (horizontal ? "DockWidth" : "DockHeight"))
                        {
                            invoked = true;
                            f.Length(2, new(7, GridUnitType.Star));
                        }
                    };
                    ((LayoutElement)f.Panes[0]).PropertyChanged += handler;
                    try
                    {
                        f.Drag(25);
                    }
                    finally
                    {
                        ((LayoutElement)f.Panes[0]).PropertyChanged -= handler;
                    }

                    await f.Settle();
                    Check.True(invoked);
                    Check.Equal(a, f.Length(0));
                    Check.Equal(b, f.Length(1));
                    Check.Equal(new GridLength(7, GridUnitType.Star), f.Length(2));
                });
                Add($"docking: orthogonal split keeps a fixed pixel outer slot ({axis})", async f =>
                {
                    f.Length(1, new(300));
                    await f.Settle();
                    var other = f.Pixels(2);
                    var target = f.Panes[1];
                    var source = new LayoutDocument
                    {
                        Title = "F",
                        ContentId = "sizing:pixel-float"
                    };
                    f.Dock.Layout.FloatingWindows.Add(new LayoutDocumentFloatingWindow { RootDocument = source });
                    DockOperations.Dock(source, (ILayoutGroup)target, horizontal ? DockPosition.Bottom : DockPosition.Right);
                    await f.Settle();
                    var wrapper = (ILayoutPositionableElement)((ILayoutElement)target).Parent!;
                    Check.Equal(new GridLength(300), horizontal ? wrapper.DockWidth : wrapper.DockHeight);
                    Check.Near(other, f.Pixels(2), 1.1);
                });
                void Add(string name, Func<Fixture, Task> body) => tests.Test(name, async () =>
                {
                    using var f = new Fixture(window, horizontal, rtl);
                    await f.Show();
                    await body(f);
                });
            }
        }

        try
        {
            return await tests.Run(output, "docking-sizing");
        }
        finally
        {
            window.Content = null;
            window.Close();
        }
    }

    private sealed class Fixture : IDisposable
    {
        internal readonly DockingManager Dock = new()
        {
            Width = 1000,
            Height = 1000,
            FloatingWindowMode = FloatingWindowMode.InSurface
        };
        internal readonly LayoutPanel Panel;
        internal readonly ILayoutPositionableElement[] Panes;
        internal readonly LayoutDocument Document;
        private readonly Window _window;
        private readonly bool _horizontal;
        internal LayoutPanelControl Grid => Dock.FindVisualChildren<LayoutPanelControl>().Single(view => ReferenceEquals(view.Model, Panel));
        internal LayoutGridResizerControl Splitter => Grid.Children.OfType<LayoutGridResizerControl>().First();

        internal Fixture(Window window, bool horizontal, bool rtl)
        {
            _horizontal = horizontal;
            Document = new LayoutDocument
            {
                Title = "D",
                ContentId = "sizing:document",
                Content = new TextBox
                {
                    Text = "Editor"
                }
            };
            var first = new LayoutAnchorablePane(new LayoutAnchorable { Title = "T", ContentId = "sizing:tool", Content = new TextBlock { Text = "T" } });
            var second = new LayoutDocumentPane(Document);
            var third = new LayoutDocumentPane(new LayoutDocument { Title = "Other", ContentId = "sizing:other", Content = new TextBlock { Text = "Other" } });
            Panes = [first, second, third];
            Panel = new LayoutPanel(first)
            {
                Orientation = horizontal ? Orientation.Horizontal : Orientation.Vertical
            };
            Panel.Children.Add(second);
            Panel.Children.Add(third);
            Dock.Layout = new LayoutRoot
            {
                RootPanel = Panel
            };
            Dock.FlowDirection = rtl ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
            _window = window;
            _window.Content = Dock;
        }

        internal GridLength Length(int index) => _horizontal ? Panes[index].DockWidth : Panes[index].DockHeight;
        internal void Length(int index, GridLength value)
        {
            if (_horizontal)
                Panes[index].DockWidth = value;
            else
                Panes[index].DockHeight = value;
        }

        internal void Minimum(int index, double value)
        {
            if (_horizontal)
                Panes[index].DockMinWidth = value;
            else
                Panes[index].DockMinHeight = value;
        }

        internal double Pixels(int index) => _horizontal ? Grid.ColumnDefinitions[index * 2].ActualWidth : Grid.RowDefinitions[index * 2].ActualHeight;
        internal void Drag(double delta)
        {
            var splitter = Splitter;
            Call(splitter, "BeginResize");
            Check.True(splitter.IsDragging);
            Call(splitter, "UpdateResize", delta);
            Call(splitter, "EndResize", false);
        }

        internal async Task Show()
        {
            for (var i = 0; i < 100 && !Dock.IsLoaded; i++)
                await Task.Delay(20);
            Check.True(Dock.IsLoaded);
            await Settle();
        }

        internal async Task Settle()
        {
            Dock.Refresh();
            Dock.UpdateLayout();
            await Task.Delay(45);
            Dock.UpdateLayout();
        }

        public void Dispose()
        {
            _window.Content = null;
            Dock.Dispose();
        }
    }

    private static object? Call(object target, string name, params object[] args)
    {
        try
        {
            return target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(target, args);
        }
        catch (TargetInvocationException e) when (e.InnerException != null)
        {
            ExceptionDispatchInfo.Capture(e.InnerException).Throw();
            throw;
        }
    }
}
