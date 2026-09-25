using Microsoft.UI.Xaml.Automation;
using UnoDock.Controls;
using UnoDock.Core;
using UnoDock.Layout;
using UnoDock.Themes;
using Windows.Foundation;

namespace UnoDock.Testing;
internal static partial class FloatingChromeTests
{
    internal sealed class Fixture : IDisposable
    {
        internal readonly DockingManager Manager = new()
        {
            Width = 920,
            Height = 580,
            FloatingWindowMode = FloatingWindowMode.Native,
            Theme = new FluentTheme()
        };
        internal readonly Window Owner;
        internal readonly LayoutContent[] Source;
        internal LayoutFloatingWindowControl Control = null!;
        internal Window Native => Control.NativeWindow ?? throw new InvalidOperationException("The native floating window is missing.");
        internal Border Caption => Control.FindVisualChildren<Border>().Single(b => b.Name == "PART_FloatingDragHandle");

        private readonly object? [] _editors;
        private readonly IDisposable _registration;
        private readonly List<Exception> _errors = [];
        internal Fixture(bool tools)
        {
            var documents = new LayoutDocumentPane(new LayoutDocument { Title = "Destination", Content = new TextBox { Text = "Destination" } });
            Manager.Layout = new()
            {
                RootPanel = new LayoutPanel(documents)
            };
            if (tools)
            {
                Source = Enumerable.Range(0, 2).Select(i => (LayoutContent)new LayoutAnchorable { Title = "Tool " + i, ContentId = "tool-" + i, Content = new TextBox { Text = "Unsaved tool draft " + i }, CanDockAsTabbedDocument = true, FloatingLeft = 160, FloatingTop = 120, FloatingWidth = 440, FloatingHeight = 320 }).ToArray();
                var pane = new LayoutAnchorablePane((LayoutAnchorable)Source[0]);
                pane.Children.Add((LayoutAnchorable)Source[1]);
                var group = new LayoutAnchorablePaneGroup();
                group.Children.Add(pane);
                Manager.Layout.FloatingWindows.Add(new LayoutAnchorableFloatingWindow { RootPanel = group });
            }
            else
            {
                var source = new LayoutDocument
                {
                    Title = "Document",
                    ContentId = "document",
                    Content = new TextBox
                    {
                        Text = "Unsaved document draft"
                    },
                    FloatingLeft = 160,
                    FloatingTop = 120,
                    FloatingWidth = 440,
                    FloatingHeight = 320
                };
                Source = [source];
                Manager.Layout.FloatingWindows.Add(new LayoutDocumentFloatingWindow { RootDocument = source });
            }

            Source[0].IsActive = true;
            _editors = Source.Select(s => s.Content).ToArray();
            Owner = new Window
            {
                Content = Manager,
                Title = "Custom floating chrome acceptance"
            };
            _registration = Microsoft.Windows.Shell.SystemCommands.RegisterWindow(Owner);
            Owner.AppWindow.Move(new() { X = 20, Y = 20 });
            Owner.AppWindow.Resize(new() { Width = 1000, Height = 700 });
            Owner.Activate();
        }

        internal async Task Show()
        {
            await Wait(() => Manager.IsLoaded);
            Manager.Refresh();
            await Wait(() => Manager.FloatingWindows.Count() == 1);
            Control = Manager.FloatingWindows.Single();
            Control.MessageFilterFailed += (_, e) => _errors.Add(e);
            await Wait(() => Control.IsLoaded && Control.NativeWindow != null && Control.ActualWidth > 0);
            await Task.Delay(100);
            Check.True(Control.IsCustomTitleBar, string.Join("\n", _errors));
        }

        internal FrameworkElement Grip(ChromeHit edge) => Control.FindVisualChildren<FrameworkElement>().Single(b => b.Name == "PART_FloatingResize" + edge);
        internal Button Button(string action) => Control.FindVisualChildren<Button>().Single(b => AutomationProperties.GetAutomationId(b) == "FloatingWindow" + action);
        internal void AssertEditors()
        {
            for (var i = 0; i < Source.Length; i++)
                Check.Same(_editors[i], Source[i].Content);
            Check.Equal(0, _errors.Count);
        }

        public void Dispose()
        {
            try
            {
                Manager.Dispose();
            }
            finally
            {
                Owner.Content = null;
                Owner.Close();
                _registration.Dispose();
            }
        }
    }

    private sealed class PointerInput : IDisposable
    {
        private readonly X11TestInput? _x11;
        private readonly WindowsFloatingInputTests.NativeInput? _windows;
        internal PointerInput()
        {
            if (OperatingSystem.IsLinux())
                _x11 = new();
            else
                _windows = new();
        }

        internal void MoveTo(FrameworkElement e, Point p)
        {
            if (_x11 != null)
                _x11.MoveTo(e, p);
            else
                _windows!.MoveTo(e, p);
        }

        private Point ScreenPoint(FrameworkElement e, Point p) => _x11?.ScreenPoint(e, p) ?? _windows!.ScreenPoint(e, p);
        internal Point OwnerPoint(FrameworkElement source, Point p, FrameworkElement owner)
        {
            var screen = ScreenPoint(source, p);
            var origin = ScreenPoint(owner, new(0, 0));
            return new((screen.X - origin.X) / owner.XamlRoot!.RasterizationScale, (screen.Y - origin.Y) / owner.XamlRoot.RasterizationScale);
        }

        internal void Press()
        {
            if (_x11 != null)
                _x11.Press();
            else
                _windows!.Press();
        }

        internal void Release()
        {
            if (_x11 != null)
                _x11.Release();
            else
                _windows!.Release();
        }

        internal void EscapeDown()
        {
            if (_x11 != null)
                _x11.KeyDown(0xff1b);
            else
                _windows!.KeyDown(0x1b);
        }

        internal void EscapeUp()
        {
            if (_x11 != null)
                _x11.KeyUp(0xff1b);
            else
                _windows!.KeyUp(0x1b);
        }

        internal async Task Click(FrameworkElement e)
        {
            await PressOn(e);
            await Task.Delay(50);
            Release();
        }

        internal async Task PressOn(FrameworkElement e, Point? point = null)
        {
            // Presenter state can precede both WM configure and XAML arrange.
            // Wait for an actual full-size custom client and stable button screen
            // geometry BEFORE injecting exactly one click. Never retry input.
            var window = Uno.UI.ApplicationHelper.Windows.Single(w => ReferenceEquals(w.Content?.XamlRoot, e.XamlRoot));
            var client = (FrameworkElement)window.Content!;
            Point? previous = null;
            DockRect? previousBounds = null;
            var stable = 0;
            for (var attempt = 0; attempt < 160; attempt++)
            {
                var bounds = FloatingChromeProbe.Bounds(window);
                var scale = client.XamlRoot!.RasterizationScale;
                var local = point ?? new Point(e.ActualWidth / 2, e.ActualHeight / 2);
                var screen = ScreenPoint(e, local);
                var arranged = e.IsLoaded && e.ActualWidth > 0 && e.ActualHeight > 0 && Math.Abs(client.ActualWidth * scale - bounds.Width) <= 1 && Math.Abs(client.ActualHeight * scale - bounds.Height) <= 1;
                stable = arranged && previous == screen && previousBounds == bounds ? stable + 1 : 0;
                previous = screen;
                previousBounds = bounds;
                if (stable >= 3)
                {
                    MoveTo(e, local);
                    await Task.Delay(25);
                    if (ScreenPoint(e, local) == screen && FloatingChromeProbe.Bounds(window) == bounds)
                    {
                        Press();
                        return;
                    }

                    stable = 0;
                }

                await Task.Delay(25);
            }

            throw new InvalidOperationException($"Custom caption input geometry did not settle: client={client.ActualWidth}x{client.ActualHeight}, native={previousBounds}, button={AutomationProperties.GetAutomationId(e)}.");
        }

        public void Dispose()
        {
            _x11?.Dispose();
            _windows?.Dispose();
        }
    }
}
