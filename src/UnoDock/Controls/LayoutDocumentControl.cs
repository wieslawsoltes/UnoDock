using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Input;
using UnoDock.Internal;
using UnoDock.Layout;
using UnoDock.Compatibility;

namespace UnoDock.Controls;

public class LayoutDocumentControl : DockInputControl
{
    public static readonly DependencyProperty ModelProperty = DependencyProperty.Register(nameof(Model), typeof(LayoutContent), typeof(LayoutDocumentControl), new PropertyMetadata(null, (d, e) => ((LayoutDocumentControl)d).OnModelChanged(e)));
    public static readonly DependencyProperty LayoutItemProperty = DependencyProperty.Register(nameof(LayoutItem), typeof(LayoutItem), typeof(LayoutDocumentControl), new PropertyMetadata(null));
    public LayoutContent? Model
    {
        get => (LayoutContent?)GetValue(ModelProperty); set => SetValue(ModelProperty, value);
    }
    public LayoutItem? LayoutItem => (LayoutItem?)GetValue(LayoutItemProperty);

    public LayoutDocumentControl()
    {
        HorizontalContentAlignment = HorizontalAlignment.Stretch;
        VerticalContentAlignment = VerticalAlignment.Stretch;
    }

    protected virtual void OnModelChanged(DependencyPropertyChangedEventArgs e)
    {
        if (Model?.Root?.Manager is { } manager)
        {
            var item = manager.GetLayoutItemFromModel(Model);
            SetLayoutItem(item);
            VisualParenting.Detach(item.View);
            Content = item.View;
        }
        else
        {
            ClearValue(LayoutItemProperty);
            Content = null;
        }
    }

    protected void SetLayoutItem(LayoutItem value) => SetValue(LayoutItemProperty, value);
    protected override void OnPreviewMouseLeftButtonDown(DockMouseButtonEventArgs e)
    {
        if (!e.Handled)
            ActivateModel();
        base.OnPreviewMouseLeftButtonDown(e);
    }

    protected override void OnPreviewMouseRightButtonDown(DockMouseButtonEventArgs e)
    {
        if (!e.Handled)
            ActivateModel();
        base.OnPreviewMouseRightButtonDown(e);
    }

    protected override void OnPreviewGotKeyboardFocus(DockKeyboardFocusChangedEventArgs e)
    {
        base.OnPreviewGotKeyboardFocus(e);
    }

    protected override void OnGotKeyboardFocus(DockKeyboardFocusChangedEventArgs e)
    {
        if (!e.Handled)
            ActivateModel();
        base.OnGotKeyboardFocus(e);
    }

    private void ActivateModel()
    {
        if (Model is { IsEnabled: true, Root: not null } model)
            model.IsActive = true;
    }
}
