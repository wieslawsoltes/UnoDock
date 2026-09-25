using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Input;
using UnoDock.Compatibility;
using UnoDock.Internal;

namespace UnoDock.Controls;
/// <summary>Flyout container with independent data-source and container extension points.</summary>
public class ContextMenuEx : MenuFlyout
{
    public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(nameof(ItemsSource), typeof(IEnumerable), typeof(ContextMenuEx), new PropertyMetadata(null));
    public static readonly DependencyProperty MenuDataContextProperty = DependencyProperty.Register(nameof(MenuDataContext), typeof(object), typeof(ContextMenuEx), new PropertyMetadata(null, (d, e) =>
    {
        var menu = (ContextMenuEx)d;
        MenuContext.Apply(menu, menu.MenuDataContext);
    }));
    public IEnumerable? ItemsSource
    {
        get => (IEnumerable?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }
    public object? MenuDataContext
    {
        get => GetValue(MenuDataContextProperty);
        set => SetValue(MenuDataContextProperty, value);
    }

    public ContextMenuEx()
    {
        Opening += (_, _) => PrepareItems();
        Opened += (_, _) => OnOpened(new RoutedEventArgs());
        Closed += (_, _) => MenuContext.Clear(this);
    }

    public void PrepareItems()
    {
        if (ItemsSource != null)
        {
            // Snapshot before replacing: an application iterator can fail without destroying the old menu.
            var items = ItemsSource.Cast<object?>().Select(value => value as MenuFlyoutItemBase ?? CreateItem(value)).ToArray();
            Items.Clear();
            foreach (var item in items)
                Items.Add(item);
        }

        if (MenuDataContext != null)
            MenuContext.Apply(this, MenuDataContext);
    }

    private MenuFlyoutItemBase CreateItem(object? value)
    {
        var item = GetContainerForItemOverride() as MenuFlyoutItem ?? throw new InvalidOperationException("The menu item factory must return a MenuFlyoutItem.");
        item.DataContext = value;
        item.Text = value?.ToString() ?? "";
        if (value is ICommand command)
            item.Command = command;
        return item;
    }

    protected virtual void OnOpened(RoutedEventArgs e)
    {
    }

    protected virtual DependencyObject GetContainerForItemOverride() => new MenuItemEx();
}
