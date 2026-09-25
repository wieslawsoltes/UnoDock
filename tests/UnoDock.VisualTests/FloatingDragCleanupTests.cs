using System.Collections;
using System.Reflection;
using System.Runtime.ExceptionServices;
using UnoDock.Controls;
using UnoDock.Layout;
using UnoDock.Themes;
using Windows.Foundation;

namespace UnoDock.Testing;
/// <summary>Actual-host callback fault injection at the terminal drag boundary.
/// Existing XTEST/SendInput suites cover physical gestures; this suite isolates
/// application DP failures and inspects capture/session/clock teardown directly.</summary>
internal static class FloatingDragCleanupTests
{
    internal static async Task<int> Run(string output)
    {
        var tests = new TestRunner();
        foreach (var native in new[]
        {
            false,
            true
        }

        )
            foreach (var tools in new[]
            {
                false,
                true
            }

            )
                foreach (var commit in new[]
                {
                    false,
                    true
                }

                )
                    foreach (var fault in new[]
                    {
                        "guides",
                        "caption",
                        "both"
                    }

                    )
                        tests.Test($"cleanup/{(native ? "native" : "surface")}/{(tools ? "tools" : "document")}/{(commit ? "release" : "cancel")}/{fault}", async () =>
                        {
                            using var f = new Fixture(native, tools);
                            await f.Show();
                            var editors = f.Source.Select(c => c.Content).ToArray();
                            var generation = f.BeginAndPaint();
                            var expected = new List<Exception>();
                            var observed = new List<Exception>();
                            using var callbacks = new Callbacks();
                            if (fault is "guides" or "both")
                            {
                                Watch(f.Overlay, UIElement.VisibilityProperty, () => f.Overlay.Visibility == Visibility.Collapsed, "main guide");
                                Watch(f.SourceOverlay, UIElement.VisibilityProperty, () => f.SourceOverlay.Visibility == Visibility.Collapsed, "floating guide");
                            }

                            if (fault is "caption" or "both")
                                Watch(f.Control, LayoutFloatingWindowControl.IsDraggingProperty, () => !f.Control.IsDragging, "caption state");
                            var error = Observe(() => f.End(generation, commit));
                            Check.True(error != null, "A throwing application observer was silently swallowed.");
                            var leaves = error is AggregateException aggregate ? aggregate.Flatten().InnerExceptions.ToArray() : new[]
                            {
                                error!
                            };
                            Check.Equal(expected.Count, observed.Count);
                            Check.Equal(expected.Count, leaves.Length);
                            for (var i = 0; i < expected.Count; i++)
                            {
                                Check.Same(expected[i], observed[i]);
                                Check.Same(expected[i], leaves[i]);
                            }

                            if (expected.Count == 1)
                                Check.Same(expected[0], error);
                            f.AssertIdle();
                            Check.True(f.Source.All(c => ReferenceEquals(c.FindParent<LayoutFloatingWindow>(), f.Floating)), "Teardown failure authorized a drop.");
                            for (var i = 0; i < editors.Length; i++)
                                Check.Same(editors[i], f.Source[i].Content);
                            Check.False((bool)Call(f.Surface, "CompleteFloatingDrag", f.Control, generation, f.Center(), false)!);
                            // A retained old release is inert, and a new gesture is usable.
                            var next = f.BeginAndPaint();
                            Check.True(next > generation);
                            f.End(next, false);
                            f.AssertIdle();
                            void Watch(DependencyObject owner, DependencyProperty property, Func<bool> terminal, string label)
                            {
                                var failure = new InvalidOperationException("Injected " + label + " failure");
                                expected.Add(failure);
                                var delivered = false;
                                callbacks.Add(owner, property, () =>
                                {
                                    if (delivered || !terminal())
                                        return;
                                    delivered = true;
                                    observed.Add(failure);
                                    throw failure;
                                });
                            }
                        });
        tests.Test("overlay cleanup: a Visibility observer failure still retires guide visuals and preview", async () =>
        {
            using var f = new Fixture(false, false);
            await f.Show();
            f.BeginAndPaint();
            var marker = new InvalidOperationException("Visibility callback failure");
            using var callbacks = new Callbacks();
            var delivered = false;
            callbacks.Add(f.Overlay, UIElement.VisibilityProperty, () =>
            {
                if (delivered || f.Overlay.Visibility != Visibility.Collapsed)
                    return;
                delivered = true;
                throw marker;
            });
            Check.Same(marker, Observe(f.Overlay.Hide));
            AssertHidden(f.Overlay);
            Check.Equal(2, Get<Canvas>(f.Overlay, "_canvas").Children.Count);
        });
        foreach (var show in new[]
        {
            false,
            true
        }

        )
            tests.Test("overlay cleanup: a callback's replacement survives " + (show ? "ShowPreview" : "Hide"), async () =>
            {
                using var f = new Fixture(false, false);
                await f.Show();
                f.BeginAndPaint();
                var original = f.Overlay.CurrentPlan;
                var replacement = DockDropPlan.Create(f.Source[0], f.Documents, DropTargetType.DocumentPaneDockRight, new(0, 0, 800, 600))!;
                Check.True(replacement.CanExecute);
                var brush = new SolidColorBrush(Microsoft.UI.Colors.CornflowerBlue);
                using var callbacks = new Callbacks();
                var reopened = false;
                callbacks.Add(f.Overlay, UIElement.VisibilityProperty, () =>
                {
                    if (reopened || f.Overlay.Visibility != Visibility.Collapsed)
                        return;
                    reopened = true;
                    f.Overlay.ShowPreview(replacement, brush);
                });
                if (show)
                    f.Overlay.ShowPreview(original, brush);
                else
                    f.Overlay.Hide();
                Check.True(reopened);
                Check.True(f.Overlay.IsOpen);
                Check.Same(replacement, f.Overlay.CurrentPlan);
                Check.Equal(Visibility.Visible, Get<Border>(f.Overlay, "_fill").Visibility);
                Check.Equal(Visibility.Visible, Get<Border>(f.Overlay, "_preview").Visibility);
                Check.Equal(2, Get<Canvas>(f.Overlay, "_canvas").Children.Count);
                Check.Equal(0, Get<IDictionary>(f.Overlay, "_guideViews").Count);
            });
        return await tests.Run(output, "floating-drag-cleanup");
    }

    private sealed class Fixture : IDisposable
    {
        internal readonly DockingManager Manager;
        internal readonly LayoutDocumentPane Documents = new(new LayoutDocument { Title = "Destination", ContentId = "target", Content = new TextBox { Text = "Destination buffer" } });
        internal readonly LayoutFloatingWindow Floating;
        internal readonly LayoutContent[] Source;
        internal LayoutFloatingWindowControl Control = null!;
        internal OverlayWindow Overlay => Get<OverlayWindow>(Surface, "_overlay");
        internal OverlayWindow SourceOverlay => Get<OverlayWindow>(Control, "_dropOverlay");
        internal FrameworkElement Surface => (FrameworkElement)typeof(DockingManager).GetProperty("Surface", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(Manager)!;

        private readonly Window _window;
        private readonly IDisposable _registration;
        private readonly bool _native;
        internal Fixture(bool native, bool tools)
        {
            _native = native;
            Manager = new()
            {
                Width = 1000,
                Height = 640,
                FloatingWindowMode = native ? FloatingWindowMode.Native : FloatingWindowMode.InSurface,
                Theme = new FluentTheme(ElementTheme.Light)
            };
            Manager.Layout = new()
            {
                RootPanel = new LayoutPanel(Documents)
            };
            if (tools)
            {
                Source = Enumerable.Range(0, 2).Select(i => (LayoutContent)new LayoutAnchorable { Title = "Tool " + i, ContentId = "tool" + i, CanDockAsTabbedDocument = true, Content = new TextBox { Text = "Retained tool draft " + i }, FloatingLeft = 150, FloatingTop = 140, FloatingWidth = 420, FloatingHeight = 300 }).ToArray();
                var pane = new LayoutAnchorablePane((LayoutAnchorable)Source[0]);
                pane.Children.Add((LayoutAnchorable)Source[1]);
                var group = new LayoutAnchorablePaneGroup();
                group.Children.Add(pane);
                Floating = new LayoutAnchorableFloatingWindow
                {
                    RootPanel = group
                };
            }
            else
            {
                var document = new LayoutDocument
                {
                    Title = "Floating document",
                    ContentId = "floating",
                    Content = new TextBox
                    {
                        Text = "Retained document draft"
                    },
                    FloatingLeft = 150,
                    FloatingTop = 140,
                    FloatingWidth = 420,
                    FloatingHeight = 300
                };
                Source = [document];
                Floating = new LayoutDocumentFloatingWindow
                {
                    RootDocument = document
                };
            }

            Manager.Layout.FloatingWindows.Add(Floating);
            Source[0].IsActive = true;
            _window = new()
            {
                Content = Manager,
                Title = "UnoDock drag cleanup acceptance"
            };
            _registration = Microsoft.Windows.Shell.SystemCommands.RegisterWindow(_window);
            _window.AppWindow.Resize(new()
            {
                Width = 1100,
                Height = 780
            });
            _window.Activate();
        }

        internal async Task Show()
        {
            await Wait(() => Manager.IsLoaded && Manager.ActualWidth > 0);
            Manager.Refresh();
            await Wait(() => Manager.FloatingWindows.Count() == 1);
            Control = Manager.FloatingWindows.Single();
            await Wait(() => Control.IsLoaded && Control.ActualWidth > 0 && (!_native || Control.NativeWindow != null));
            Manager.UpdateLayout();
            Control.UpdateLayout();
            await Task.Delay(40);
        }

        internal Point Center()
        {
            var view = (FrameworkElement)Call(Surface, "GetView", Documents)!;
            return view.TransformToVisual(Surface).TransformPoint(new(view.ActualWidth / 2, view.ActualHeight / 2));
        }

        internal long BeginAndPaint()
        {
            var caption = Call(Control, "BeginCaptionDrag", Center(), null, true);
            Check.True(caption != null, "The fixture could not acquire a caption gesture.");
            var generation = Get<long>(caption!, "Generation");
            if (_native)
                Call(Control, "StartDragClock");
            Check.True((bool)Call(Surface, "UpdateFloatingDrag", Control, generation, Center(), false)!);
            // Give both real overlay instances an eligible presentation even if
            // native stacking makes one compass outside its host's clipped area.
            var plan = DockDropPlan.Create(Source[0], Documents, DropTargetType.DocumentPaneDockInside, new(0, 0, 800, 600))!;
            Check.True(plan.CanExecute);
            var brush = new SolidColorBrush(Microsoft.UI.Colors.CornflowerBlue);
            if (!Overlay.IsOpen)
                Overlay.ShowPreview(plan, brush);
            if (!SourceOverlay.IsOpen)
                SourceOverlay.ShowPreview(plan, brush);
            Check.True(Control.IsDragging);
            Check.True(Overlay.IsOpen);
            Check.True(SourceOverlay.IsOpen);
            return generation;
        }

        internal void End(long generation, bool commit)
        {
            if (commit)
                Call(Surface, "CompleteFloatingDrag", Control, generation, Center(), false);
            else
                Call(Surface, "CancelDrag");
        }

        internal void AssertIdle()
        {
            Check.False(Control.IsDragging);
            Check.True(Get<object?>(Control, "_captionDrag") == null);
            Check.True(Get<object?>(Control, "_dragClock") == null);
            Check.True(Get<object?>(Surface, "_floatingDrag") == null);
            Check.True(Get<object?>(Surface, "_dragContent") == null);
            Check.False(Get<DispatcherTimer>(Surface, "_dragScrollTimer").IsEnabled);
            Check.Equal(0, Get<Border>(Control, "_dragHandle").PointerCaptures?.Count ?? 0);
            AssertHidden(Overlay);
            AssertHidden(SourceOverlay);
        }

        public void Dispose()
        {
            try
            {
                Manager.Dispose();
            }
            finally
            {
                _window.Content = null;
                _window.Close();
                _registration.Dispose();
            }
        }
    }

    private static void AssertHidden(OverlayWindow overlay)
    {
        Check.False(overlay.IsOpen);
        Check.True(overlay.CurrentPlan == null);
        Check.Equal(0, overlay.DisplayedGuides.Count);
        Check.Equal(0, Get<IDictionary>(overlay, "_guideViews").Count);
        Check.Equal(0, Get<IDictionary>(overlay, "_plates").Count);
        Check.Equal(Visibility.Collapsed, Get<Border>(overlay, "_fill").Visibility);
        Check.Equal(Visibility.Collapsed, Get<Border>(overlay, "_preview").Visibility);
    }

    private sealed class Callbacks : IDisposable
    {
        private readonly List<(DependencyObject Owner, DependencyProperty Property, long Token)> _tokens = [];
        internal void Add(DependencyObject owner, DependencyProperty property, Action callback) => _tokens.Add((owner, property, owner.RegisterPropertyChangedCallback(property, (_, _) => callback())));
        public void Dispose()
        {
            foreach (var entry in _tokens)
                entry.Owner.UnregisterPropertyChangedCallback(entry.Property, entry.Token);
            _tokens.Clear();
        }
    }

    private static Exception? Observe(Action action)
    {
        try
        {
            action();
            return null;
        }
        catch (Exception error)
        {
            return error;
        }
    }

    private static T Get<T>(object owner, string name)
    {
        for (var type = owner.GetType(); type != null; type = type.BaseType)
            if (type.GetField(name, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly) is { } field)
                return (T)field.GetValue(owner)!;
        throw new MissingFieldException(name);
    }

    private static object? Call(object owner, string name, params object?[] args)
    {
        for (var type = owner.GetType(); type != null; type = type.BaseType)
        {
            var method = type.GetMethods(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly).SingleOrDefault(m => m.Name == name && m.GetParameters().Length == args.Length);
            if (method == null)
                continue;
            try
            {
                return method.Invoke(owner, args);
            }
            catch (TargetInvocationException error) when (error.InnerException != null)
            {
                ExceptionDispatchInfo.Capture(error.InnerException).Throw();
                throw;
            }
        }

        throw new MissingMethodException(name);
    }

    private static async Task Wait(Func<bool> ready)
    {
        for (var i = 0; i < 100 && !ready(); i++)
            await Task.Delay(20);
        Check.True(ready(), "Drag cleanup fixture did not become ready.");
    }
}
