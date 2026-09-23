using System.Globalization;
using UnoDock.Compatibility;

namespace UnoDock.Gallery;

public sealed partial class GalleryPage
{
    private void ShowConverterLab()
    {
        if (Dock.DocumentsSource != null) { Log("Leave source-bound mode before adding a converter lab document."); return; }
        var document = Document("converter-lab-" + _nextDocument++, "Converter & binding lab", new ConverterLab());
        var pane = Dock.Layout.Descendents().OfType<LayoutDocumentPane>().FirstOrDefault();
        if (pane == null) { pane = new(); Dock.Layout.RootPanel.Children.Add(pane); }
        pane.Children.Add(document); document.IsActive = true;
    }
    private void ShowNativeWindowLab()
    {
        if (OperatingSystem.IsBrowser()) { Log("Native windows require a desktop head. Browser floating windows remain in the surface."); return; }
        if (Dock.DocumentsSource != null) { Log("Leave source-bound mode before opening native tool windows."); return; }
        Dock.FloatingWindowMode = FloatingWindowMode.Native;
        foreach (var title in new[] { "Native docking A", "Native docking B" })
        {
            var tool = new LayoutAnchorable
            {
                ContentId = "native-lab-" + _nextDocument++, Title = title,
                Content = new TextBox { AcceptsReturn = true, Text = "Drag this TOOL TAB into the other native tool window or into the main workspace. The editor is retained.\n\nNative WinUI, Uno Skia Win32 and X11 use client-coordinate conversion. macOS/custom islands still require an application adapter." },
                FloatingLeft = title.EndsWith('A') ? 90 : 700, FloatingTop = 140, FloatingWidth = 520, FloatingHeight = 380
            };
            _content[tool.ContentId] = tool.Content;
            tool.AddToLayout(Dock, AnchorableShowStrategy.Right); tool.Float();
        }
        Log("Created two native tool windows. Use their tab headers to test cross-window docking; OS title-bar docking is a separate remaining boundary.");
    }
}

/// <summary>Bindings attach only while this view is loaded, so closing/resetting the
/// workspace cannot leave source subscriptions or queued callbacks targeting a stale view.</summary>
internal sealed class ConverterLab : UserControl
{
    private readonly LabModel _model = new();
    private readonly List<ConverterBinding> _bindings = [];
    private readonly ContentControl _result = new() { Content = "Retained initial value" };
    private readonly TextBox _editor = new() { Header = "Two-way editor (source normalizes whitespace)" };
    private readonly TextBlock _source = new();
    public ConverterLab()
    {
        var content = new StackPanel { Padding = new(24), Spacing = 14, MaxWidth = 920, HorizontalAlignment = HorizontalAlignment.Left };
        content.Children.Add(new TextBlock { Text = "Converter contracts, without silent fallback", FontSize = 26, TextWrapping = TextWrapping.Wrap });
        content.Children.Add(new TextBlock { Text = "Change the transfer result below. DoNothing retains the current target; UnsetValue applies the configured fallback. Native WinUI Binding has no DoNothing sentinel, so this example uses ConverterBinding explicitly.", TextWrapping = TextWrapping.Wrap });
        var input = new TextBox { Header = "Source text", Text = "First value" };
        input.TextChanged += (_, _) => _model.Text = input.Text;
        var mode = new ComboBox { Header = "Forward transfer", ItemsSource = new[] { "Value", "DoNothing", "UnsetValue" }, SelectedIndex = 0, MinWidth = 220 };
        mode.SelectionChanged += (_, _) => _model.Mode = mode.SelectedIndex;
        content.Children.Add(input); content.Children.Add(mode);
        content.Children.Add(new TextBlock { Text = "TARGET VALUE", Opacity = .65 }); content.Children.Add(_result);
        content.Children.Add(_editor); content.Children.Add(_source);
        var explanation = new TextBlock { Text = "Two-way transfers use independent write guards, drain reentrant source notifications, marshal background notifications to the UI thread, and enforce one binding owner per target property. Bindings are disposed on unload and reattached on load.", TextWrapping = TextWrapping.Wrap, Opacity = .8 };
        content.Children.Add(explanation);
        var background = new Button { Content = "Send a source change from a worker thread" };
        background.Click += async (_, _) =>
        {
            background.IsEnabled = false;
            try { await Task.Run(() => _model.Edit = "Worker update " + DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture)); }
            finally { background.IsEnabled = true; }
        };
        content.Children.Add(background);
        Content = new ScrollViewer { Content = content };
        Loaded += (_, _) => Attach(); Unloaded += (_, _) => Detach();
    }
    private void Attach()
    {
        if (_bindings.Count != 0) return;
        try
        {
            _bindings.Add(new(_result, ContentControl.ContentProperty, () => _model.Text,
                value => _model.Mode switch { 1 => BindingValue.DoNothing, 2 => DependencyProperty.UnsetValue, _ => value },
                [_model], fallbackValue: "Fallback value", useFallbackValue: true));
            _bindings.Add(new(_editor, TextBox.TextProperty, () => _model.Edit, value => value,
                [_model], nameof(LabModel.Edit), writeSource: value => _model.Edit = ((string?)value ?? "").Trim(), convertBack: value => value));
            _bindings.Add(new(_source, TextBlock.TextProperty, () => "SOURCE MODEL: " + _model.Edit, value => value, [_model], nameof(LabModel.Edit)));
        }
        catch { Detach(); throw; }
    }
    private void Detach() { foreach (var binding in _bindings) binding.Dispose(); _bindings.Clear(); }
    private sealed class LabModel : INotifyPropertyChanged
    {
        private string _text = "First value", _edit = "Edit me"; private int _mode;
        public string Text { get => _text; set { if (_text == value) return; _text = value; Changed(nameof(Text)); } }
        public string Edit { get => _edit; set { if (_edit == value) return; _edit = value; Changed(nameof(Edit)); } }
        public int Mode { get => _mode; set { if (_mode == value) return; _mode = value; Changed(nameof(Mode)); } }
        public event PropertyChangedEventHandler? PropertyChanged;
        private void Changed(string name) => PropertyChanged?.Invoke(this, new(name));
    }
}
