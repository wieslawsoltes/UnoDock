using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Input;
using UnoDock.Internal;
using UnoDock.Layout;
using UnoDock.Compatibility;

namespace UnoDock.Controls;

public abstract partial class LayoutTabItemBase : DockInputControl
{
    public static readonly DependencyProperty ModelProperty = DependencyProperty.Register(nameof(Model), typeof(LayoutContent), typeof(LayoutTabItemBase), new PropertyMetadata(null, (d, e) => ((LayoutTabItemBase)d).OnModelChanged(e)));
    public static readonly DependencyProperty LayoutItemProperty = DependencyProperty.Register(nameof(LayoutItem), typeof(LayoutItem), typeof(LayoutTabItemBase), new PropertyMetadata(null));
    private readonly Grid _chrome = new();
    private readonly DockChromeButton _label;
    private readonly Image _icon = new() { Width = 16, Height = 16, Margin = new Thickness(0, 0, 3, 0), Visibility = Visibility.Collapsed };
    private readonly ContentPresenter _header = new() { VerticalAlignment = VerticalAlignment.Center };
    private readonly DockChromeButton _close;
    private DockingManager? _manager;
    public LayoutContent? Model { get => (LayoutContent?)GetValue(ModelProperty); set => SetValue(ModelProperty, value); }
    public LayoutItem? LayoutItem => (LayoutItem?)GetValue(LayoutItemProperty);
    protected LayoutTabItemBase()
    {
        IsTabStop = false; Padding = new(0); Margin = new(0); Height = 20;
        HorizontalContentAlignment = HorizontalAlignment.Stretch; VerticalContentAlignment = VerticalAlignment.Stretch;
        _chrome.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) }); _chrome.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        _label = DockChrome.Button("", ActivateFromKeyboard); var header = new Grid(); header.ColumnDefinitions.Add(new() { Width = GridLength.Auto }); header.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) });
        header.Children.Add(_icon); Grid.SetColumn(_header, 1); header.Children.Add(_header); _label.Content = header;
        _label.MinWidth = 44; _label.MaxWidth = 260; _label.Padding = new Thickness(1, 0, 1, 0); _label.HorizontalContentAlignment = HorizontalAlignment.Left; _label.HorizontalAlignment = HorizontalAlignment.Stretch; _label.VerticalAlignment = VerticalAlignment.Stretch;
        _label.DoubleTapped += (_, _) => { if (Model?.IsFloating == true) Model.Dock(); else Model?.Float(); };
        _close = DockChrome.Icon(DockGlyph.Close, () => { if (Model != null) DockVisuals.CloseOrHide(Model); }, "Close tab");
        Grid.SetColumn(_close, 1); _chrome.Children.Add(_label); _chrome.Children.Add(_close); Content = _chrome; InitializeTabAutomation();
    }
    protected virtual void OnModelChanged(DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is LayoutContent old) old.PropertyChanged -= ModelChanged;
        if (e.NewValue is LayoutContent model) model.PropertyChanged += ModelChanged;
        if (Model?.Root?.Manager is { } manager) Update(manager);
        QueueAutomationRefresh();
    }
    protected void SetLayoutItem(LayoutItem value) => SetValue(LayoutItemProperty, value);
    private void ModelChanged(object? sender, PropertyChangedEventArgs e) { if (_manager != null) Update(_manager); QueueAutomationRefresh(); }
    internal override FrameworkElement DockCaptureElement => _label;
    private void ActivateFromKeyboard()
    {
        // Pointer activation occurs in the overrideable press path. A Button.Click
        // on release must not bypass a subclass that vetoed that path.
        if (_label.FocusState != FocusState.Pointer && Model is { IsEnabled: true } model) model.IsActive = true;
    }
    protected override bool AcceptsPointerEvent(PointerRoutedEventArgs e)
    {
        for (var current = e.OriginalSource as DependencyObject; current != null && !ReferenceEquals(current, this); current = VisualTreeHelper.GetParent(current))
            if (ReferenceEquals(current, _close)) return false;
        return true;
    }
    protected override void OnMouseDown(DockMouseButtonEventArgs e)
    {
        if (!e.Handled && Model is { IsEnabled: true } model && e.ChangedButton == DockMouseButton.Middle)
        { DockVisuals.CloseOrHide(model); e.Handled = true; return; }
        base.OnMouseDown(e);
    }
    protected override void OnMouseLeftButtonDown(DockMouseButtonEventArgs e)
    {
        if (!e.Handled && Model is { IsEnabled: true } model)
        { model.IsActive = true; model.Root?.Manager?.BeginDrag(model, this, e.NativeEvent); }
        base.OnMouseLeftButtonDown(e);
    }
    protected override void OnMouseRightButtonDown(DockMouseButtonEventArgs e)
    { if (!e.Handled && Model is { IsEnabled: true } model) model.IsActive = true; base.OnMouseRightButtonDown(e); }
    protected override void OnMouseEnter(DockMouseEventArgs e)
    { if (!e.Handled) VisualStateManager.GoToState(this, "PointerOver", false); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(DockMouseEventArgs e)
    { if (!e.Handled) VisualStateManager.GoToState(this, "Normal", false); base.OnMouseLeave(e); }
    protected override void OnMouseMove(DockMouseEventArgs e) => base.OnMouseMove(e);
    protected override void OnMouseLeftButtonUp(DockMouseButtonEventArgs e) => base.OnMouseLeftButtonUp(e);
    internal void Update(DockingManager manager)
    {
        _manager = manager; if (Model == null) return;
        SetLayoutItem(manager.GetLayoutItemFromModel(Model));
        var template = manager.HeaderTemplate(Model, this);
        _header.ContentTemplate = template; _header.Content = template == null ? Model.Title : Model;
        _icon.Source = template == null ? Model.IconSource as ImageSource : null;
        _icon.Visibility = _icon.Source == null ? Visibility.Collapsed : Visibility.Visible;
        _label.IsEnabled = Model.IsEnabled;
        _close.IsEnabled = Model.IsEnabled;
        var palette = DockChrome.Palette(manager);
        var tool = Model is LayoutAnchorable && Model.Parent is not LayoutDocumentPane;
        _label.Configure(palette); _close.Configure(palette);
        _label.FontWeight = Microsoft.UI.Text.FontWeights.Normal;
        _close.Visibility = !tool && Model.CanClose && Model.IsSelected ? Visibility.Visible : Visibility.Collapsed;
        _chrome.Background = Model.IsSelected ? palette.Surface : palette.Tab;
        _chrome.BorderBrush = palette.Border;
        _chrome.BorderThickness = tool ? new(0, 0, 1, 0) : new(0, 0, 1, 0);
        _chrome.Padding = new(tool ? 3 : 0, 0, tool ? 3 : 0, 0);
        MinHeight = 0; Height = tool ? palette.ToolTabHeight - 2 : palette.TabHeight - 1;
        DockVisuals.SetName(_label, Model.Title ?? "Document");
        ToolTipService.SetToolTip(_label, Model.ToolTip ?? Model.Title);
        MenuContext.SetTarget(this, Model);
        ContextFlyout = DockVisuals.Menu(manager, Model);
    }
    internal void FocusLabel() => _label.Focus(FocusState.Keyboard);
    protected override Microsoft.UI.Xaml.Automation.Peers.AutomationPeer OnCreateAutomationPeer() => new LayoutTabAutomationPeer(this);
    protected override void OnKeyDown(KeyRoutedEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || InputState.ControlDown || Model == null) return;
        if (e.Key is Windows.System.VirtualKey.Left or Windows.System.VirtualKey.Right or Windows.System.VirtualKey.Home or Windows.System.VirtualKey.End)
        {
            this.FindVisualAncestor<LayoutCachePaneControl>()?.NavigateHeader(Model, e.Key);
            e.Handled = true;
        }
    }
    internal void DetachModel() { MenuContext.SetTarget(this, null); Model = null; _manager = null; ClearValue(LayoutItemProperty); }
}
