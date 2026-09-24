using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Input;
using UnoDock.Internal;
using UnoDock.Layout;
using UnoDock.Compatibility;

namespace UnoDock.Controls;

/// <summary>Uno tab host. Content presenters are keyed by model identity and survive tab selection and movement.</summary>
public partial class LayoutCachePaneControl : DockSelectionControl
{
    private readonly Grid _layout = new();
    private readonly Grid _content = new();
    private Panel _headers = new DocumentPaneTabPanel();
    private readonly ScrollViewer _scroll;
    private readonly Grid _titleRow = new();
    private readonly TextBlock _title = new() { Margin = new Thickness(10, 5, 4, 5), VerticalAlignment = VerticalAlignment.Center };
    private readonly Dictionary<LayoutContent, LayoutTabItemBase> _tabs = new(ReferenceEqualityComparer.Instance);
    protected ILayoutContentSelector? Selector { get; private set; }
    protected ILayoutGroup? Pane { get; private set; }
    public LayoutCachePaneControl()
    {
        HorizontalContentAlignment = HorizontalAlignment.Stretch; VerticalContentAlignment = VerticalAlignment.Stretch;
        _layout.RowDefinitions.Add(new() { Height = GridLength.Auto });
        _layout.RowDefinitions.Add(new() { Height = GridLength.Auto });
        _layout.RowDefinitions.Add(new() { Height = new(1, GridUnitType.Star) });
        _scroll = new() { Content = _headers, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled, HorizontalScrollMode = ScrollMode.Enabled, VerticalScrollMode = ScrollMode.Disabled, MaxHeight = 54 };
        Grid.SetRow(_scroll, 1); Grid.SetRow(_content, 2);
        _layout.Children.Add(_titleRow); _layout.Children.Add(_scroll); _layout.Children.Add(_content); Content = _layout;
        IsTabStop = false;
        InitializeChrome();
    }
    public IEnumerable<LayoutContent> Items => Pane?.Children.OfType<LayoutContent>() ?? [];
    internal void UpdatePane(ILayoutGroup pane, DockSurface surface)
    {
        BindPane(pane);
        if (pane is LayoutAnchorablePane && _headers is not AnchorablePaneTabPanel)
        {
            _headers.Children.Clear(); _headers = new AnchorablePaneTabPanel(); _scroll.Content = _headers;
            _scroll.HorizontalScrollMode = ScrollMode.Disabled; _scroll.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
        }
        var models = pane.Children.OfType<LayoutContent>().Where(c => c is not LayoutDocument { IsVisible: false }).ToArray();
        var selected = Selector!.SelectedContent;
        foreach (var stale in _tabs.Keys.Where(k => !models.Contains(k, ReferenceEqualityComparer.Instance)).ToArray())
        { _tabs[stale].DetachModel(); _tabs.Remove(stale); }
        var tabViews = new List<UIElement>(); var contentViews = new List<UIElement>();
        foreach (var model in models)
        {
            if (!_tabs.TryGetValue(model, out var tab))
            {
                tab = CreateTabItem(model) ?? throw new InvalidOperationException("The tab factory returned null.");
                if (VisualTreeHelper.GetParent(tab) != null || _tabs.Values.Contains(tab) || (tab.Model != null && !ReferenceEquals(tab.Model, model)))
                    throw new InvalidOperationException("The tab factory must return an unattached, unshared tab for the requested model.");
                tab.Model = model; _tabs.Add(model, tab);
            }
            tab.Update(surface.Manager); tabViews.Add(tab);
            var item = surface.Manager.GetLayoutItemFromModel(model); item.UpdateView();
            var presenter = ReferenceEquals(model, selected) ? item.View : item.ExistingView;
            if (presenter != null)
            {
                presenter.Visibility = ReferenceEquals(model, selected) ? Visibility.Visible : Visibility.Collapsed;
                contentViews.Add(presenter);
            }
        }
        VisualParenting.ReconcilePanel(_headers, tabViews); VisualParenting.ReconcilePanel(_content, contentViews);
        UpdatePaneChrome(pane, models, surface.Manager);
        UpdateTitle(pane is LayoutAnchorablePane, selected as LayoutAnchorable, surface.Manager);
        DockVisuals.SetName(this, pane is LayoutDocumentPane ? "Document tab group" : "Tool tab group");
        MenuContext.SetTarget(this, selected);
        ContextFlyout = selected == null ? null : DockVisuals.Menu(surface.Manager, selected);
        SynchronizeSelection();
        RevealSelectedHeader(selected);
    }
    /// <summary>Creates an unparented tab once per model in this pane. This is an additive Uno composition extension.</summary>
    protected virtual LayoutTabItemBase CreateTabItem(LayoutContent model) => model is LayoutAnchorable ? new LayoutAnchorableTabItem() : new LayoutDocumentTabItem();
    private LayoutAnchorable? _titleModel;
    private DockingManager? _titleManager;
    internal int InsertionIndex(Point surfacePoint, DockSurface surface)
    {
        if (IsOverHeaderElement(_titlePresenter, surfacePoint, surface)) return Pane?.ChildrenCount ?? 0;
        var index = 0;
        foreach (var model in Items)
        {
            if (!_tabs.TryGetValue(model, out var tab)) { index++; continue; }
            var bounds = DockCoordinates.Bounds(tab, new Rect(0, 0, tab.ActualWidth, tab.ActualHeight), surface, surface.Manager.CrossWindowCoordinates);
            if (FlowDirection == FlowDirection.RightToLeft ? surfacePoint.X > bounds.X + bounds.Width / 2 : surfacePoint.X < bounds.X + bounds.Width / 2) return index;
            index++;
        }
        return index;
    }
    internal bool IsOverHeader(Point point, DockSurface surface) => IsOverHeaderElement(_scroll, point, surface) || IsOverHeaderElement(_titlePresenter, point, surface);
    private static bool IsOverHeaderElement(FrameworkElement header, Point point, DockSurface surface)
    {
        if (header.ActualWidth <= 0 || header.ActualHeight <= 0) return false;
        for (DependencyObject? element = header; element != null; element = VisualTreeHelper.GetParent(element))
            if (element is UIElement { Visibility: Visibility.Collapsed }) return false;
        try
        {
            var local = DockCoordinates.Translate(surface, point, header, surface.Manager.CrossWindowCoordinates);
            return new Rect(0, 0, header.ActualWidth, header.ActualHeight).Contains(local);
        }
        catch (Exception e) when (DockCoordinates.IsUnavailable(e)) { return false; }
    }
    internal bool ScrollHeaderAt(Point point, DockSurface surface, double seconds)
    {
        if (!IsOverHeaderElement(_scroll, point, surface) || _scroll.ScrollableWidth <= 0) return false;
        var local = DockCoordinates.Translate(surface, point, _scroll, surface.Manager.CrossWindowCoordinates);
        var delta = DockInteractionGeometry.AutoScrollDelta(local.X, _scroll.ActualWidth, _scroll.HorizontalOffset,
            _scroll.ScrollableWidth, seconds, FlowDirection == FlowDirection.RightToLeft);
        return delta != 0 && _scroll.ChangeView(Math.Clamp(_scroll.HorizontalOffset + delta, 0, _scroll.ScrollableWidth), null, null, true);
    }
    internal LayoutTabItemBase? TabFor(LayoutContent model) => _tabs.GetValueOrDefault(model);
    protected override Microsoft.UI.Xaml.Automation.Peers.AutomationPeer OnCreateAutomationPeer() => new LayoutPaneAutomationPeer(this);
    internal void NavigateHeader(LayoutContent from, Windows.System.VirtualKey key)
    {
        var items = Items.Where(m => m.IsEnabled && m is not LayoutDocument { IsVisible: false }).ToArray();
        if (items.Length == 0) return;
        var index = Array.IndexOf(items, from);
        var direction = key == Windows.System.VirtualKey.Left ? -1 : 1;
        if (FlowDirection == FlowDirection.RightToLeft) direction = -direction;
        index = key == Windows.System.VirtualKey.Home ? 0 : key == Windows.System.VirtualKey.End ? items.Length - 1 : (index + direction + items.Length) % items.Length;
        items[index].IsActive = true; TabFor(items[index])?.FocusLabel();
    }
    internal void ReleaseViews()
    {
        foreach (var tab in _tabs.Values) tab.DetachModel(); _tabs.Clear();
        _headers.Children.Clear(); _content.Children.Clear(); _titleModel = null; _titleManager = null;
        _paneObserver?.Dispose(); _paneObserver = null;
        _headerGeneration++; _lastHeaderSelection = null; _lastHeaderIndex = -1;
    }
}
public class LayoutDocumentPaneControl : LayoutCachePaneControl, ILayoutControl, IRefreshableLayoutControl
{
    private readonly LayoutDocumentPane _model;
    public LayoutDocumentPaneControl(LayoutDocumentPane model) { ArgumentNullException.ThrowIfNull(model); _model = model; BindPane(model); }
    public ILayoutElement Model => _model;
    void IRefreshableLayoutControl.Update(DockSurface surface) { Style = surface.Manager.DocumentPaneControlStyle; UpdatePane(_model, surface); }
    protected override void OnMouseLeftButtonDown(DockMouseButtonEventArgs e) { if (!e.Handled) ActivateSelection(); base.OnMouseLeftButtonDown(e); }
    protected override void OnMouseRightButtonDown(DockMouseButtonEventArgs e) { if (!e.Handled) ActivateSelection(); base.OnMouseRightButtonDown(e); }
    protected override void OnSelectionChanged(SelectionChangedEventArgs e) => base.OnSelectionChanged(e);
    protected override IEnumerator LogicalChildren => base.LogicalChildren;
}
public class LayoutAnchorablePaneControl : LayoutCachePaneControl, ILayoutControl, IRefreshableLayoutControl
{
    private readonly LayoutAnchorablePane _model;
    public LayoutAnchorablePaneControl(LayoutAnchorablePane model) { ArgumentNullException.ThrowIfNull(model); _model = model; BindPane(model); }
    public ILayoutElement Model => _model;
    void IRefreshableLayoutControl.Update(DockSurface surface) { Style = surface.Manager.AnchorablePaneControlStyle; UpdatePane(_model, surface); }
    protected override void OnMouseLeftButtonDown(DockMouseButtonEventArgs e) { if (!e.Handled) ActivateSelection(); base.OnMouseLeftButtonDown(e); }
    protected override void OnMouseRightButtonDown(DockMouseButtonEventArgs e) { if (!e.Handled) ActivateSelection(); base.OnMouseRightButtonDown(e); }
    protected override void OnGotKeyboardFocus(DockKeyboardFocusChangedEventArgs e) { if (!e.Handled) ActivateSelection(); base.OnGotKeyboardFocus(e); }
}

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
public class LayoutDocumentTabItem : LayoutTabItemBase
{
    public LayoutDocumentTabItem() { }
    protected override void OnMouseDown(DockMouseButtonEventArgs e) => base.OnMouseDown(e);
    protected override void OnMouseEnter(DockMouseEventArgs e) => base.OnMouseEnter(e);
    protected override void OnMouseLeave(DockMouseEventArgs e) => base.OnMouseLeave(e);
    protected override void OnMouseLeftButtonDown(DockMouseButtonEventArgs e) => base.OnMouseLeftButtonDown(e);
    protected override void OnMouseLeftButtonUp(DockMouseButtonEventArgs e) => base.OnMouseLeftButtonUp(e);
    protected override void OnMouseMove(DockMouseEventArgs e) => base.OnMouseMove(e);
}
public class LayoutAnchorableTabItem : LayoutTabItemBase
{
    public LayoutAnchorableTabItem() { }
    protected override void OnMouseEnter(DockMouseEventArgs e) => base.OnMouseEnter(e);
    protected override void OnMouseLeave(DockMouseEventArgs e) => base.OnMouseLeave(e);
    protected override void OnMouseLeftButtonDown(DockMouseButtonEventArgs e) => base.OnMouseLeftButtonDown(e);
    protected override void OnMouseLeftButtonUp(DockMouseButtonEventArgs e) => base.OnMouseLeftButtonUp(e);
    protected override void OnMouseMove(DockMouseEventArgs e) => base.OnMouseMove(e);
}

public class LayoutDocumentControl : DockInputControl
{
    public static readonly DependencyProperty ModelProperty = DependencyProperty.Register(nameof(Model), typeof(LayoutContent), typeof(LayoutDocumentControl), new PropertyMetadata(null, (d, e) => ((LayoutDocumentControl)d).OnModelChanged(e)));
    public static readonly DependencyProperty LayoutItemProperty = DependencyProperty.Register(nameof(LayoutItem), typeof(LayoutItem), typeof(LayoutDocumentControl), new PropertyMetadata(null));
    public LayoutContent? Model { get => (LayoutContent?)GetValue(ModelProperty); set => SetValue(ModelProperty, value); }
    public LayoutItem? LayoutItem => (LayoutItem?)GetValue(LayoutItemProperty);
    public LayoutDocumentControl() { HorizontalContentAlignment = HorizontalAlignment.Stretch; VerticalContentAlignment = VerticalAlignment.Stretch; }
    protected virtual void OnModelChanged(DependencyPropertyChangedEventArgs e)
    {
        if (Model?.Root?.Manager is { } manager)
        { var item = manager.GetLayoutItemFromModel(Model); SetLayoutItem(item); VisualParenting.Detach(item.View); Content = item.View; }
        else { ClearValue(LayoutItemProperty); Content = null; }
    }
    protected void SetLayoutItem(LayoutItem value) => SetValue(LayoutItemProperty, value);
    protected override void OnPreviewMouseLeftButtonDown(DockMouseButtonEventArgs e)
    { if (!e.Handled) ActivateModel(); base.OnPreviewMouseLeftButtonDown(e); }
    protected override void OnPreviewMouseRightButtonDown(DockMouseButtonEventArgs e)
    { if (!e.Handled) ActivateModel(); base.OnPreviewMouseRightButtonDown(e); }
    protected override void OnPreviewGotKeyboardFocus(DockKeyboardFocusChangedEventArgs e)
    { base.OnPreviewGotKeyboardFocus(e); }
    protected override void OnGotKeyboardFocus(DockKeyboardFocusChangedEventArgs e)
    { if (!e.Handled) ActivateModel(); base.OnGotKeyboardFocus(e); }
    private void ActivateModel() { if (Model is { IsEnabled: true, Root: not null } model) model.IsActive = true; }
}
public class LayoutAnchorableControl : LayoutDocumentControl
{
    public new LayoutAnchorable? Model { get => base.Model as LayoutAnchorable; set => base.Model = value; }
    public LayoutAnchorableControl() { }
    protected override void OnGotKeyboardFocus(DockKeyboardFocusChangedEventArgs e) => base.OnGotKeyboardFocus(e);
}
public class AnchorablePaneTitle : LayoutAnchorableTabItem
{
    public new LayoutAnchorable? Model { get => base.Model as LayoutAnchorable; set => base.Model = value; }
    public AnchorablePaneTitle() { }
    protected override void OnMouseLeave(DockMouseEventArgs e) => base.OnMouseLeave(e);
    protected override void OnMouseLeftButtonDown(DockMouseButtonEventArgs e) => base.OnMouseLeftButtonDown(e);
    protected override void OnMouseLeftButtonUp(DockMouseButtonEventArgs e) => base.OnMouseLeftButtonUp(e);
    protected override void OnMouseMove(DockMouseEventArgs e) => base.OnMouseMove(e);
}
