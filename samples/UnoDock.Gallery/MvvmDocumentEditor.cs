using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Data;

namespace UnoDock.Gallery;

public sealed class MvvmDocumentEditor : UserControl
{
    private readonly List<SampleButton> _commands = [];
    private readonly Border _commandFrame = new();
    private readonly Border _statusFrame = new();
    internal TextBox Editor
    {
        get;
    }

    public MvvmDocumentEditor()
    {
        var grid = new Grid();
        grid.RowDefinitions.Add(new()
        {
            Height = new(31)
        });
        grid.RowDefinitions.Add(new()
        {
            Height = new(1, GridUnitType.Star)
        });
        grid.RowDefinitions.Add(new()
        {
            Height = new(23)
        });
        var toolbar = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 4,
            Margin = new(5, 3, 5, 3)
        };
        Command("Save", nameof(WorkspaceDocument.SaveCommand), "MvvmEditorSave");
        Command("Revert", nameof(WorkspaceDocument.RevertCommand), "MvvmEditorRevert");
        Command("Close", nameof(WorkspaceDocument.CloseCommand), "MvvmEditorClose");
        _commandFrame.Child = toolbar;
        _commandFrame.BorderThickness = new(0, 0, 0, 1);
        AutomationProperties.SetAutomationId(_commandFrame, "MvvmEditorCommandBar");
        grid.Children.Add(_commandFrame);
        Editor = new TextBox
        {
            AcceptsReturn = true,
            TextWrapping = TextWrapping.NoWrap,
            FontFamily = new FontFamily(OperatingSystem.IsWindows() ? "Consolas" : "DejaVu Sans Mono"),
            FontSize = 12,
            Padding = new(8),
            BorderThickness = new(0),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        AutomationProperties.SetAutomationId(Editor, "MvvmEditorText");
        Editor.SetBinding(TextBox.TextProperty, new Binding { Path = new(nameof(WorkspaceDocument.EditorText)), Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });
        Editor.SetBinding(TextBox.IsReadOnlyProperty, new Binding { Path = new(nameof(WorkspaceDocument.IsReadOnly)), Mode = BindingMode.OneWay });
        Grid.SetRow(Editor, 1);
        grid.Children.Add(Editor);
        var footer = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            Margin = new(7, 3, 7, 3)
        };
        footer.Children.Add(new TextBlock { Text = "Lines", FontSize = 11 });
        Value(nameof(WorkspaceDocument.LineCount));
        footer.Children.Add(new TextBlock { Text = "  Characters", FontSize = 11 });
        Value(nameof(WorkspaceDocument.CharacterCount));
        _statusFrame.Child = footer;
        _statusFrame.BorderThickness = new(0, 1, 0, 0);
        AutomationProperties.SetAutomationId(_statusFrame, "MvvmEditorStatusBar");
        Grid.SetRow(_statusFrame, 2);
        grid.Children.Add(_statusFrame);
        Content = grid;
        // Control-local subscriptions do not retain a document or recreate its view
        // when native reparenting or an inherited theme changes.
        Loaded += (_, _) => UpdateChrome();
        ActualThemeChanged += (_, _) => UpdateChrome();
        UpdateChrome();
        void Command(string title, string property, string id)
        {
            var button = new SampleButton
            {
                Content = title,
                Height = 24,
                Padding = new(8, 1, 8, 1)
            };
            button.SetBinding(Button.CommandProperty, new Binding { Path = new(property), Mode = BindingMode.OneWay });
            AutomationProperties.SetAutomationId(button, id);
            AutomationProperties.SetName(button, title);
            ToolTipService.SetToolTip(button, title);
            _commands.Add(button);
            toolbar.Children.Add(button);
        }

        void Value(string property)
        {
            var text = new TextBlock
            {
                FontSize = 11
            };
            text.SetBinding(TextBlock.TextProperty, new Binding { Path = new(property) });
            footer.Children.Add(text);
        }
    }

    private void UpdateChrome()
    {
        var dark = ActualTheme == ElementTheme.Dark;
        var palette = SampleChrome.Default(dark);
        Foreground = palette.Foreground;
        foreach (var command in _commands)
            command.Configure(palette);
        _commandFrame.Background = _statusFrame.Background = SampleChrome.Color(dark ? 0x2d2d30u : 0xf5f5f5u);
        _commandFrame.BorderBrush = _statusFrame.BorderBrush = SampleChrome.Color(dark ? 0x454545u : 0xd4d4d4u);
    }
}
