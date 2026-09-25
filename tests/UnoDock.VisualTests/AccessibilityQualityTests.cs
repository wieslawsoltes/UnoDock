using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Windows.Input;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation.Provider;
using UnoDock.Controls;
using UnoDock.Gallery;
using UnoDock.Themes;
using Windows.System;

namespace UnoDock.Testing;
/// <summary>Provider-level acceptance on real Uno hosts. These tests do not claim
/// external screen-reader or cross-process UIA transport certification.</summary>
internal static class AccessibilityQualityTests
{
    internal static async Task<int> Run(string output)
    {
        var tests = new TestRunner();
        using var dock = new DockingManager
        {
            Width = 1000,
            Height = 640,
            FloatingWindowMode = FloatingWindowMode.InSurface
        };
        var scene = new Grid();
        scene.Children.Add(dock);
        var window = new Window
        {
            Content = scene,
            Title = "UnoDock accessibility provider acceptance"
        };
        using var registration = Microsoft.Windows.Shell.SystemCommands.RegisterWindow(window);
        window.AppWindow.Resize(new() { Width = 1060, Height = 720 });
        window.Activate();
        LayoutAnchorablePane first = null!;
        LayoutDocumentPane second = null!;
        LayoutPanel model = null!;
        LayoutPanelControl grid = null!;
        LayoutGridResizerControl splitter = null!;
        LayoutGridResizerAutomationPeer range = null!;
        LayoutDocument[] documents = [];
        try
        {
            foreach (var horizontal in new[]
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
                    var label = $"H={horizontal}, RTL={rtl}";
                    tests.Test("range: actual size, role, units and bounds: " + label, async () =>
                    {
                        await Reset(horizontal, rtl);
                        Check.Same(range, range.GetPattern(PatternInterface.RangeValue));
                        Check.Equal(AutomationControlType.Thumb, range.GetAutomationControlType());
                        Check.Equal(horizontal ? AutomationOrientation.Horizontal : AutomationOrientation.Vertical, range.GetOrientation());
                        Check.False(range.IsReadOnly);
                        Check.Near(100, range.Minimum);
                        Check.Near(Total() - 100, range.Maximum);
                        Check.Near(Pixels(), range.Value);
                        Check.Near(10, range.SmallChange);
                        Check.Near(50, range.LargeChange);
                    });
                    tests.Test("range: SetValue shares star-preserving transaction: " + label, async () =>
                    {
                        await Reset(horizontal, rtl);
                        var start = range.Value;
                        range.SetValue(start + 23);
                        await Settle();
                        Check.Near(start + 23, range.Value, .6);
                        var a = horizontal ? first.DockWidth : first.DockHeight;
                        var b = horizontal ? second.DockWidth : second.DockHeight;
                        Check.True(a.IsStar && b.IsStar);
                        Check.Near(3, a.Value + b.Value);
                        Check.False(splitter.IsDragging);
                        Check.False(Ghosts());
                    });
                    tests.Test("range: Home, End and page changes use bounded logical sizes: " + label, async () =>
                    {
                        await Reset(horizontal, rtl);
                        Check.True(Key(VirtualKey.Home));
                        await Settle();
                        Check.Near(100, range.Value, .6);
                        Check.True(Key(VirtualKey.PageDown));
                        await Settle();
                        Check.Near(150, range.Value, .6);
                        Check.True(Key(VirtualKey.PageUp));
                        await Settle();
                        Check.Near(100, range.Value, .6);
                        Check.True(Key(VirtualKey.End));
                        await Settle();
                        Check.Near(range.Maximum, range.Value, .6);
                    });
                }

            foreach (var value in new[]
            {
                double.NaN,
                double.PositiveInfinity,
                double.NegativeInfinity,
                -1d,
                10000d
            }

            )
                tests.Test("range: invalid numeric input leaves both endpoints unchanged: " + value, async () =>
                {
                    await Reset();
                    var before = (first.DockWidth, second.DockWidth);
                    Check.Throws<ArgumentOutOfRangeException>(() => range.SetValue(value));
                    Check.Equal(before, (first.DockWidth, second.DockWidth));
                    Check.False(Ghosts());
                });
            tests.Test("range: no-op does not notify, convert Auto, or round lengths", async () =>
            {
                await Reset();
                first.DockWidth = GridLength.Auto;
                await Settle();
                var original = (first.DockWidth, second.DockWidth);
                var changed = 0;
                first.PropertyChanged += (_, _) => changed++;
                second.PropertyChanged += (_, _) => changed++;
                range.SetValue(range.Value);
                Check.Equal(0, changed);
                Check.Equal(original, (first.DockWidth, second.DockWidth));
            });
            tests.Test("range: disabled splitter is read-only and cannot change values", async () =>
            {
                await Reset();
                var old = range.Value;
                var lengths = (first.DockWidth, second.DockWidth);
                splitter.IsEnabled = false;
                Check.True(range.IsReadOnly);
                Check.Near(old, range.Value);
                Check.Throws<InvalidOperationException>(() => range.SetValue(old + 10));
                Check.Equal(lengths, (first.DockWidth, second.DockWidth));
                Check.False(Key(VirtualKey.Home));
                splitter.IsEnabled = true;
                Check.False(range.IsReadOnly);
            });
            tests.Test("range: pointer preview remains committed and rejects competing provider writes", async () =>
            {
                await Reset();
                var old = range.Value;
                Call(splitter, "BeginResize");
                Call(splitter, "UpdateResize", 50d);
                Check.True(splitter.IsDragging);
                Check.True(range.IsReadOnly);
                Check.Near(old, range.Value);
                Check.Throws<InvalidOperationException>(() => range.SetValue(old + 10));
                Check.True(splitter.IsDragging);
                splitter.CancelDrag();
                Check.False(range.IsReadOnly);
                Check.Near(old, range.Value);
            });
            tests.Test("range: root replacement invalidates an already obtained provider", async () =>
            {
                await Reset();
                var old = range;
                var lengths = (first.DockWidth, second.DockWidth);
                dock.Layout = new();
                Check.True(old.IsReadOnly);
                Check.Throws<InvalidOperationException>(() => old.SetValue(150));
                Check.Equal(lengths, (first.DockWidth, second.DockWidth));
            });
            tests.Test("range: reordered endpoints invalidate provider before view refresh", async () =>
            {
                await Reset();
                var old = range;
                model.Children.Move(0, 1);
                Check.True(old.IsReadOnly);
                Check.Throws<InvalidOperationException>(() => old.SetValue(150));
                await Settle();
            });
            tests.Test("range: orientation change invalidates old coordinate axis", async () =>
            {
                await Reset();
                var old = range;
                model.Orientation = Orientation.Vertical;
                Check.True(old.IsReadOnly);
                Check.Throws<InvalidOperationException>(() => old.SetValue(150));
                await Settle();
            });
            tests.Test("range: unload and reattach revalidate rather than retain stale geometry", async () =>
            {
                await Reset();
                var old = range;
                scene.Children.Remove(dock);
                await Task.Delay(30);
                Check.True(old.IsReadOnly);
                Check.Throws<InvalidOperationException>(() => old.SetValue(150));
                scene.Children.Add(dock);
                await Settle();
                Check.False(old.IsReadOnly);
            });
            tests.Test("range: infeasible minima return a finite read-only interval", async () =>
            {
                await Reset();
                first.DockMinWidth = 900;
                second.DockMinWidth = 900;
                // Before another layout is arranged the old span cannot meet the new minima.
                Check.True(range.IsReadOnly);
                Check.True(double.IsFinite(range.Value));
                Check.True(range.Minimum <= range.Value && range.Value <= range.Maximum);
                Check.Throws<InvalidOperationException>(() => range.SetValue(range.Value));
                await Settle();
            });
            tests.Test("range: root-changing observer cannot commit a stale second endpoint", async () =>
            {
                await Reset();
                var original = (first.DockWidth, second.DockWidth);
                var invoked = false;
                first.PropertyChanged += (_, e) =>
                {
                    if (e.PropertyName == "DockWidth" && !invoked)
                    {
                        invoked = true;
                        dock.Layout = new();
                    }
                };
                range.SetValue(range.Value + 20);
                Check.True(invoked);
                Check.Equal(original, (first.DockWidth, second.DockWidth));
                Check.False(Ghosts());
            });
            tests.Test("range: competing endpoint edit survives rollback", async () =>
            {
                await Reset();
                var original = first.DockWidth;
                var invoked = false;
                first.PropertyChanged += (_, e) =>
                {
                    if (e.PropertyName == "DockWidth" && !invoked)
                    {
                        invoked = true;
                        second.DockWidth = new(444);
                    }
                };
                range.SetValue(range.Value + 20);
                Check.Equal(original, first.DockWidth);
                Check.Equal(new GridLength(444), second.DockWidth);
                Check.False(Ghosts());
            });
            tests.Test("range: throwing observer releases the transaction for retry", async () =>
            {
                await Reset();
                var original = (first.DockWidth, second.DockWidth);
                var invoked = false;
                second.PropertyChanged += (_, e) =>
                {
                    if (e.PropertyName == "DockWidth" && !invoked)
                    {
                        invoked = true;
                        throw new InvalidOperationException("range-observer");
                    }
                };
                Check.Throws<InvalidOperationException>(() => range.SetValue(range.Value + 20));
                Check.Equal(original, (first.DockWidth, second.DockWidth));
                Check.False(Ghosts());
                Check.False(range.IsReadOnly);
                range.SetValue(range.Value + 12);
                await Settle();
                Check.True(first.DockWidth != original.Item1);
            });
            tests.Test("range: worker-thread provider calls are rejected before model writes", async () =>
            {
                await Reset();
                var old = (first.DockWidth, second.DockWidth);
                await Task.Run(() => Check.Throws<InvalidOperationException>(() => range.SetValue(200)));
                Check.Equal(old, (first.DockWidth, second.DockWidth));
            });
            tests.Test("range: unattached standalone resizer reports no mutable numeric range", () =>
            {
                var control = new LayoutGridResizerControl();
                var peer = new LayoutGridResizerAutomationPeer(control);
                Check.True(peer.IsReadOnly);
                Check.Near(0, peer.Value);
                Check.Near(0, peer.Minimum);
                Check.Near(0, peer.Maximum);
                Check.Throws<InvalidOperationException>(() => peer.SetValue(0));
            });
            tests.Test("tab: discover selection/invoke and stable model metadata", async () =>
            {
                await Reset();
                var peer = TabPeer(documents[1]);
                Check.Same(peer, peer.GetPattern(PatternInterface.SelectionItem));
                Check.Same(peer, peer.GetPattern(PatternInterface.Invoke));
                Check.Equal(AutomationControlType.TabItem, peer.GetAutomationControlType());
                Check.Equal("document-1", peer.GetAutomationId());
                Check.Equal("Document 1", peer.GetName());
                Check.Equal("Description 1", peer.GetHelpText());
                Check.True(peer.SelectionContainer != null);
            });
            tests.Test("tab: explicit accessibility labels override model metadata", async () =>
            {
                await Reset();
                var tab = Tab(documents[1]);
                var peer = TabPeer(documents[1]);
                AutomationProperties.SetName(tab, "Application label");
                AutomationProperties.SetAutomationId(tab, "app-id");
                AutomationProperties.SetHelpText(tab, "Application help");
                Check.Equal("Application label", peer.GetName());
                Check.Equal("app-id", peer.GetAutomationId());
                Check.Equal("Application help", peer.GetHelpText());
            });
            tests.Test("tab: automation Select and Invoke follow the current command", async () =>
            {
                await Reset();
                var model = documents[1];
                var peer = TabPeer(model);
                var calls = 0;
                dock.GetLayoutItemFromModel(model).ActivateCommand = new Command(() =>
                {
                    calls++;
                    model.IsActive = true;
                });
                peer.Select();
                Check.Equal(1, calls);
                Check.True(peer.IsSelected);
                Check.Same(model, dock.Layout.ActiveContent);
                peer.Invoke();
                Check.Equal(2, calls);
                await Settle();
                Check.Equal(1, PanePeer().GetSelection().Length);
            });
            tests.Test("tab: AddToSelection cannot replace another item in a single-selection pane", async () =>
            {
                await Reset();
                documents[0].IsActive = true;
                Check.Throws<InvalidOperationException>(() => TabPeer(documents[1]).AddToSelection());
                Check.Same(documents[0], dock.Layout.ActiveContent);
                TabPeer(documents[0]).AddToSelection();
                Check.True(TabPeer(documents[0]).IsSelected);
            });
            tests.Test("tab: RemoveFromSelection preserves the required selected item", async () =>
            {
                await Reset();
                documents[0].IsActive = true;
                var selected = TabPeer(documents[0]);
                Check.Throws<InvalidOperationException>(selected.RemoveFromSelection);
                TabPeer(documents[1]).RemoveFromSelection();
                Check.True(selected.IsSelected);
                Check.False(PanePeer().CanSelectMultiple);
                Check.True(PanePeer().IsSelectionRequired);
            });
            tests.Test("tab: disabled model and disabled view cannot activate", async () =>
            {
                await Reset();
                var peer = TabPeer(documents[1]);
                documents[1].IsEnabled = false;
                Check.False(peer.IsEnabled());
                Check.Throws<InvalidOperationException>(peer.Select);
                documents[1].IsEnabled = true;
                Tab(documents[1]).IsEnabled = false;
                Check.False(peer.IsEnabled());
                Check.Throws<InvalidOperationException>(peer.Invoke);
            });
            tests.Test("tab: command CanExecute refusal does not activate", async () =>
            {
                await Reset();
                var item = dock.GetLayoutItemFromModel(documents[1]);
                var calls = 0;
                item.ActivateCommand = new Command(() => calls++, () => false);
                Check.Throws<InvalidOperationException>(() => TabPeer(documents[1]).Select());
                Check.Equal(0, calls);
                Check.False(documents[1].IsActive);
            });
            foreach (var change in new[]
            {
                "root",
                "command",
                "disable",
                "transfer"
            }

            )
                tests.Test("tab: CanExecute callback invalidates pending activation: " + change, async () =>
                {
                    await Reset();
                    var target = documents[1];
                    var peer = TabPeer(target);
                    var calls = 0;
                    var item = dock.GetLayoutItemFromModel(target);
                    using var other = new DockingManager
                    {
                        Layout = new()
                        {
                            RootPanel = new(new LayoutDocumentPane())
                        }
                    };
                    item.ActivateCommand = new Command(() => calls++, () =>
                    {
                        switch (change)
                        {
                            case "root":
                                dock.Layout = new();
                                break;
                            case "command":
                                item.ActivateCommand = new Command(() => calls++);
                                break;
                            case "disable":
                                target.IsEnabled = false;
                                break;
                            case "transfer":
                                second.Children.Remove(target);
                                ((LayoutDocumentPane)other.Layout.RootPanel.Children[0]).Children.Add(target);
                                break;
                        }

                        return true;
                    });
                    Check.Throws<InvalidOperationException>(peer.Select);
                    Check.Equal(0, calls);
                });
            tests.Test("tab: old provider rejects a closed or removed model", async () =>
            {
                await Reset();
                var peer = TabPeer(documents[1]);
                second.Children.Remove(documents[1]);
                Check.False(peer.IsEnabled());
                Check.False(peer.IsSelected);
                Check.True(peer.SelectionContainer == null);
                Check.Throws<InvalidOperationException>(peer.Select);
                await Settle();
                Check.Throws<InvalidOperationException>(peer.Invoke);
            });
            tests.Test("tab: old pane provider reports no selection after root replacement", async () =>
            {
                await Reset();
                var peer = PanePeer();
                Check.Equal(1, peer.GetSelection().Length);
                dock.Layout = new();
                Check.Equal(0, peer.GetSelection().Length);
                Check.False(peer.IsSelectionRequired);
            });
            tests.Test("tab: focus provider reaches actual label without changing the selected document", async () =>
            {
                await Reset();
                var peer = TabPeer(documents[1]);
                var active = dock.Layout.ActiveContent;
                peer.SetFocus();
                await Settle();
                Check.True(peer.HasKeyboardFocus());
                Check.Same(active, dock.Layout.ActiveContent);
            });
            tests.Test("tab: live title and description update without replacing the peer", async () =>
            {
                await Reset();
                var peer = TabPeer(documents[1]);
                documents[1].Title = "Renamed";
                documents[1].Description = "New help";
                await Settle();
                Check.Same(peer, TabPeer(documents[1]));
                Check.Equal("Renamed", peer.GetName());
                Check.Equal("New help", peer.GetHelpText());
            });
            tests.Test("focus: same-element keyboard and pointer transitions repaint the cue", async () =>
            {
                await Reset();
                range.SetFocus();
                await Settle();
                var cue = splitter.FindVisualChildren<Border>().Single(b => b.Name == "PART_SplitterKeyboardFocus");
                Check.Equal(Visibility.Visible, cue.Visibility);
                Check.True(splitter.Focus(FocusState.Pointer));
                await Task.Delay(30);
                Check.Equal(FocusState.Pointer, splitter.FocusState);
                Check.Equal(Visibility.Collapsed, cue.Visibility);
                Check.True(splitter.Focus(FocusState.Keyboard));
                await Task.Delay(30);
                Check.Equal(FocusState.Keyboard, splitter.FocusState);
                Check.Equal(Visibility.Visible, cue.Visibility);
            });
            foreach (var variant in new[]
            {
                "light",
                "dark",
                "rtl",
                "vertical"
            }

            )
                tests.Test("focus: visible splitter cue preserves stock dimensions: " + variant, async () =>
                {
                    await Reset(variant != "vertical", variant == "rtl");
                    if (variant == "dark")
                    {
                        dock.RequestedTheme = ElementTheme.Dark;
                        dock.Theme = new FluentTheme(ElementTheme.Dark);
                        await Settle();
                    }

                    var before = (splitter.ActualWidth, splitter.ActualHeight);
                    range.SetFocus();
                    await Settle();
                    var cue = splitter.FindVisualChildren<Border>().Single(b => b.Name == "PART_SplitterKeyboardFocus");
                    Check.Equal(Visibility.Visible, cue.Visibility);
                    Check.True(cue.ActualWidth > 0 && cue.ActualHeight > 0);
                    Check.False(cue.IsHitTestVisible);
                    Check.Equal(before, (splitter.ActualWidth, splitter.ActualHeight));
                    await VisualCapture.Save(dock, Path.Combine(output, "visuals", "accessibility-focus-" + variant + ".png"));
                    TabPeer(documents[0]).SetFocus();
                    await Settle();
                    Check.Equal(Visibility.Collapsed, cue.Visibility);
                });
            tests.Test("sample: numeric range actions and reset use the actual current provider", async () =>
            {
                using var page = new GalleryPage
                {
                    Width = 1000,
                    Height = 680
                };
                window.Content = page;
                try
                {
                    await Wait(() => page.IsLoaded);
                    page.ExecuteSampleCommand("splitters");
                    await Wait(() => page.FindVisualChildren<DockingManager>().Count() == 2);
                    var inner = page.FindVisualChildren<DockingManager>().Single(manager => !ReferenceEquals(manager, page.Dock));
                    inner.UpdateLayout();
                    await Wait(() => inner.FindVisualChildren<LayoutGridResizerControl>().Any(v => v.IsLoaded));
                    var control = inner.FindVisualChildren<LayoutGridResizerControl>().Single();
                    var peer = (LayoutGridResizerAutomationPeer)FrameworkElementAutomationPeer.CreatePeerForElement(control);
                    var number = page.FindVisualChildren<TextBox>().Single(box => AutomationProperties.GetName(box) == "Leading pane size in DIPs");
                    var value = (peer.Minimum + peer.Maximum) / 2;
                    number.Text = value.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
                    Invoke("Apply size");
                    await Wait(() => Math.Abs(peer.Value - value) <= .6);
                    Invoke("Minimum");
                    await Wait(() => Math.Abs(peer.Value - peer.Minimum) <= .6);
                    Invoke("Maximum");
                    await Wait(() => Math.Abs(peer.Value - peer.Maximum) <= .6);
                    Invoke("Reset 1* / 2*");
                    await Wait(() => peer.IsReadOnly && inner.FindVisualChildren<LayoutGridResizerControl>().Any(v => !ReferenceEquals(v, control) && v.IsLoaded));
                    Invoke("Read range");
                    var current = (LayoutGridResizerAutomationPeer)FrameworkElementAutomationPeer.CreatePeerForElement(inner.FindVisualChildren<LayoutGridResizerControl>().Single());
                    Check.False(current.IsReadOnly);
                    Check.True(!ReferenceEquals(current, peer));
                    Check.Near(current.Value, double.Parse(number.Text, System.Globalization.CultureInfo.InvariantCulture));
                    Invoke("Focus divider");
                    await Task.Delay(30);
                    Check.True(current.HasKeyboardFocus());
                    await VisualCapture.Save(page, Path.Combine(output, "visuals", "accessibility-splitter-sample.png"));
                    void Invoke(string title)
                    {
                        var button = page.FindVisualChildren<Button>().Single(b => b.Content is string text && text == title);
                        var buttonPeer = FrameworkElementAutomationPeer.CreatePeerForElement(button);
                        ((IInvokeProvider)buttonPeer!.GetPattern(PatternInterface.Invoke)!).Invoke();
                    }
                }
                finally
                {
                    window.Content = scene;
                    await Wait(() => dock.IsLoaded);
                }
            });
            if (OperatingSystem.IsLinux() && Environment.GetEnvironmentVariable("UNODOCK_NATIVE_INPUT_TESTS") == "1")
            {
                tests.Test("XTEST: Tab traversal reaches the divider and shows keyboard focus", async () =>
                {
                    await Reset();
                    using var input = new X11TestInput();
                    var editor = (TextBox)((LayoutAnchorable)first.Children[0]).Content!;
                    input.MoveTo(editor, new(20, 20));
                    Check.True(editor.Focus(FocusState.Keyboard));
                    await Settle();
                    input.KeyPress(0xff09);
                    await Wait(() => splitter.FocusState == FocusState.Keyboard);
                    var cue = splitter.FindVisualChildren<Border>().Single(b => b.Name == "PART_SplitterKeyboardFocus");
                    Check.Equal(Visibility.Visible, cue.Visibility);
                });
                foreach (var rtl in new[]
                {
                    false,
                    true
                }

                )
                    tests.Test("XTEST: focused splitter receives Home, PageDown and physical arrows, RTL=" + rtl, async () =>
                    {
                        await Reset(true, rtl);
                        window.Activate();
                        using var input = new X11TestInput();
                        // Place the server pointer on this dedicated native window before
                        // keyboard input; a headless X11 session has no EWMH window manager.
                        input.MoveTo(splitter, new(splitter.ActualWidth / 2, splitter.ActualHeight / 2));
                        range.SetFocus();
                        await Settle();
                        input.KeyPress(0xff50);
                        await Wait(() => Math.Abs(range.Value - 100) <= .6);
                        input.KeyPress(0xff56);
                        await Wait(() => Math.Abs(range.Value - 150) <= .6);
                        input.KeyPress(0xff53);
                        await Wait(() => Math.Abs(range.Value - (rtl ? 140 : 160)) <= .6);
                    });
            }

            return await tests.Run(output, "accessibility-quality");
        }
        finally
        {
            window.Content = null;
            window.Close();
        }

        async Task Reset(bool horizontal = true, bool rtl = false)
        {
            dock.Theme = new GenericTheme();
            dock.RequestedTheme = ElementTheme.Light;
            dock.FlowDirection = rtl ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
            dock.Width = 1000;
            dock.Height = 640;
            first = new(new LayoutAnchorable { Title = "Tools", ContentId = "tools", Content = new TextBox { Text = "Focus the divider to resize without a mouse.", Padding = new(12) } })
            {
                DockWidth = new(1, GridUnitType.Star),
                DockHeight = new(1, GridUnitType.Star),
                DockMinWidth = 100,
                DockMinHeight = 100
            };
            documents = Enumerable.Range(0, 3).Select(i => new LayoutDocument { Title = "Document " + i, ContentId = "document-" + i, Description = "Description " + i, Content = new TextBox { Text = "Accessible document " + i, Padding = new(12) } }).ToArray();
            second = new()
            {
                DockWidth = new(2, GridUnitType.Star),
                DockHeight = new(2, GridUnitType.Star),
                DockMinWidth = 100,
                DockMinHeight = 100
            };
            foreach (var document in documents)
                second.Children.Add(document);
            model = new(first)
            {
                Orientation = horizontal ? Orientation.Horizontal : Orientation.Vertical
            };
            model.Children.Add(second);
            dock.Layout = new()
            {
                RootPanel = model
            };
            documents[0].IsActive = true;
            window.Activate();
            await Settle();
            grid = dock.FindVisualChildren<LayoutPanelControl>().Single();
            splitter = grid.FindVisualChildren<LayoutGridResizerControl>().Single();
            range = (LayoutGridResizerAutomationPeer)FrameworkElementAutomationPeer.CreatePeerForElement(splitter);
        }

        async Task Settle()
        {
            dock.Refresh();
            dock.UpdateLayout();
            await Task.Delay(45);
            dock.UpdateLayout();
        }

        bool Horizontal() => model.Orientation == Orientation.Horizontal;
        double Pixels() => Horizontal() ? grid.ColumnDefinitions[0].ActualWidth : grid.RowDefinitions[0].ActualHeight;
        double Total() => Pixels() + (Horizontal() ? grid.ColumnDefinitions[2].ActualWidth : grid.RowDefinitions[2].ActualHeight);
        bool Ghosts() => grid.FindVisualChildren<Border>().Any(b => b.Name == "PART_SplitterPreview");
        bool Key(VirtualKey key) => (bool)Call(splitter, "ResizeFromKey", key)!;
        LayoutTabItemBase Tab(LayoutDocument document) => dock.FindVisualChildren<LayoutDocumentTabItem>().Single(t => ReferenceEquals(t.Model, document));
        LayoutTabAutomationPeer TabPeer(LayoutDocument document) => (LayoutTabAutomationPeer)FrameworkElementAutomationPeer.CreatePeerForElement(Tab(document));
        LayoutPaneAutomationPeer PanePeer() => (LayoutPaneAutomationPeer)FrameworkElementAutomationPeer.CreatePeerForElement(dock.FindVisualChildren<LayoutDocumentPaneControl>().Single());
    }

    private static async Task Wait(Func<bool> condition)
    {
        for (var i = 0; i < 100 && !condition(); i++)
            await Task.Delay(20);
        Check.True(condition(), "Provider/input condition did not converge.");
    }

    private static object? Call(object target, string name, params object[] arguments)
    {
        try
        {
            return target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(target, arguments);
        }
        catch (TargetInvocationException e)when (e.InnerException != null)
        {
            ExceptionDispatchInfo.Capture(e.InnerException).Throw();
            throw;
        }
    }

    private sealed class Command(Action execute, Func<bool>? canExecute = null) : ICommand
    {
        public event EventHandler? CanExecuteChanged
        {
            add
            {
            }

            remove
            {
            }
        }

        public bool CanExecute(object? parameter) => canExecute?.Invoke() ?? true;
        public void Execute(object? parameter) => execute();
    }
}
