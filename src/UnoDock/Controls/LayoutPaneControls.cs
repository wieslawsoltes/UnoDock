using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Input;
using Xceed.Wpf.AvalonDock.Internal;
using Xceed.Wpf.AvalonDock.Layout;
#if WINDOWS
using DockPointerDeviceType = Microsoft.UI.Input.PointerDeviceType;
#else
using DockPointerDeviceType = Windows.Devices.Input.PointerDeviceType;
#endif

namespace Xceed.Wpf.AvalonDock.Controls;

/// <summary>Uno tab host. Content presenters are keyed by model identity and survive tab selection and movement.</summary>
public class LayoutCachePaneControl : ContentControl
{
    private readonly Grid _layout = new();
    private readonly Grid _content = new();
    private readonly StackPanel _headers = new() { Orientation = Orientation.Horizontal, Spacing = 1 };
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
    }
    public int SelectedIndex { get => Selector?.SelectedContentIndex ?? -1; set { if (Selector != null) Selector.SelectedContentIndex = value; } }
    public object? SelectedItem { get => Selector?.SelectedContent; set { if (value is LayoutContent c && Pane != null && Selector != null) Selector.SelectedContentIndex = Pane.IndexOfChild(c); } }
    public IEnumerable<LayoutContent> Items => Pane?.Children.OfType<LayoutContent>() ?? [];
    internal void UpdatePane(ILayoutGroup pane, DockSurface surface)
    {
        Pane = pane; Selector = (ILayoutContentSelector)pane;
        var models = pane.Children.OfType<LayoutContent>().Where(c => c is not LayoutDocument { IsVisible: false }).ToArray();
        var selected = Selector.SelectedContent;
        foreach (var stale in _tabs.Keys.Where(k => !models.Contains(k, ReferenceEqualityComparer.Instance)).ToArray())
        { _tabs[stale].DetachModel(); _tabs.Remove(stale); }
        var tabViews = new List<UIElement>(); var contentViews = new List<UIElement>();
        foreach (var model in models)
        {
            if (!_tabs.TryGetValue(model, out var tab))
            { tab = model is LayoutAnchorable ? new LayoutAnchorableTabItem() : new LayoutDocumentTabItem(); tab.Model = model; _tabs.Add(model, tab); }
            tab.Update(surface.Manager); tabViews.Add(tab);
            var item = surface.Manager.GetLayoutItemFromModel(model); item.UpdateView();
            item.View.Visibility = ReferenceEquals(model, selected) ? Visibility.Visible : Visibility.Collapsed;
            contentViews.Add(item.View);
        }
        VisualParenting.ReconcilePanel(_headers, tabViews); VisualParenting.ReconcilePanel(_content, contentViews);
        _scroll.Visibility = pane is LayoutDocumentPane { ShowHeader: false } || models.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
        _layout.Background = DockVisuals.Brush(surface.Manager, "UnoDock.PaneBrush", "LayerFillColorDefaultBrush");
        _headers.Background = DockVisuals.Brush(surface.Manager, "UnoDock.HeaderBrush", "ControlFillColorSecondaryBrush");
        UpdateTitle(pane is LayoutAnchorablePane, selected as LayoutAnchorable, surface.Manager);
        DockVisuals.SetName(this, pane is LayoutDocumentPane ? "Document tab group" : "Tool tab group");
        ContextFlyout = selected == null ? null : DockVisuals.Menu(surface.Manager, selected);
    }
    private LayoutAnchorable? _titleModel;
    private DockingManager? _titleManager;
    private void UpdateTitle(bool visible, LayoutAnchorable? selected, DockingManager manager)
    {
        _titleRow.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        _title.Text = selected?.Title ?? "Tools";
        _titleModel = selected; _titleManager = manager;
        if (_titleRow.Children.Count != 0) return;
        _titleRow.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) }); _titleRow.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        _titleRow.Children.Add(_title);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2 };
        actions.Children.Add(DockVisuals.Button("↗", () => _titleModel?.Float(), "Float tool"));
        actions.Children.Add(DockVisuals.Button("◇", () => _titleModel?.ToggleAutoHide(), "Toggle tool auto-hide"));
        actions.Children.Add(DockVisuals.Button("×", () => { if (_titleModel != null) DockVisuals.CloseOrHide(_titleModel); }, "Hide or close tool"));
        Grid.SetColumn(actions, 1); _titleRow.Children.Add(actions);
        _title.PointerPressed += (_, e) => { if (_titleModel != null) _titleManager?.BeginDrag(_titleModel, _title, e); };
        _title.DoubleTapped += (_, _) => { if (_titleModel?.IsFloating == true) _titleModel.Dock(); else _titleModel?.Float(); };
    }
    internal int InsertionIndex(Point surfacePoint, DockSurface surface)
    {
        var index = 0;
        foreach (var model in Items)
        {
            if (!_tabs.TryGetValue(model, out var tab)) continue;
            var bounds = DockVisuals.Bounds(tab, surface);
            if (surfacePoint.X < bounds.X + bounds.Width / 2) return index;
            index++;
        }
        return index;
    }
    internal void ReleaseViews()
    {
        foreach (var tab in _tabs.Values) tab.DetachModel(); _tabs.Clear();
        _headers.Children.Clear(); _content.Children.Clear(); _titleModel = null; _titleManager = null;
    }
}
public class LayoutDocumentPaneControl(LayoutDocumentPane model) : LayoutCachePaneControl, ILayoutControl, IRefreshableLayoutControl
{
    public ILayoutElement Model => model;
    void IRefreshableLayoutControl.Update(DockSurface surface) { Style = surface.Manager.DocumentPaneControlStyle; UpdatePane(model, surface); }
}
public class LayoutAnchorablePaneControl(LayoutAnchorablePane model) : LayoutCachePaneControl, ILayoutControl, IRefreshableLayoutControl
{
    public ILayoutElement Model => model;
    void IRefreshableLayoutControl.Update(DockSurface surface) { Style = surface.Manager.AnchorablePaneControlStyle; UpdatePane(model, surface); }
}

public abstract class LayoutTabItemBase : ContentControl
{
    public static readonly DependencyProperty ModelProperty = DependencyProperty.Register(nameof(Model), typeof(LayoutContent), typeof(LayoutTabItemBase), new PropertyMetadata(null, (d, e) => ((LayoutTabItemBase)d).OnModelChanged(e)));
    public static readonly DependencyProperty LayoutItemProperty = DependencyProperty.Register(nameof(LayoutItem), typeof(LayoutItem), typeof(LayoutTabItemBase), new PropertyMetadata(null));
    private readonly Grid _chrome = new();
    private readonly Button _label;
    private readonly ContentPresenter _header = new() { VerticalAlignment = VerticalAlignment.Center };
    private readonly Button _close;
    private DockingManager? _manager;
    public LayoutContent? Model { get => (LayoutContent?)GetValue(ModelProperty); set => SetValue(ModelProperty, value); }
    public LayoutItem? LayoutItem => (LayoutItem?)GetValue(LayoutItemProperty);
    protected LayoutTabItemBase()
    {
        IsTabStop = false; Padding = new(0); Margin = new(0);
        _chrome.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) }); _chrome.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        _label = DockVisuals.Button("", () => { if (Model != null) Model.IsActive = true; }); _label.Content = _header;
        _label.MinWidth = 72; _label.MaxWidth = 260; _label.HorizontalContentAlignment = HorizontalAlignment.Left;
        _label.AddHandler(PointerPressedEvent, new PointerEventHandler(OnLabelPressed), true);
        _label.DoubleTapped += (_, _) => { if (Model?.IsFloating == true) Model.Dock(); else Model?.Float(); };
        _close = DockVisuals.Button("×", () => { if (Model != null) DockVisuals.CloseOrHide(Model); }, "Close tab");
        Grid.SetColumn(_close, 1); _chrome.Children.Add(_label); _chrome.Children.Add(_close); Content = _chrome;
    }
    protected virtual void OnModelChanged(DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is LayoutContent old) old.PropertyChanged -= ModelChanged;
        if (e.NewValue is LayoutContent model) model.PropertyChanged += ModelChanged;
        if (Model?.Root?.Manager is { } manager) Update(manager);
    }
    protected void SetLayoutItem(LayoutItem value) => SetValue(LayoutItemProperty, value);
    private void ModelChanged(object? sender, PropertyChangedEventArgs e) { if (_manager != null) Update(_manager); }
    private void OnLabelPressed(object sender, PointerRoutedEventArgs args)
    {
        if (Model == null) return;
        var point = args.GetCurrentPoint(_label);
        if (point.Properties.IsMiddleButtonPressed) { DockVisuals.CloseOrHide(Model); args.Handled = true; return; }
        if (!point.Properties.IsLeftButtonPressed && args.Pointer.PointerDeviceType == DockPointerDeviceType.Mouse) return;
        Model.IsActive = true; _manager?.BeginDrag(Model, _label, args);
    }
    internal void Update(DockingManager manager)
    {
        _manager = manager; if (Model == null) return;
        SetLayoutItem(manager.GetLayoutItemFromModel(Model));
        var template = manager.HeaderTemplate(Model, this);
        _header.ContentTemplate = template; _header.Content = template == null ? Model.Title : Model;
        _label.IsEnabled = Model.IsEnabled;
        _close.Visibility = Model.CanClose || Model is LayoutAnchorable { CanHide: true } ? Visibility.Visible : Visibility.Collapsed;
        _label.FontWeight = Model.IsSelected ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal;
        _chrome.BorderThickness = new(0, 0, 0, Model.IsSelected ? 3 : 0);
        _chrome.BorderBrush = DockVisuals.Brush(manager, "UnoDock.AccentBrush", "AccentFillColorDefaultBrush");
        DockVisuals.SetName(_label, Model.Title ?? "Document");
        ToolTipService.SetToolTip(_label, Model.ToolTip ?? Model.Title);
        ContextFlyout = DockVisuals.Menu(manager, Model);
    }
    internal void DetachModel() { Model = null; _manager = null; ClearValue(LayoutItemProperty); }
}
public class LayoutDocumentTabItem : LayoutTabItemBase { public LayoutDocumentTabItem() { } }
public class LayoutAnchorableTabItem : LayoutTabItemBase { public LayoutAnchorableTabItem() { } }

public class LayoutDocumentControl : ContentControl
{
    public static readonly DependencyProperty ModelProperty = DependencyProperty.Register(nameof(Model), typeof(LayoutContent), typeof(LayoutDocumentControl), new PropertyMetadata(null, (d, e) => ((LayoutDocumentControl)d).OnModelChanged(e)));
    public static readonly DependencyProperty LayoutItemProperty = DependencyProperty.Register(nameof(LayoutItem), typeof(LayoutItem), typeof(LayoutDocumentControl), new PropertyMetadata(null));
    public LayoutContent? Model { get => (LayoutContent?)GetValue(ModelProperty); set => SetValue(ModelProperty, value); }
    public LayoutItem? LayoutItem => (LayoutItem?)GetValue(LayoutItemProperty);
    public LayoutDocumentControl() { HorizontalContentAlignment = HorizontalAlignment.Stretch; VerticalContentAlignment = VerticalAlignment.Stretch; GotFocus += (_, _) => { if (Model != null) Model.IsActive = true; }; }
    protected virtual void OnModelChanged(DependencyPropertyChangedEventArgs e)
    {
        if (Model?.Root?.Manager is { } manager)
        { var item = manager.GetLayoutItemFromModel(Model); SetLayoutItem(item); VisualParenting.Detach(item.View); Content = item.View; }
        else { ClearValue(LayoutItemProperty); Content = null; }
    }
    protected void SetLayoutItem(LayoutItem value) => SetValue(LayoutItemProperty, value);
}
public class LayoutAnchorableControl : LayoutDocumentControl
{
    public new LayoutAnchorable? Model { get => base.Model as LayoutAnchorable; set => base.Model = value; }
    public LayoutAnchorableControl() { }
}
public class AnchorablePaneTitle : LayoutAnchorableTabItem
{
    public new LayoutAnchorable? Model { get => base.Model as LayoutAnchorable; set => base.Model = value; }
    public AnchorablePaneTitle() { }
}
