using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Data;
using UnoDock.Internal;
using UnoDock.Layout;

namespace UnoDock.Controls;
/// <summary>Ordinary ListBox selection/automation with compact, independently authored chrome.</summary>
public sealed partial class NavigatorListBox : ListBox
{
    [ThreadStatic]
    private static ControlTemplate? _template;
    [ThreadStatic]
    private static ItemsPanelTemplate? _panel;
    [ThreadStatic]
    private static DataTemplate? _itemTemplate;
    internal event Action<LayoutItem>? ItemInvoked;
    private DockPalette _palette = DockChrome.Default(false);
    private Brush _surface = DockChrome.Transparent;
    public NavigatorListBox()
    {
        MinHeight = 0;
        MaxHeight = 400;
        MinWidth = 0;
        MaxWidth = 340;
        Margin = new(5, 0, 5, 5);
        Padding = new(0);
        BorderThickness = new(0);
        HorizontalAlignment = HorizontalAlignment.Stretch;
        HorizontalContentAlignment = HorizontalAlignment.Stretch;
        VerticalAlignment = VerticalAlignment.Top;
        // No ListBox.SelectionMode or ScrollIntoView calls: they are not implemented
        // by the pinned Uno ListBox. Its existing single-selection model is retained.
        Template = _template ??= (ControlTemplate)DockChrome.Resource<ControlTemplate>("UnoDock.NavigatorListTemplate");
        ItemsPanel = _panel ??= (ItemsPanelTemplate)DockChrome.Resource<ItemsPanelTemplate>("UnoDock.NavigatorItemsPanel");
        ItemTemplate = _itemTemplate ??= (DataTemplate)DockChrome.Resource<DataTemplate>("UnoDock.NavigatorItemTemplate");
    }

    // LayoutItem inherits FrameworkElement, but it is a model adapter, not an
    // item container. Uno's generic ItemsControl otherwise inserts that zero-sized
    // object directly and never invokes ItemTemplate or creates a selectable row.
    protected override bool IsItemItsOwnContainerOverride(object item) => item is ListBoxItem;
    protected override DependencyObject GetContainerForItemOverride() => new NavigatorListItem();
    protected override void PrepareContainerForItemOverride(DependencyObject element, object item)
    {
        base.PrepareContainerForItemOverride(element, item);
        if (element is NavigatorListItem row)
        {
            row.Invoked -= RowInvoked;
            row.Invoked += RowInvoked;
            row.Configure(_palette, _surface);
            // The stock item template shows the title only. Rows present the
            // content's header template or icon unless the application replaced it.
            if (item is LayoutItem layoutItem && ReferenceEquals(ItemTemplate, _itemTemplate) && ItemTemplateSelector == null)
                row.PresentHeader(layoutItem);
            else
                row.ReleaseHeader();
        }
    }

    protected override void ClearContainerForItemOverride(DependencyObject element, object item)
    {
        if (element is NavigatorListItem row)
            row.Invoked -= RowInvoked;
        base.ClearContainerForItemOverride(element, item);
    }

    private void RowInvoked(NavigatorListItem row)
    {
        // A retained native button must not revive an obsolete list container.
        if (IsLoaded && row.Content is LayoutItem item && ReferenceEquals(ContainerFromItem(item), row))
            ItemInvoked?.Invoke(item);
    }

    internal void Configure(DockPalette palette, Brush surface)
    {
        var changed = _palette != palette || !ReferenceEquals(_surface, surface);
        _palette = palette;
        _surface = surface;
        Background = surface;
        Foreground = palette.Foreground;
        FontSize = palette.FontSize;
        if (!changed)
            return;
        foreach (var row in this.FindVisualChildren<NavigatorListItem>())
            row.Configure(palette, surface);
    }
}
