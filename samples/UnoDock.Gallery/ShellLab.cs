using Microsoft.Windows.Shell;
using UnoDock.Controls;

namespace UnoDock.Gallery;
public sealed partial class GalleryPage
{
    private void ShowShellLab()
    {
        var stack = new StackPanel
        {
            Spacing = 12,
            Padding = new(24)
        };
        stack.Children.Add(new TextBlock { Text = "Window shell laboratory", FontSize = 25, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        stack.Children.Add(new TextBlock { Text = "Create a frame, resize its edges or corners, then try maximize, minimize, restore and close. Detaching chrome cancels an unfinished frame drag. Commands target this frame explicitly—not whichever native window is active.", TextWrapping = TextWrapping.Wrap, MaxWidth = 840 });
        var info = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            FontFamily = new FontFamily("Consolas")
        };
        var metrics = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap
        };
        var chrome = new WindowChrome
        {
            CaptionHeight = 38,
            ResizeBorderThickness = new(8),
            CornerRadius = new(8)
        };
        LayoutDocument? model = null;
        var protectClosing = false;
        LayoutFloatingWindowControl? Host() => model == null ? null : Dock.FloatingWindows.FirstOrDefault(w => ReferenceEquals(w.Model, model.FindParent<LayoutFloatingWindow>()));
        void Describe()
        {
            var host = Host();
            var capability = host?.NativeWindow is { } native ? WindowChrome.GetAppliedCapabilities(native) : host == null ? WindowChromeCapabilities.None : WindowChrome.GetAppliedCapabilities(host);
            info.Text = host == null ? "No floating frame. Create one or float it again." : $"Host: {(host.NativeWindow == null ? "in-surface" : "native")}    Applied: {capability}\nBounds (DIPs): {model!.FloatingLeft:F1}, {model.FloatingTop:F1}, {model.FloatingWidth:F1} × {model.FloatingHeight:F1}\nMaximized: {host.IsMaximized}    Close allowed: {model.CanClose}";
            var value = SystemParameters2.Current;
            metrics.Text = $"OS metrics: {value.UxThemeName} / {value.UxThemeColor}; high contrast={value.HighContrast}; glass={value.IsGlassEnabled}; caption={value.WindowCaptionHeight:F1} DIPs. Zero native measurements mean unavailable, not an inferred frame size.";
        }

        void Apply()
        {
            var host = Host();
            if (host == null)
            {
                Describe();
                return;
            }

            if (host.NativeWindow is { } native)
                WindowChrome.SetWindowChrome(native, chrome);
            else
                WindowChrome.SetWindowChrome(host, chrome);
            DispatcherQueue.TryEnqueue(Describe);
        }

        void Create(bool native)
        {
            if (model?.Root != null)
            {
                model.CanClose = true;
                model.Close();
            }

            Dock.FloatingWindowMode = native ? FloatingWindowMode.Native : FloatingWindowMode.InSurface;
            model = Document("shell-frame-" + _nextDocument++, "Shell experiment", Editor("This editor is retained across frame operations.\n\nResize the left edge: the right edge remains fixed.\nSet a caption control's IsHitTestVisibleInChrome attached property to keep it interactive.\n\nNative caption drag regions and DWM glass are Windows-only. The frame remains usable on other hosts without pretending to provide unsupported OS effects."));
            model.CanClose = !protectClosing;
            model.FloatingWidth = 620;
            model.FloatingHeight = 400;
            model.FloatingLeft = 80;
            model.FloatingTop = 90;
            Dock.Layout.Descendents().OfType<LayoutDocumentPane>().First().Children.Add(model);
            model.Float();
            Dock.Refresh();
            DispatcherQueue.TryEnqueue(Apply);
        }

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6
        };
        Add("Create in-surface", () => Create(false));
        Add("Create native", () => Create(true));
        Add("Apply chrome", Apply);
        Add("Detach chrome", () =>
        {
            if (Host()is { } host)
            {
                WindowChrome.SetWindowChrome(host, null);
                if (host.NativeWindow is { } native)
                    WindowChrome.SetWindowChrome(native, null);
            }

            Describe();
        });
        stack.Children.Add(new ScrollViewer { Content = actions, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled });
        var commands = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6
        };
        foreach (var(label, command)in new[]
        {
            ("Maximize", SystemCommands.MaximizeWindowCommand),
            ("Minimize", SystemCommands.MinimizeWindowCommand),
            ("Restore", SystemCommands.RestoreWindowCommand),
            ("Close", SystemCommands.CloseWindowCommand)
        }

        )
        {
            var button = new Button
            {
                Content = label
            };
            button.Click += (_, _) =>
            {
                if (Host()is { } host)
                    command.Execute(null, host);
                Describe();
            };
            commands.Children.Add(button);
        }

        var menu = new Button
        {
            Content = "System menu"
        };
        commands.Children.Add(menu);
        menu.Click += (_, _) =>
        {
            if (Host()is { } host)
                SystemCommands.CreateSystemMenu(host).ShowAt(menu);
        };
        stack.Children.Add(commands);
        Slider("Caption height", 0, 96, chrome.CaptionHeight, v => chrome.CaptionHeight = v);
        Slider("Resize border", 0, 24, chrome.ResizeBorderThickness.Left, v => chrome.ResizeBorderThickness = new(v));
        Slider("Managed corner radius", 0, 32, chrome.CornerRadius.TopLeft, v => chrome.CornerRadius = new(v));
        Toggle("Show managed caption menu", true, v => chrome.ShowSystemMenu = v);
        Toggle("Extend Windows glass over entire client", false, v => chrome.GlassFrameThickness = v ? WindowChrome.GlassFrameCompleteThickness : new(0));
        Toggle("Protect the experiment from closing", false, v =>
        {
            protectClosing = v;
            if (model != null)
                model.CanClose = !v;
            Describe();
        });
        stack.Children.Add(info);
        stack.Children.Add(metrics);
        var timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(350)
        };
        timer.Tick += (_, _) => Describe();
        stack.Loaded += (_, _) =>
        {
            Describe();
            timer.Start();
        };
        stack.Unloaded += (_, _) => timer.Stop();
        var document = Document("shell-lab-" + _nextDocument++, "Window shell", new ScrollViewer { Content = stack });
        Dock.Layout.Descendents().OfType<LayoutDocumentPane>().First().Children.Add(document);
        document.IsActive = true;
        void Add(string label, Action execute)
        {
            var button = new Button
            {
                Content = label
            };
            button.Click += (_, _) =>
            {
                try
                {
                    execute();
                }
                catch (Exception e)
                {
                    Log(e.Message);
                }
            };
            actions.Children.Add(button);
        }

        void Slider(string label, double min, double max, double initial, Action<double> change)
        {
            var control = new Slider
            {
                Header = label,
                Minimum = min,
                Maximum = max,
                Value = initial,
                Width = 460,
                HorizontalAlignment = HorizontalAlignment.Left
            };
            control.ValueChanged += (_, e) =>
            {
                change(e.NewValue);
                DispatcherQueue.TryEnqueue(Describe);
            };
            stack.Children.Add(control);
        }

        void Toggle(string label, bool initial, Action<bool> change)
        {
            var control = new CheckBox
            {
                Content = label,
                IsChecked = initial
            };
            control.Checked += (_, _) => change(true);
            control.Unchecked += (_, _) => change(false);
            stack.Children.Add(control);
        }
    }
}
