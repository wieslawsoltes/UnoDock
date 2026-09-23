using Microsoft.UI.Xaml.Input;
using Xceed.Wpf.AvalonDock.Internal;

namespace Xceed.Wpf.AvalonDock.Controls;

public class DropDownButton : ToggleButton
{
    public static readonly DependencyProperty DropDownContextMenuProperty = DependencyProperty.Register(nameof(DropDownContextMenu), typeof(MenuFlyout), typeof(DropDownButton), new PropertyMetadata(null, (d, e) => ((DropDownButton)d).MenuChanged(e)));
    public static readonly DependencyProperty DropDownContextMenuDataContextProperty = DependencyProperty.Register(nameof(DropDownContextMenuDataContext), typeof(object), typeof(DropDownButton), new PropertyMetadata(null));
    private MenuFlyout? _openMenu;
    public DropDownButton() { Click += (_, _) => OnClick(); Unloaded += (_, _) => CloseMenu(); }
    public MenuFlyout? DropDownContextMenu { get => (MenuFlyout?)GetValue(DropDownContextMenuProperty); set => SetValue(DropDownContextMenuProperty, value); }
    public object? DropDownContextMenuDataContext { get => GetValue(DropDownContextMenuDataContextProperty); set => SetValue(DropDownContextMenuDataContextProperty, value); }
    protected virtual void OnDropDownContextMenuChanged(DependencyPropertyChangedEventArgs e) { }
    private void MenuChanged(DependencyPropertyChangedEventArgs e) { CloseMenu(); OnDropDownContextMenuChanged(e); }
    protected virtual void OnClick()
    {
        var wasOpen = _openMenu != null;
        if (wasOpen) { CloseMenu(); return; }
        if (DropDownContextMenu is not { } menu) { IsChecked = false; return; }
        MenuContext.Apply(menu, DropDownContextMenuDataContext ?? DataContext);
        _openMenu = menu; menu.Closed += MenuClosed;
        try { menu.ShowAt(this); IsChecked = true; }
        catch { MenuClosed(menu, EventArgs.Empty); throw; }
    }
    private void MenuClosed(object? sender, object args)
    {
        if (_openMenu is not { } menu) return;
        _openMenu = null; menu.Closed -= MenuClosed; MenuContext.Clear(menu); IsChecked = false;
    }
    private void CloseMenu() { var menu = _openMenu; if (menu == null) return; MenuClosed(menu, EventArgs.Empty); menu.Hide(); }
}

public class DropDownControlArea : UserControl
{
    public static readonly DependencyProperty DropDownContextMenuProperty = DependencyProperty.Register(nameof(DropDownContextMenu), typeof(MenuFlyout), typeof(DropDownControlArea), new PropertyMetadata(null));
    public static readonly DependencyProperty DropDownContextMenuDataContextProperty = DependencyProperty.Register(nameof(DropDownContextMenuDataContext), typeof(object), typeof(DropDownControlArea), new PropertyMetadata(null));
    public MenuFlyout? DropDownContextMenu { get => (MenuFlyout?)GetValue(DropDownContextMenuProperty); set => SetValue(DropDownContextMenuProperty, value); }
    public object? DropDownContextMenuDataContext { get => GetValue(DropDownContextMenuDataContextProperty); set => SetValue(DropDownContextMenuDataContextProperty, value); }
    private MenuFlyout? _openMenu;
    public DropDownControlArea() => Unloaded += (_, _) => CloseMenu();
    private void Closed(object? sender, object args)
    {
        if (_openMenu is not { } menu) return;
        _openMenu = null; menu.Closed -= Closed; MenuContext.Clear(menu);
    }
    private void CloseMenu() { var menu = _openMenu; if (menu == null) return; Closed(menu, EventArgs.Empty); menu.Hide(); }
    protected override void OnRightTapped(RightTappedRoutedEventArgs e)
    {
        base.OnRightTapped(e);
        if (e.Handled || DropDownContextMenu is not { } menu) return;
        CloseMenu(); MenuContext.Apply(menu, DropDownContextMenuDataContext ?? DataContext);
        _openMenu = menu; menu.Closed += Closed;
        try { menu.ShowAt(this, new FlyoutShowOptions { Position = e.GetPosition(this) }); e.Handled = true; }
        catch { Closed(menu, EventArgs.Empty); throw; }
    }
}

/// <summary>Flyout container with independent data-source and container extension points.</summary>
public class ContextMenuEx : MenuFlyout
{
    public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(nameof(ItemsSource), typeof(IEnumerable), typeof(ContextMenuEx), new PropertyMetadata(null));
    public static readonly DependencyProperty MenuDataContextProperty = DependencyProperty.Register(nameof(MenuDataContext), typeof(object), typeof(ContextMenuEx), new PropertyMetadata(null, (d, e) => { var menu = (ContextMenuEx)d; MenuContext.Apply(menu, menu.MenuDataContext); }));
    public IEnumerable? ItemsSource { get => (IEnumerable?)GetValue(ItemsSourceProperty); set => SetValue(ItemsSourceProperty, value); }
    public object? MenuDataContext { get => GetValue(MenuDataContextProperty); set => SetValue(MenuDataContextProperty, value); }
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
            Items.Clear(); foreach (var item in items) Items.Add(item);
        }
        if (MenuDataContext != null) MenuContext.Apply(this, MenuDataContext);
    }
    private MenuFlyoutItemBase CreateItem(object? value)
    {
        var item = GetContainerForItemOverride() as MenuFlyoutItem ?? throw new InvalidOperationException("The menu item factory must return a MenuFlyoutItem.");
        item.DataContext = value; item.Text = value?.ToString() ?? "";
        if (value is ICommand command) item.Command = command;
        return item;
    }
    protected virtual void OnOpened(RoutedEventArgs e) { }
    protected virtual DependencyObject GetContainerForItemOverride() => new MenuItemEx();
}

public class MenuItemEx : MenuFlyoutItem
{
    public static readonly DependencyProperty IconTemplateProperty = DependencyProperty.Register(nameof(IconTemplate), typeof(DataTemplate), typeof(MenuItemEx), new PropertyMetadata(null, (d, e) => ((MenuItemEx)d).OnIconTemplateChanged(e)));
    public static readonly DependencyProperty IconTemplateSelectorProperty = DependencyProperty.Register(nameof(IconTemplateSelector), typeof(DataTemplateSelector), typeof(MenuItemEx), new PropertyMetadata(null, (d, e) => ((MenuItemEx)d).OnIconTemplateSelectorChanged(e)));
    private DataTemplate? _appliedTemplate;
    private IconElement? _generatedIcon;
    public DataTemplate? IconTemplate { get => (DataTemplate?)GetValue(IconTemplateProperty); set => SetValue(IconTemplateProperty, value); }
    public DataTemplateSelector? IconTemplateSelector { get => (DataTemplateSelector?)GetValue(IconTemplateSelectorProperty); set => SetValue(IconTemplateSelectorProperty, value); }
    public MenuItemEx() => DataContextChanged += (_, _) => UpdateIcon();
    protected virtual void OnIconTemplateChanged(DependencyPropertyChangedEventArgs e) => UpdateIcon();
    protected virtual void OnIconTemplateSelectorChanged(DependencyPropertyChangedEventArgs e) => UpdateIcon();
    private void UpdateIcon()
    {
        var template = IconTemplateSelector?.SelectTemplate(DataContext, this) ?? IconTemplate;
        if (!ReferenceEquals(template, _appliedTemplate))
        {
            var icon = template?.LoadContent();
            if (icon != null && icon is not IconElement) throw new InvalidOperationException("A WinUI menu icon template must produce an IconElement (for example FontIcon or PathIcon).");
            if (template != null || ReferenceEquals(Icon, _generatedIcon)) Icon = (IconElement?)icon;
            _generatedIcon = icon as IconElement; _appliedTemplate = template;
        }
        if (_generatedIcon != null) _generatedIcon.DataContext = DataContext;
    }
}
