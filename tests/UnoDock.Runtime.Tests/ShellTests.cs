using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.Shell;
using UnoDock;
using UnoDock.Controls;
using UnoDock.Layout;

namespace UnoDock.Testing;
public static partial class ShellTests
{
    public static async Task<int> Run(string output, DockingManager host)
    {
        var tests = new TestRunner();
        tests.Test("shell command identity and target owner", () =>
        {
            Check.Equal("CloseWindow", SystemCommands.CloseWindowCommand.Name);
            Check.Equal(typeof(SystemCommands), SystemCommands.CloseWindowCommand.OwnerType);
            Check.Same(SystemCommands.CloseWindowCommand, SystemCommands.CloseWindowCommand);
        });
        tests.Test("shell commands reject absent and arbitrary targets", () =>
        {
            Check.False(SystemCommands.CloseWindowCommand.CanExecute(null));
            Check.False(SystemCommands.MaximizeWindowCommand.CanExecute(new object ()));
            Check.False(SystemCommands.CloseWindowCommand.CanExecute(new ContentControl()));
        });
        tests.Test("null native shell target rejected", () => Check.Throws<ArgumentNullException>(() => SystemCommands.CloseWindow((Window)null!)));
        tests.Test("null compositional target rejected", () => Check.Throws<ArgumentNullException>(() => SystemCommands.CloseWindow((ContentControl)null!)));
        tests.Test("null registration rejected", () => Check.Throws<ArgumentNullException>(() => SystemCommands.RegisterWindow(null!)));
        tests.Test("unknown menu target rejected", () => Check.Throws<ArgumentException>(() => SystemCommands.CreateSystemMenu(new object ())));
        tests.Test("nonfinite menu position rejected", () => Check.Throws<ArgumentOutOfRangeException>(() => SystemCommands.ShowSystemMenu(new ContentControl(), new(double.NaN, 0))));
        tests.Test("chrome attached input has independent local values", () =>
        {
            var a = new Button();
            var b = new Button();
            Check.False(WindowChrome.GetIsHitTestVisibleInChrome(a));
            WindowChrome.SetIsHitTestVisibleInChrome(a, true);
            Check.True(WindowChrome.GetIsHitTestVisibleInChrome(a));
            Check.False(WindowChrome.GetIsHitTestVisibleInChrome(b));
            a.ClearValue(WindowChrome.IsHitTestVisibleInChromeProperty);
            Check.False(WindowChrome.GetIsHitTestVisibleInChrome(a));
        });
        tests.Test("clone retains values without sharing configuration", () =>
        {
            var chrome = new WindowChrome
            {
                CaptionHeight = 47,
                ResizeBorderThickness = new(1, 2, 3, 4),
                GlassFrameThickness = new(5, 6, 7, 8),
                CornerRadius = new(9),
                ShowSystemMenu = false
            };
            var clone = chrome.Clone();
            Check.False(ReferenceEquals(chrome, clone));
            Check.Near(47, clone.CaptionHeight);
            Check.Equal(chrome.ResizeBorderThickness, clone.ResizeBorderThickness);
            Check.Equal(chrome.GlassFrameThickness, clone.GlassFrameThickness);
            Check.Equal(chrome.CornerRadius, clone.CornerRadius);
            Check.False(clone.ShowSystemMenu);
            clone.CaptionHeight = 24;
            Check.Near(47, chrome.CaptionHeight);
        });
        foreach (var value in new[]
        {
            -1d,
            double.NaN,
            double.PositiveInfinity
        }

        )
        {
            tests.Test("caption rejects " + value, () => Check.Throws<ArgumentOutOfRangeException>(() => new WindowChrome().CaptionHeight = value));
            tests.Test("resize border rejects " + value, () => Check.Throws<ArgumentOutOfRangeException>(() => new WindowChrome().ResizeBorderThickness = new(value)));
            tests.Test("corner radius rejects " + value, () => Check.Throws<ArgumentOutOfRangeException>(() => new WindowChrome().CornerRadius = new(value)));
        }

        tests.Test("negative glass side canonicalizes all sides", () =>
        {
            var chrome = new WindowChrome
            {
                GlassFrameThickness = new(0, -7, 0, 0)
            };
            Check.Equal(WindowChrome.GlassFrameCompleteThickness, chrome.GlassFrameThickness);
        });
        tests.Test("invalid direct dependency-property write rolls back", () =>
        {
            var chrome = new WindowChrome
            {
                CaptionHeight = 24
            };
            var calls = 0;
            chrome.PropertyChanged += (_, _) => calls++;
            Check.Throws<ArgumentOutOfRangeException>(() => chrome.SetValue(WindowChrome.CaptionHeightProperty, double.NaN));
            Check.Near(24, chrome.CaptionHeight);
            Check.Equal(0, calls);
        });
        tests.Test("chrome change notifications publish committed values", () =>
        {
            var chrome = new WindowChrome();
            var count = 0;
            chrome.PropertyChanged += (_, e) =>
            {
                Check.Equal("CaptionHeight", e.PropertyName);
                Check.Near(51, chrome.CaptionHeight);
                count++;
            };
            chrome.CaptionHeight = 51;
            chrome.CaptionHeight = 51;
            Check.Equal(1, count);
        });
        tests.Test("same-thread shell metrics are observable and stable", () =>
        {
            var value = SystemParameters2.Current;
            Check.Same(value, SystemParameters2.Current);
            Check.Equal(value.WindowGlassColor, value.WindowGlassBrush.Color);
            var changes = 0;
            System.ComponentModel.PropertyChangedEventHandler handler = (_, _) => changes++;
            value.PropertyChanged += handler;
            try
            {
                value.Refresh();
                Check.Equal(0, changes);
            }
            finally
            {
                value.PropertyChanged -= handler;
            }

            Check.True(double.IsFinite(value.WindowCaptionHeight));
            Check.True(value.WindowCaptionHeight >= 0);
        });
        tests.Test("metrics refresh requires creating thread", async () =>
        {
            var value = SystemParameters2.Current;
            await Task.Run(() => Check.Throws<InvalidOperationException>(value.Refresh));
        });
        if (!OperatingSystem.IsWindows())
            tests.Test("unsupported native metrics are not invented", () =>
            {
                var value = SystemParameters2.Current;
                Check.False(value.IsGlassEnabled);
                Check.Near(0, value.WindowCaptionHeight);
                Check.Equal(new Thickness(0), value.WindowNonClientFrameThickness);
            });
        tests.Test("managed chrome can be shared and detached independently", async () =>
        {
            var a = new LayoutDocumentFloatingWindowControl(new());
            var b = new LayoutDocumentFloatingWindowControl(new());
            var original = a.ResizeBorderThickness;
            var chrome = new WindowChrome
            {
                ResizeBorderThickness = new(12),
                CornerRadius = new(7)
            };
            try
            {
                WindowChrome.SetWindowChrome(a, chrome);
                WindowChrome.SetWindowChrome(b, chrome);
                await Tick();
                Check.Equal(new Thickness(12), a.ResizeBorderThickness);
                Check.Equal(new Thickness(12), b.ResizeBorderThickness);
                Check.True(WindowChrome.GetAppliedCapabilities(a).HasFlag(WindowChromeCapabilities.ManagedFrame));
                WindowChrome.SetWindowChrome(a, null);
                chrome.ResizeBorderThickness = new(9);
                await Tick();
                Check.Equal(original, a.ResizeBorderThickness);
                Check.Equal(new Thickness(9), b.ResizeBorderThickness);
                Check.True(WindowChrome.GetWindowChrome(a) == null);
                Check.Same(chrome, WindowChrome.GetWindowChrome(b));
            }
            finally
            {
                WindowChrome.SetWindowChrome(a, null);
                WindowChrome.SetWindowChrome(b, null);
            }
        });
        tests.Test("queued chrome update cannot mutate a detached host", async () =>
        {
            var control = new LayoutDocumentFloatingWindowControl(new());
            var original = control.ResizeBorderThickness;
            WindowChrome.SetWindowChrome(control, new() { ResizeBorderThickness = new(19) });
            WindowChrome.SetWindowChrome(control, null);
            await Tick();
            Check.Equal(original, control.ResizeBorderThickness);
        });
        tests.Test("detach restores last applied value while a new value is queued", async () =>
        {
            var control = new LayoutDocumentFloatingWindowControl(new());
            var original = control.ResizeBorderThickness;
            var chrome = new WindowChrome
            {
                ResizeBorderThickness = new(17)
            };
            WindowChrome.SetWindowChrome(control, chrome);
            await Tick();
            chrome.ResizeBorderThickness = new(23);
            WindowChrome.SetWindowChrome(control, null);
            await Tick();
            Check.Equal(original, control.ResizeBorderThickness);
        });
        tests.Test("chrome detach preserves independently changed host value", async () =>
        {
            var control = new LayoutDocumentFloatingWindowControl(new());
            WindowChrome.SetWindowChrome(control, new() { ResizeBorderThickness = new(13) });
            await Tick();
            control.ResizeBorderThickness = new(22);
            WindowChrome.SetWindowChrome(control, null);
            Check.Equal(new Thickness(22), control.ResizeBorderThickness);
        });
        tests.Test("document docking captions remain client-area chrome exclusions", async () =>
        {
            var previous = host.Layout;
            var mode = host.FloatingWindowMode;
            try
            {
                host.FloatingWindowMode = FloatingWindowMode.InSurface;
                var document = new LayoutDocument
                {
                    Title = "Document drag handle"
                };
                host.Layout = new()
                {
                    RootPanel = new UnoDock.Layout.LayoutPanel(new LayoutDocumentPane(document))
                };
                document.Float();
                host.Refresh();
                await Tick();
                var floating = host.FloatingWindows.Single();
                var caption = (TextBlock)typeof(LayoutFloatingWindowControl).GetField("_caption", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(floating)!;
                var handle = (Border)typeof(LayoutFloatingWindowControl).GetField("_dragHandle", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(floating)!;
                Check.False(caption.IsHitTestVisible); // The label cannot steal the shared handle's capture.
                Check.True(handle.IsHitTestVisible);
                Check.True(WindowChrome.GetIsHitTestVisibleInChrome(handle));
                var chrome = new WindowChrome
                {
                    CaptionHeight = 64
                };
                WindowChrome.SetWindowChrome(floating, chrome);
                await Tick();
                Check.True(WindowChrome.GetIsHitTestVisibleInChrome(handle));
                WindowChrome.SetWindowChrome(floating, null);
                Check.True(WindowChrome.GetIsHitTestVisibleInChrome(handle));
            }
            finally
            {
                host.Layout = previous;
                host.FloatingWindowMode = mode;
                host.Refresh();
                await Tick();
            }
        });
        tests.Test("managed window commands preserve content and geometry", async () =>
        {
            var previous = host.Layout;
            var mode = host.FloatingWindowMode;
            var doc = new LayoutDocument
            {
                Title = "Shell test",
                ContentId = "shell",
                Content = new TextBox
                {
                    Text = "retained"
                },
                FloatingWidth = 350,
                FloatingHeight = 230
            };
            try
            {
                host.FloatingWindowMode = FloatingWindowMode.InSurface;
                host.Layout = new()
                {
                    RootPanel = new UnoDock.Layout.LayoutPanel(new LayoutDocumentPane(doc))
                };
                doc.Float();
                host.Refresh();
                await Tick();
                var window = host.FloatingWindows.Single();
                var content = doc.Content;
                Check.True(SystemCommands.MaximizeWindowCommand.CanExecute(null, window));
                SystemCommands.MaximizeWindowCommand.Execute(null, window);
                Check.True(window.IsMaximized);
                Check.True(doc.IsMaximized);
                SystemCommands.RestoreWindowCommand.Execute(null, window);
                Check.False(window.IsMaximized);
                Check.Near(350, doc.FloatingWidth);
                Check.Near(230, doc.FloatingHeight);
                SystemCommands.MinimizeWindow(window);
                Check.Equal(Visibility.Collapsed, window.Visibility);
                Check.True(SystemCommands.RestoreWindowCommand.CanExecute(window));
                window.Activate();
                Check.Equal(Visibility.Visible, window.Visibility);
                Check.Same(content, doc.Content);
                Check.False(SystemCommands.RestoreWindowCommand.CanExecute(window));
                doc.CanClose = false;
                Check.False(SystemCommands.CloseWindowCommand.CanExecute(window));
                doc.CanClose = true;
                EventHandler<System.ComponentModel.CancelEventArgs> cancel = (_, e) => e.Cancel = true;
                doc.Closing += cancel;
                SystemCommands.CloseWindow(window);
                Check.True(doc.Root != null);
                doc.Closing -= cancel;
                SystemCommands.CloseWindow(window);
                Check.True(doc.Root == null);
                Check.False(SystemCommands.CloseWindowCommand.CanExecute(window));
            }
            finally
            {
                host.Layout = previous;
                host.FloatingWindowMode = mode;
                host.Refresh();
                await Tick();
            }
        });
        if (OperatingSystem.IsLinux() || OperatingSystem.IsWindows() || OperatingSystem.IsMacOS())
        {
            tests.Test("registered native window routing and close lifecycle", async () =>
            {
                var content = new ContentControl
                {
                    Content = new TextBlock
                    {
                        Text = "Shell lifecycle"
                    }
                };
                var window = new Window
                {
                    Title = "Shell lifecycle",
                    Content = content
                };
                using var lease = SystemCommands.RegisterWindow(window);
                var closed = false;
                window.Closed += (_, _) => closed = true;
                window.Activate();
                await Tick();
                try
                {
                    Check.True(SystemCommands.CloseWindowCommand.CanExecute(content));
                    Check.True(SystemCommands.CloseWindowCommand.CanExecute(window));
                    var chrome = new WindowChrome();
                    WindowChrome.SetWindowChrome(window, chrome);
                    await Tick();
                    Check.Same(chrome, WindowChrome.GetWindowChrome(window));
                    WindowChrome.SetWindowChrome(window, null);
                    Check.True(WindowChrome.GetWindowChrome(window) == null);
                    SystemCommands.CloseWindow(content);
                    await Tick();
                    Check.True(closed);
                    Check.False(SystemCommands.CloseWindowCommand.CanExecute(window));
                }
                finally
                {
                    if (!closed)
                        window.Close();
                }
            });
            tests.Test("unregistered native target stays disabled after close", async () =>
            {
                var window = new Window
                {
                    Content = new Grid()
                };
                var closed = false;
                window.Closed += (_, _) => closed = true;
                window.Activate();
                await Tick();
                try
                {
                    Check.True(SystemCommands.CloseWindowCommand.CanExecute(window));
                    SystemCommands.CloseWindow(window);
                    await Tick();
                    Check.True(closed);
                    Check.False(SystemCommands.CloseWindowCommand.CanExecute(window));
                }
                finally
                {
                    if (!closed)
                        window.Close();
                }
            });
            tests.Test("released registration retains closed-target guard", async () =>
            {
                var window = new Window
                {
                    Content = new Grid()
                };
                var lease = SystemCommands.RegisterWindow(window);
                lease.Dispose();
                lease.Dispose();
                window.Close();
                await Tick();
                Check.False(SystemCommands.CloseWindowCommand.CanExecute(window));
            });
            tests.Test("native window command rejects worker-thread calls", async () =>
            {
                var window = new Window();
                using var lease = SystemCommands.RegisterWindow(window);
                try
                {
                    await Task.Run(() => Check.Throws<InvalidOperationException>(() => SystemCommands.CloseWindow(window)));
                }
                finally
                {
                    window.Close();
                }
            });
        }

        RegisterNativeInput(tests, host);
        return await tests.Run(output, "shell");
    }

    private static async Task Tick()
    {
        await Task.Delay(70);
    }
}
