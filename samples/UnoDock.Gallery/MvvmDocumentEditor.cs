using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Data;

namespace UnoDock.Gallery;

public sealed class MvvmDocumentEditor : UserControl
{
    internal TextBox Editor { get; }
    public MvvmDocumentEditor()
    {
        var grid = new Grid();
        grid.RowDefinitions.Add(new() { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new() { Height = new(1, GridUnitType.Star) });
        grid.RowDefinitions.Add(new() { Height = GridLength.Auto });
        var toolbar = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Margin = new(5, 3, 5, 3) };
        Command("Save", nameof(WorkspaceDocument.SaveCommand), "MvvmEditorSave");
        Command("Revert", nameof(WorkspaceDocument.RevertCommand), "MvvmEditorRevert");
        Command("Close", nameof(WorkspaceDocument.CloseCommand), "MvvmEditorClose");
        grid.Children.Add(toolbar);
        Editor = new TextBox
        {
            AcceptsReturn = true, TextWrapping = TextWrapping.NoWrap,
            FontFamily = new FontFamily(OperatingSystem.IsWindows() ? "Consolas" : "DejaVu Sans Mono"),
            FontSize = 12, Padding = new(8), BorderThickness = new(0),
            HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch
        };
        AutomationProperties.SetAutomationId(Editor, "MvvmEditorText");
        Editor.SetBinding(TextBox.TextProperty, new Binding { Path = new(nameof(WorkspaceDocument.EditorText)), Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });
        Editor.SetBinding(TextBox.IsReadOnlyProperty, new Binding { Path = new(nameof(WorkspaceDocument.IsReadOnly)), Mode = BindingMode.OneWay });
        Grid.SetRow(Editor, 1); grid.Children.Add(Editor);
        var footer = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Margin = new(7, 3, 7, 3) };
        footer.Children.Add(new TextBlock { Text = "Lines", FontSize = 11 }); Value(nameof(WorkspaceDocument.LineCount));
        footer.Children.Add(new TextBlock { Text = "  Characters", FontSize = 11 }); Value(nameof(WorkspaceDocument.CharacterCount));
        Grid.SetRow(footer, 2); grid.Children.Add(footer); Content = grid;
        void Command(string title, string property, string id)
        {
            var button = new Button { Content = title, FontSize = 12, MinHeight = 24, Padding = new(7, 2, 7, 2) };
            button.SetBinding(Button.CommandProperty, new Binding { Path = new(property), Mode = BindingMode.OneWay });
            AutomationProperties.SetAutomationId(button, id); toolbar.Children.Add(button);
        }
        void Value(string property)
        {
            var text = new TextBlock { FontSize = 11 };
            text.SetBinding(TextBlock.TextProperty, new Binding { Path = new(property) }); footer.Children.Add(text);
        }
    }
}

public sealed class MvvmToolPresenter : ContentControl
{
    public MvvmToolPresenter()
    {
        HorizontalContentAlignment = HorizontalAlignment.Stretch;
        VerticalContentAlignment = VerticalAlignment.Stretch;
        DataContextChanged += (_, _) => Present();
        Loaded += (_, _) => Present();
    }
    private void Present()
    {
        var view = (DataContext as WorkspaceTool)?.View;
        if (ReferenceEquals(Content, view)) return;
        if (view != null)
        {
            // The view belongs to a WorkspaceTool created by this sample. Restore
            // may create a new template container; transfer that owned view without
            // rebuilding its list, selection or scroll state.
            switch (VisualTreeHelper.GetParent(view))
            {
                case ContentPresenter parent when ReferenceEquals(parent.Content, view): parent.Content = null; break;
                case ContentControl parent when ReferenceEquals(parent.Content, view): parent.Content = null; break;
                case Border parent when ReferenceEquals(parent.Child, view): parent.Child = null; break;
                case Panel parent: parent.Children.Remove(view); break;
            }
        }
        Content = view;
    }
}
