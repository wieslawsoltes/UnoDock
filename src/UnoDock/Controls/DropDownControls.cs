using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Input;
using UnoDock.Compatibility;
using UnoDock.Internal;

namespace UnoDock.Controls;

public class DropDownButton : ToggleButton
{
    public static readonly DependencyProperty DropDownContextMenuProperty = DependencyProperty.Register(nameof(DropDownContextMenu), typeof(MenuFlyout), typeof(DropDownButton), new PropertyMetadata(null, (d, e) => ((DropDownButton)d).MenuChanged(e)));
    public static readonly DependencyProperty DropDownContextMenuDataContextProperty = DependencyProperty.Register(nameof(DropDownContextMenuDataContext), typeof(object), typeof(DropDownButton), new PropertyMetadata(null, (d, _) => ((DropDownButton)d)._session?.Refresh()));
    private readonly DropDownMenuSession _session;
    private bool _syncChecked;
    public DropDownButton()
    {
        _session = new(this, () => DropDownContextMenu, () => DropDownContextMenuDataContext ?? DataContext, SynchronizeChecked);
        Click += (_, _) => { try { OnClick(); } finally { SynchronizeChecked(_session.IsOpen); } };
        Unloaded += (_, _) => _session.Close();
        IsEnabledChanged += (_, _) => { if (!IsEnabled) _session.Close(); };
        DataContextChanged += (_, _) => _session.Refresh();
    }
    public MenuFlyout? DropDownContextMenu { get => (MenuFlyout?)GetValue(DropDownContextMenuProperty); set => SetValue(DropDownContextMenuProperty, value); }
    public object? DropDownContextMenuDataContext { get => GetValue(DropDownContextMenuDataContextProperty); set => SetValue(DropDownContextMenuDataContextProperty, value); }
    /// <summary>Shows the configured menu through the same lifetime as a native click.</summary>
    public void OpenDropDown() => _session.Open();
    /// <summary>Closes this trigger's current opening, not a menu subsequently owned by another trigger.</summary>
    public void CloseDropDown() => _session.Close();
    protected virtual void OnDropDownContextMenuChanged(DependencyPropertyChangedEventArgs e) { }
    private void MenuChanged(DependencyPropertyChangedEventArgs e)
    {
        // Cancel before the override can install or open a replacement menu.
        _session?.Close(); OnDropDownContextMenuChanged(e);
    }
    protected virtual void OnClick()
    {
        if (_session.IsRequested) _session.Close(); else _session.Open();
    }
    private void SynchronizeChecked(bool value)
    {
        if (_syncChecked || IsChecked == value) return;
        _syncChecked = true;
        try { IsChecked = value; }
        finally { _syncChecked = false; }
        // Checked callbacks may explicitly reject opening after the DP was set.
        if (value && IsChecked != true) _session.Close();
    }
    protected override void OnKeyDown(KeyRoutedEventArgs e)
    {
        base.OnKeyDown(e);
        if (!e.Handled && e.Key == Windows.System.VirtualKey.Escape && _session.IsRequested)
        { _session.Close(); e.Handled = true; }
    }
}

public class DropDownControlArea : UserControl
{
    public static readonly DependencyProperty DropDownContextMenuProperty = DependencyProperty.Register(nameof(DropDownContextMenu), typeof(MenuFlyout), typeof(DropDownControlArea), new PropertyMetadata(null, (d, _) => ((DropDownControlArea)d)._session?.Close()));
    public static readonly DependencyProperty DropDownContextMenuDataContextProperty = DependencyProperty.Register(nameof(DropDownContextMenuDataContext), typeof(object), typeof(DropDownControlArea), new PropertyMetadata(null, (d, _) => ((DropDownControlArea)d)._session?.Refresh()));
    private readonly DropDownMenuSession _session;
    private uint? _rightPointer;
    private bool _downHandled, _suppressMouseRightTap;
    private long _inputGeneration;
    public MenuFlyout? DropDownContextMenu { get => (MenuFlyout?)GetValue(DropDownContextMenuProperty); set => SetValue(DropDownContextMenuProperty, value); }
    public object? DropDownContextMenuDataContext { get => GetValue(DropDownContextMenuDataContextProperty); set => SetValue(DropDownContextMenuDataContextProperty, value); }
    public DropDownControlArea()
    {
        _session = new(this, () => DropDownContextMenu, () => DropDownContextMenuDataContext ?? DataContext, _ => { });
        Unloaded += (_, _) => { ResetPointer(); _session.Close(); };
        IsEnabledChanged += (_, _) => { if (!IsEnabled) { ResetPointer(); _session.Close(); } };
        DataContextChanged += (_, _) => _session.Refresh();
        AddHandler(PointerPressedEvent, new PointerEventHandler(RightPressed), true);
        AddHandler(PointerReleasedEvent, new PointerEventHandler(RightReleased), true);
        AddHandler(PointerMovedEvent, new PointerEventHandler(ButtonTransition), true);
        ContextRequested += (_, e) =>
        {
            if (e.Handled || !IsEnabled) return;
            if (e.TryGetPosition(this, out var position))
            {
                if (_suppressMouseRightTap) { e.Handled = true; return; }
                _session.Open(position);
            }
            else _session.Open();
            if (_session.IsRequested) e.Handled = true;
        };
        PointerCanceled += (_, _) => ResetPointer();
        PointerCaptureLost += (_, e) => { if (ReferenceEquals(e.OriginalSource, this)) ResetPointer(); };
    }
    public void OpenDropDown(Point? position = null) => _session.Open(position);
    public void CloseDropDown() => _session.Close();
    /// <summary>Local compatibility stage over the original native right-button press.</summary>
    protected virtual void OnMouseRightButtonDown(DockMouseButtonEventArgs e) { }
    /// <summary>Local compatibility stage, not a synthetic WPF tunnel. Mark Handled
    /// or omit the base call to suppress the default context-menu opening.</summary>
    protected virtual void OnPreviewMouseRightButtonUp(DockMouseButtonEventArgs e)
    {
        if (e.Handled) return;
        _session.Open(e.GetPosition(this));
        if (_session.IsRequested) e.Handled = true;
    }
    private void RightPressed(object sender, PointerRoutedEventArgs e)
    {
        if (!IsEnabled || e.Handled || e.GetCurrentPoint(this).Properties.PointerUpdateKind != PointerUpdateKind.RightButtonPressed) return;
        _inputGeneration++; _rightPointer = e.Pointer.PointerId;
        _suppressMouseRightTap = true; _downHandled = false;
        var args = new DockMouseButtonEventArgs(e, this, DockMouseButton.Right, true);
        try { OnMouseRightButtonDown(args); _downHandled = args.Handled; }
        catch { ResetPointer(); throw; }
        finally { args.Complete(); }
    }
    private void RightReleased(object sender, PointerRoutedEventArgs e)
    {
        if (_rightPointer != e.Pointer.PointerId || e.GetCurrentPoint(this).Properties.PointerUpdateKind != PointerUpdateKind.RightButtonReleased) return;
        _rightPointer = null;
        var generation = _inputGeneration;
        var args = new DockMouseButtonEventArgs(e, this, DockMouseButton.Right, false) { Handled = _downHandled || e.Handled };
        try { if (IsEnabled) OnPreviewMouseRightButtonUp(args); }
        finally
        {
            args.Complete();
            // Mouse RightTapped can arrive before or after PointerReleased. The
            // actual right-button protocol above is its only opening authority.
            DispatcherQueue.TryEnqueue(() => { if (_inputGeneration == generation) _suppressMouseRightTap = false; });
        }
    }
    private void ButtonTransition(object sender, PointerRoutedEventArgs e)
    {
        var kind = e.GetCurrentPoint(this).Properties.PointerUpdateKind;
        if (kind == PointerUpdateKind.RightButtonPressed) RightPressed(sender, e);
        else if (kind == PointerUpdateKind.RightButtonReleased) RightReleased(sender, e);
    }
    private void ResetPointer() { _inputGeneration++; _rightPointer = null; _downHandled = false; _suppressMouseRightTap = false; }
    protected override void OnRightTapped(RightTappedRoutedEventArgs e)
    {
        base.OnRightTapped(e);
        if (e.Handled || !IsEnabled) return;
        if (e.PointerDeviceType == PointerDeviceType.Mouse && _suppressMouseRightTap) { e.Handled = true; return; }
        _session.Open(e.GetPosition(this));
        if (_session.IsRequested) e.Handled = true;
    }
    protected override void OnKeyDown(KeyRoutedEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled) return;
        if (e.Key == Windows.System.VirtualKey.Escape && _session.IsRequested)
        { _session.Close(); e.Handled = true; return; }
        if (DropDownKeyboard.IsContextRequest(this, e))
        { _session.Open(); if (_session.IsRequested) e.Handled = true; }
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
