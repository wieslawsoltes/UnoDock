using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using UnoDock.Controls;
using UnoDock.Themes;
using Windows.System;
using PathShape = Microsoft.UI.Xaml.Shapes.Path;

namespace UnoDock.Gallery;
public sealed partial class GalleryPage : IDisposable
{
    private readonly Dictionary<string, Action> _sampleCommands = new(StringComparer.Ordinal);
    private SamplePropertyInspector? _sampleInspector;
    private ComboBox? _samplePicker, _themePicker;
    private Grid? _sampleShell;
    private bool _selectingSample, _selectingTheme, _pageDisposed;
    internal SampleKind CurrentSample { get; private set; }
    internal SampleTheme CurrentSampleTheme { get; private set; }
    internal SamplePropertyInspector? PropertyInspector => _sampleInspector;

    private void BuildSampleShell()
    {
        FontSize = 12;
        RequestedTheme = ElementTheme.Light;
        _sampleShell = new Grid();
        foreach (var height in new[]
        {
            SampleChrome.MenuHeight,
            36d,
            double.NaN,
            23d
        }

        )
            _sampleShell.RowDefinitions.Add(new() { Height = double.IsNaN(height) ? new(1, GridUnitType.Star) : new(height) });
        var menu = SampleChrome.CreateMenuBar();
        AddMenu("File", ("New document", "new", AddDocument), ("Save layout", "save", () => Run(Save)), ("Restore layout", "restore", () => Run(Restore)), ("Inspect layout XML", "xml", ShowXml));
        AddMenu("Layout", ("Float / Dock", "float", ToggleFloating), ("Auto-hide / Pin", "pin", TogglePin), ("New vertical tab group", "split-right", () => Split(UnoDock.Core.DockPosition.Right)), ("New horizontal tab group", "split-bottom", () => Split(UnoDock.Core.DockPosition.Bottom)), ("Show hidden tools", "show-tools", ShowHiddenTools), ("Reset current sample", "reset", () => SwitchSample(CurrentSample)));
        AddMenu("Samples", ("Docking", "classic", () => SwitchSample(SampleKind.Classic)), ("IDE workspace", "workspace", () => SwitchSample(SampleKind.Workspace)), ("MVVM binding", "binding", () => SwitchSample(SampleKind.Binding)), ("Add 1,000 tabs", "stress", Stress));
        AddMenu("Diagnostics", ("Public contracts", "parity", ShowParityLab), ("Converters", "converters", ShowConverterLab), ("Native windows", "windows", ShowNativeWindowLab), ("Window shell", "shell", ShowShellLab), ("Window lifecycle", "lifecycle", ShowWindowLifecycleLab), ("Input extensions", "input", ShowInputExtensionsLab), ("Visual observations", "visual", ShowVisualParityLab), ("Navigator", "navigator", ShowNavigatorLab), ("Docking guides", "guides", ShowDockingGuidesLab), ("Splitters", "splitters", ShowSplitterLab), ("Auto-hide", "autohide", ShowAutoHideLab), ("Menus", "menus", ShowMenuLab));
        AddMenu("View", ("Left-to-right", "ltr", () => Dock.FlowDirection = FlowDirection.LeftToRight), ("Right-to-left", "rtl", () => Dock.FlowDirection = FlowDirection.RightToLeft), ("Normal density", "normal-density", () => SetDensity(12)), ("Large text", "large-text", () => SetDensity(18)));
        _sampleShell.Children.Add(menu);
        var toolbar = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 7,
            Margin = new(6, 3, 6, 3)
        };
        toolbar.Children.Add(Label("Sample:"));
        _samplePicker = Picker("SampleSelector", new[] { "Docking", "IDE workspace", "MVVM binding" }, 158);
        _samplePicker.SelectionChanged += (_, _) =>
        {
            if (!_selectingSample && _samplePicker.SelectedIndex >= 0)
                SwitchSample((SampleKind)_samplePicker.SelectedIndex);
        };
        toolbar.Children.Add(_samplePicker);
        toolbar.Children.Add(Label("Theme:"));
        _themePicker = Picker("ThemeSelector", new[] { "Generic", "Light", "Dark" }, 100);
        _themePicker.SelectionChanged += (_, _) =>
        {
            if (!_selectingTheme && _themePicker.SelectedIndex >= 0)
                SetSampleTheme((SampleTheme)_themePicker.SelectedIndex);
        };
        toolbar.Children.Add(_themePicker);
        toolbar.Children.Add(new Border { Width = 1, Margin = new(2, 2, 2, 2), Background = SampleChrome.Color(0xb4b4b4) });
        Tool("New document", "new", "M 3,1 L 10,1 L 13,4 L 13,15 L 3,15 Z M 10,1 L 10,4 L 13,4 M 5,8 L 11,8 M 8,5 L 8,11");
        Tool("Save layout", "save", "M 2,2 L 13,2 L 14,3 L 14,14 L 2,14 Z M 5,2 L 5,6 L 11,6 L 11,2 M 5,10 L 11,10 L 11,14 L 5,14 Z");
        Tool("Restore layout", "restore", "M 3,5 C 5,1 13,2 13,8 C 13,14 5,15 3,11 M 3,1 L 3,5 L 7,5");
        Tool("Float / Dock", "float", "M 1,5 L 10,5 L 10,14 L 1,14 Z M 5,1 L 14,1 L 14,10 M 8,7 L 14,1 M 10,1 L 14,1 L 14,5");
        var reset = SampleChrome.Button("Reset layout", () => ExecuteSampleCommand("reset"));
        reset.Padding = new(8, 1, 8, 1);
        reset.MinHeight = 24;
        toolbar.Children.Add(reset);
        var tools = new ScrollViewer
        {
            Content = toolbar,
            HorizontalScrollMode = ScrollMode.Enabled,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
        Grid.SetRow(tools, 1);
        _sampleShell.Children.Add(tools);
        Dock.Margin = new(2, 0, 2, 0);
        Dock.FontSize = 12;
        Grid.SetRow(Dock, 2);
        _sampleShell.Children.Add(Dock);
        _status.FontSize = 11;
        _status.Margin = new(7, 3, 7, 0);
        _status.TextTrimming = TextTrimming.CharacterEllipsis;
        Grid.SetRow(_status, 3);
        _sampleShell.Children.Add(_status);
        Content = _sampleShell;
        _themePicker.SelectedIndex = 0;
        _samplePicker.SelectedIndex = 0;
        void AddMenu(string title, params (string Label, string Id, Action Invoke)[] commands)
        {
            var parent = SampleChrome.CreateMenuItem(title);
            foreach (var command in commands)
            {
                _sampleCommands.Add(command.Id, command.Invoke);
                var item = new MenuFlyoutItem
                {
                    Text = command.Label
                };
                AutomationProperties.SetAutomationId(item, "SampleCommand-" + command.Id);
                item.Click += (_, _) => ExecuteSampleCommand(command.Id);
                parent.Items.Add(item);
            }

            menu.Items.Add(parent);
        }

        void Tool(string title, string command, string data)
        {
            var icon = (PathShape)Microsoft.UI.Xaml.Markup.XamlReader.Load($"<Path xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' Data='{data}' StrokeThickness='1' Width='16' Height='16'/>");
            var button = SampleChrome.Button("", () => ExecuteSampleCommand(command), title);
            button.Content = icon;
            button.Width = 26;
            button.Height = 24;
            button.Configure(SampleChrome.Default(false));
            AutomationProperties.SetAutomationId(button, "SampleToolbar-" + command);
            toolbar.Children.Add(button);
        }

        static TextBlock Label(string value) => new()
        {
            Text = value,
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = 12
        };
        static ComboBox Picker(string id, string[] items, double width)
        {
            var box = new ComboBox
            {
                ItemsSource = items,
                Width = width,
                MinHeight = 0,
                Height = 27,
                Padding = new(6, 2, 0, 2),
                FontSize = 12
            };
            AutomationProperties.SetAutomationId(box, id);
            return box;
        }
    }

    internal void ExecuteSampleCommand(string id)
    {
        ObjectDisposedException.ThrowIf(_pageDisposed, this);
        if (!_sampleCommands.TryGetValue(id, out var action))
            throw new ArgumentException("Unknown sample command.", nameof(id));
        try
        {
            action();
        }
        catch (Exception error)
        {
            Log(error.Message);
            _status.Text = error.Message;
        }
    }

    internal void SetSampleTheme(SampleTheme theme)
    {
        if (!Enum.IsDefined(theme))
            throw new ArgumentOutOfRangeException(nameof(theme));
        _selectingTheme = true;
        try
        {
            if (_themePicker != null)
                _themePicker.SelectedIndex = (int)theme;
        }
        finally
        {
            _selectingTheme = false;
        }

        CurrentSampleTheme = theme;
        var dark = theme == SampleTheme.Dark;
        RequestedTheme = dark ? ElementTheme.Dark : ElementTheme.Light;
        Dock.Theme = theme == SampleTheme.Generic ? new GenericTheme() : new FluentTheme(RequestedTheme);
        if (_sampleShell != null)
        {
            _sampleShell.Background = SampleChrome.Color(dark ? 0x252526u : 0xf0f0f0u);
            foreach (var button in _sampleShell.FindVisualChildren<SampleButton>())
                button.Configure(SampleChrome.Default(dark));
        }

        _status.Foreground = SampleChrome.Default(dark).Foreground;
    }

    private void SetDensity(double fontSize)
    {
        Dock.Resources["UnoDock.FontSize"] = fontSize;
        Dock.Refresh();
    }

    private void ToggleFloating()
    {
        if (Dock.Layout.ActiveContent is not { } active)
            return;
        if (active.IsFloating)
            active.Dock();
        else
            active.Float();
    }

    private void TogglePin()
    {
        if (Dock.Layout.ActiveContent is LayoutAnchorable tool)
            tool.ToggleAutoHide();
        else
            _status.Text = "Select a tool window before changing auto-hide.";
    }

    private void ShowHiddenTools()
    {
        foreach (var tool in Dock.Layout.Hidden.ToArray())
            tool.Show();
    }

    private void Reset() => SwitchSample(SampleKind.Classic);
    internal void SwitchSample(SampleKind sample)
    {
        if (!Enum.IsDefined(sample))
            throw new ArgumentOutOfRangeException(nameof(sample));
        ObjectDisposedException.ThrowIf(_pageDisposed, this);
        ++_workspaceEpoch;
        ++_restoreRequest;
        StopMvvmWorkspace();
        _sampleInspector?.Dispose();
        _sampleInspector = null;
        _selectingSample = true;
        try
        {
            using var batch = Dock.BeginLayoutUpdate();
            Dock.DocumentsSource = null;
            Dock.AnchorablesSource = null;
            Dock.LayoutItemTemplate = null;
            Dock.LayoutItemContainerStyle = null;
            _content.Clear();
            CurrentSample = sample;
            switch (sample)
            {
                case SampleKind.Classic:
                    BuildClassicSample();
                    break;
                case SampleKind.Workspace:
                    ResetWorkbench();
                    break;
                case SampleKind.Binding:
                    BindingDemo();
                    break;
            }

            if (_samplePicker != null)
                _samplePicker.SelectedIndex = (int)sample;
            _status.Text = "Ready  |  Drag tabs to dock  |  Ctrl+Tab: switch  |  Ctrl+F4: close  |  UnoDock preview 16";
        }
        finally
        {
            _selectingSample = false;
        }
    }

    private void BuildClassicSample()
    {
        // Independently authored from the publicly illustrated sample arrangement.
        // No original sample XAML, resource dictionary or image is loaded here.
        _sampleInspector = new SamplePropertyInspector(Dock);
        var properties = ToolModel("properties", "Properties", _sampleInspector);
        properties.CanHide = false;
        properties.CanClose = false;
        properties.AutoHideWidth = 240;
        var contentButton = new Button
        {
            Content = "Document 1 Content",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MinHeight = 0,
            Padding = new(8, 3, 8, 3),
            FontSize = 12
        };
        var first = Document("document1", "Document 1", contentButton);
        contentButton.Click += (_, _) => Log("Document 1 button invoked.");
        var second = Document("document2", "Document 2", Editor("Document 2 Content"));
        var documents = new LayoutDocumentPane(first);
        documents.Children.Add(second);
        var alarms = new ListBox
        {
            ItemsSource = new[]
            {
                "Alarm 1",
                "Alarm 2",
                "Alarm 3"
            },
            FontSize = 12,
            BorderThickness = new(0)
        };
        alarms.ItemContainerStyle = (Style)Microsoft.UI.Xaml.Markup.XamlReader.Load("<Style xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' TargetType='ListBoxItem'><Setter Property='MinHeight' Value='22'/><Setter Property='Padding' Value='4,1'/><Setter Property='FontSize' Value='12'/></Style>");
        var alarmModel = ToolModel("alarms", "Alarms", alarms);
        var journal = ToolModel("journal", "Journal", Editor("Journal\n\nAn editable journal accompanies this docking sample.\nSwitch tools, float this window, then dock it back: the same editor is retained."));
        var right = new LayoutAnchorablePane(alarmModel);
        right.Children.Add(journal);
        var rightGroup = new LayoutAnchorablePaneGroup
        {
            DockWidth = new(125),
            DockMinWidth = 100
        };
        rightGroup.Children.Add(right);
        var documentGroup = new LayoutDocumentPaneGroup();
        documentGroup.Children.Add(documents);
        var panel = new LayoutPanel(new LayoutAnchorablePane(properties) { DockWidth = new(200), DockMinWidth = 150 });
        panel.Orientation = Orientation.Horizontal;
        panel.Children.Add(documentGroup);
        panel.Children.Add(rightGroup);
        var root = new LayoutRoot
        {
            RootPanel = panel
        };
        var rail = new LayoutAnchorGroup();
        rail.Children.Add(ToolModel("agenda", "Agenda", Editor("Agenda\n\n09:00  Review workspace\n10:30  Layout design\n14:00  Runtime validation")));
        rail.Children.Add(ToolModel("contacts", "Contacts", Editor("Contacts\n\nProject team\nDesign review\nSupport")));
        root.LeftSide.Children.Add(rail);
        Dock.AllowMixedOrientation = true;
        Dock.Layout = root;
        first.IsActive = true;
    }

    private LayoutAnchorable ToolModel(string id, string title, object content)
    {
        _content[id] = content;
        return new()
        {
            ContentId = id,
            Title = title,
            Content = content
        };
    }

    internal string SerializeSample()
    {
        using var writer = new StringWriter();
        new XmlLayoutSerializer(Dock).Serialize(writer);
        return writer.ToString();
    }

    internal void RestoreSample(string xml)
    {
        if (_mvvmWorkspace?.IsCurrent == true)
        {
            _mvvmWorkspace.RestoreLayout(xml);
            return;
        }

        var serializer = new XmlLayoutSerializer(Dock);
        serializer.LayoutSerializationCallback += (_, e) =>
        {
            if (e.Model.ContentId is { } id && _content.TryGetValue(id, out var content))
                e.Content = content;
        };
        serializer.Deserialize(new StringReader(xml));
    }

    public new void Dispose()
    {
        if (_pageDisposed)
            return;
        ++_workspaceEpoch;
        ++_restoreRequest;
        StopMvvmWorkspace();
        _pageDisposed = true;
        _sampleInspector?.Dispose();
        _sampleInspector = null;
        Dock.Dispose();
        _content.Clear();
        _sampleCommands.Clear();
    }
}
