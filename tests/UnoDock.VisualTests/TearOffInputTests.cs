using UnoDock.Controls;
using UnoDock.Layout;
using UnoDock.Themes;
using Windows.Foundation;

namespace UnoDock.Testing;
/// <summary>Opt-in physical-input acceptance for continuous tear-off on a
/// dedicated desktop (Win32 SendInput or X11 XTEST). Every gesture is real input;
/// no private drag entry point replaces the pointer.</summary>
internal static class TearOffInputTests
{
    internal static async Task<int> Run(string output)
    {
        if (Environment.GetEnvironmentVariable("UNODOCK_NATIVE_INPUT_TESTS") != "1" || !(OperatingSystem.IsWindows() || OperatingSystem.IsLinux()))
            return 0;
        var tests = new TestRunner();
        tests.Test("tear-off: a document tab leaves its strip and its window follows the pointer", async () =>
        {
            using var f = new Fixture();
            await f.Show();
            using var input = Input.Create();
            var editor = f.Second.Content;
            input.MoveTo(f.Tab(f.Second), new(20, 8));
            await Task.Delay(60);
            input.Press();
            await Task.Delay(60);
            var start = input.ScreenPoint(f.Tab(f.Second), new(20, 8));
            await input.Glide(start, new(start.X, start.Y + 90));
            await Wait(() => f.Second.IsFloating && f.Control(f.Second) is { IsDragging: true, NativeWindow: not null });
            var control = f.Control(f.Second)!;
            var before = control.NativeWindow!.AppWindow.Position;
            await input.Glide(new(start.X, start.Y + 90), new(start.X + 160, start.Y + 190));
            await Wait(() => control.NativeWindow!.AppWindow.Position.X > before.X + 60 && control.NativeWindow!.AppWindow.Position.Y > before.Y + 40);
            input.Release();
            await Wait(() => !control.IsDragging);
            Check.True(f.Second.IsFloating, "Releasing over empty desktop keeps the torn-off document floating.");
            Check.Same(editor, f.Second.Content);
            Check.True(ReferenceEquals(f.First.Parent, f.Source), "The remaining document stays in its pane.");
        });
        tests.Test("tear-off: releasing over the target pane's center guide docks the torn tab", async () =>
        {
            using var f = new Fixture();
            await f.Show();
            using var input = Input.Create();
            var editor = f.Second.Content;
            var start = input.ScreenPoint(f.Tab(f.Second), new(20, 8));
            input.Move(start);
            await Task.Delay(60);
            input.Press();
            await Task.Delay(60);
            await input.Glide(start, new(start.X, start.Y + 90));
            await Wait(() => f.Control(f.Second) is { IsDragging: true });
            var target = input.ScreenPoint(f.TargetView, new(f.TargetView.ActualWidth / 2, f.TargetView.ActualHeight / 2));
            await input.Glide(new(start.X, start.Y + 90), target);
            await Task.Delay(250);
            await Hold("guides");
            input.Release();
            await Wait(() => ReferenceEquals(f.Second.Parent, f.Target));
            Check.Same(editor, f.Second.Content);
            Check.Equal(0, f.Manager.Layout.FloatingWindows.Count);
        });
        tests.Test("tear-off: dragging a tool pane title floats the whole pane", async () =>
        {
            using var f = new Fixture();
            await f.Show();
            using var input = Input.Create();
            var title = f.ToolTitle();
            var start = input.ScreenPoint(title, new(30, title.ActualHeight / 2));
            input.Move(start);
            await Task.Delay(60);
            input.Press();
            await Task.Delay(60);
            await input.Glide(start, new(start.X + 220, start.Y + 140));
            await Wait(() => f.ToolA.IsFloating && f.ToolB.IsFloating && f.Control(f.ToolA) is { IsDragging: true });
            Check.True(ReferenceEquals(f.ToolA.Parent, f.ToolB.Parent), "Both tools move together in one pane.");
            input.Release();
            await Wait(() => f.Control(f.ToolA) is { IsDragging: false });
            Check.Equal(1, f.Manager.Layout.FloatingWindows.Count);
        });
        tests.Test("tear-off: Escape ends the move and keeps the content floating", async () =>
        {
            using var f = new Fixture();
            await f.Show();
            using var input = Input.Create();
            var start = input.ScreenPoint(f.Tab(f.Second), new(20, 8));
            input.Move(start);
            await Task.Delay(60);
            input.Press();
            await Task.Delay(60);
            await input.Glide(start, new(start.X + 40, start.Y + 120));
            await Wait(() => f.Control(f.Second) is { IsDragging: true });
            input.EscapeDown();
            await Wait(() => f.Control(f.Second) is { IsDragging: false });
            input.EscapeUp();
            input.Release();
            await Task.Delay(120);
            Check.True(f.Second.IsFloating);
        });
        tests.Test("tear-off: ContinuousTearOff=false keeps the drag inside the workspace", async () =>
        {
            using var f = new Fixture();
            f.Manager.ContinuousTearOff = false;
            await f.Show();
            using var input = Input.Create();
            var start = input.ScreenPoint(f.Tab(f.Second), new(20, 8));
            input.Move(start);
            await Task.Delay(60);
            input.Press();
            await Task.Delay(60);
            await input.Glide(start, new(start.X, start.Y + 120));
            await Task.Delay(200);
            Check.True(!f.Second.IsFloating, "Content must not float mid-gesture when tear-off is disabled.");
            input.EscapeDown();
            await Task.Delay(120);
            input.EscapeUp();
            input.Release();
            await Task.Delay(120);
            Check.True(ReferenceEquals(f.Second.Parent, f.Source));
        });
        return await tests.Run(output, "tear-off");
    }

    /// <summary>Optional pause for external screenshots of a live gesture:
        /// UNODOCK_HOLD_MARKER names a file created while the gesture is held.</summary>
        private static async Task Hold(string name)
    {
        if (Environment.GetEnvironmentVariable("UNODOCK_HOLD_MARKER") is not { Length: > 0 } marker)
            return;
        var path = marker + "-" + name;
        await File.WriteAllTextAsync(path, name);
        await Task.Delay(4000);
        File.Delete(path);
    }

    private static async Task Wait(Func<bool> ready)
    {
        for (var i = 0; i < 160 && !ready(); i++)
            await Task.Delay(25);
        Check.True(ready(), "The native tear-off transition did not settle.");
    }

    /// <summary>Uniform wrapper over the Windows and X11 physical input helpers.</summary>
    private sealed class Input : IDisposable
    {
        private readonly WindowsFloatingInputTests.NativeInput? _windows;
        private readonly X11TestInput? _x11;
        private Input(WindowsFloatingInputTests.NativeInput? windows, X11TestInput? x11)
        {
            _windows = windows;
            _x11 = x11;
        }

        internal static Input Create() => OperatingSystem.IsWindows() ? new(new(), null) : new(null, new());
        internal Point ScreenPoint(FrameworkElement element, Point point) => _windows?.ScreenPoint(element, point) ?? _x11!.ScreenPoint(element, point);
        internal void MoveTo(FrameworkElement element, Point point) => Move(ScreenPoint(element, point));
        internal void Move(Point screen)
        {
            if (_windows != null)
                _windows.Glide(screen);
            else
                _x11!.Move(screen);
        }

        internal async Task Glide(Point from, Point to, int steps = 16)
        {
            for (var i = 1; i <= steps; i++)
            {
                Move(new(from.X + (to.X - from.X) * i / steps, from.Y + (to.Y - from.Y) * i / steps));
                await Task.Delay(30);
            }
        }

        internal void Press()
        {
            if (_windows != null)
                _windows.Press();
            else
                _x11!.Press();
        }

        internal void Release()
        {
            if (_windows != null)
                _windows.Release();
            else
                _x11!.Release();
        }

        internal void EscapeDown()
        {
            if (_windows != null)
                _windows.KeyDown(0x1b);
            else
                _x11!.KeyDown(0xff1b);
        }

        internal void EscapeUp()
        {
            if (_windows != null)
                _windows.KeyUp(0x1b);
            else
                _x11!.KeyUp(0xff1b);
        }

        public void Dispose()
        {
            _windows?.Dispose();
            _x11?.Dispose();
        }
    }

    private sealed class Fixture : IDisposable
    {
        internal readonly DockingManager Manager = new()
        {
            FloatingWindowMode = FloatingWindowMode.Native,
            Theme = new GenericTheme()
        };
        internal readonly LayoutDocument First = new()
        {
            Title = "First.cs",
            ContentId = "first",
            Content = new TextBox
            {
                Text = "First editor"
            }
        };
        internal readonly LayoutDocument Second = new()
        {
            Title = "Second.cs",
            ContentId = "second",
            Content = new TextBox
            {
                Text = "Second editor"
            }
        };
        internal readonly LayoutDocument Third = new()
        {
            Title = "Third.cs",
            ContentId = "third",
            Content = new TextBox
            {
                Text = "Third editor"
            }
        };
        internal readonly LayoutAnchorable ToolA = new()
        {
            Title = "Explorer",
            ContentId = "explorer",
            Content = new TextBlock
            {
                Text = "Explorer"
            }
        };
        internal readonly LayoutAnchorable ToolB = new()
        {
            Title = "Toolbox",
            ContentId = "toolbox",
            Content = new TextBlock
            {
                Text = "Toolbox"
            }
        };
        internal readonly LayoutDocumentPane Source, Target;
        private readonly Window _window;
        private readonly IDisposable _registration;
        internal Fixture()
        {
            Source = new LayoutDocumentPane(First);
            Source.Children.Add(Second);
            Target = new LayoutDocumentPane(Third);
            var tools = new LayoutAnchorablePane(ToolA)
            {
                DockWidth = new(220)
            };
            tools.Children.Add(ToolB);
            var panel = new LayoutPanel(tools);
            panel.Children.Add(Source);
            panel.Children.Add(Target);
            Manager.Layout = new()
            {
                RootPanel = panel
            };
            First.IsActive = true;
            _window = new Window
            {
                Content = Manager,
                Title = "UnoDock tear-off acceptance"
            };
            _registration = Microsoft.Windows.Shell.SystemCommands.RegisterWindow(_window);
            _window.AppWindow.Resize(new()
            {
                Width = 1200,
                Height = 760
            });
            _window.AppWindow.Move(new()
            {
                X = 40,
                Y = 40
            });
        }

        internal async Task Show()
        {
            _window.Activate();
            for (var i = 0; i < 80 && (!Manager.IsLoaded || Manager.ActualWidth <= 0); i++)
                await Task.Delay(25);
            await Task.Delay(300);
        }

        internal LayoutTabItemBase Tab(LayoutContent content) => Manager.FindVisualChildren<LayoutTabItemBase>().First(t => ReferenceEquals(t.Model, content));
        internal FrameworkElement TargetView => Manager.FindVisualChildren<LayoutDocumentPaneControl>().First(p => ReferenceEquals(p.Model, Target));

        internal FrameworkElement ToolTitle() => Manager.FindVisualChildren<ContentPresenter>().First(p => p.Name == "PART_ToolCaption" && p.ActualWidth > 0);
        internal LayoutFloatingWindowControl? Control(LayoutContent content) => content.FindParent<LayoutFloatingWindow>() is { } model ? Manager.FloatingWindows.FirstOrDefault(w => ReferenceEquals(w.Model, model)) : null;
        public void Dispose()
        {
            foreach (var control in Manager.FloatingWindows.ToArray())
                control.NativeWindow?.Close();
            _registration.Dispose();
            _window.Close();
        }
    }
}
