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
    private readonly TextBlock _title = new()
    {
        Margin = new Thickness(10, 5, 4, 5),
        VerticalAlignment = VerticalAlignment.Center
    };
    private readonly Dictionary<LayoutContent, LayoutTabItemBase> _tabs = new(ReferenceEqualityComparer.Instance);
    protected ILayoutContentSelector? Selector
    {
        get; private set;
    }
    protected ILayoutGroup? Pane
    {
        get; private set;
    }

    public LayoutCachePaneControl()
    {
        HorizontalContentAlignment = HorizontalAlignment.Stretch;
        VerticalContentAlignment = VerticalAlignment.Stretch;
        _layout.RowDefinitions.Add(new()
        {
            Height = GridLength.Auto
        });
        _layout.RowDefinitions.Add(new()
        {
            Height = GridLength.Auto
        });
        _layout.RowDefinitions.Add(new()
        {
            Height = new(1, GridUnitType.Star)
        });
        _scroll = new()
        {
            Content = _headers,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            HorizontalScrollMode = ScrollMode.Enabled,
            VerticalScrollMode = ScrollMode.Disabled,
            MaxHeight = 54
        };
        Grid.SetRow(_scroll, 1);
        Grid.SetRow(_content, 2);
        _layout.Children.Add(_titleRow);
        _layout.Children.Add(_scroll);
        _layout.Children.Add(_content);
        Content = _layout;
        IsTabStop = false;
        InitializeChrome();
    }

    public IEnumerable<LayoutContent> Items => Pane?.Children.OfType<LayoutContent>() ?? [];

    internal void UpdatePane(ILayoutGroup pane, DockSurface surface)
    {
        BindPane(pane);
        if (pane is LayoutAnchorablePane && _headers is not AnchorablePaneTabPanel)
        {
            _headers.Children.Clear();
            _headers = new AnchorablePaneTabPanel();
            _scroll.Content = _headers;
            _scroll.HorizontalScrollMode = ScrollMode.Disabled;
            _scroll.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
        }

        var models = pane.Children.OfType<LayoutContent>().Where(c => c is not LayoutDocument { IsVisible: false }).ToArray();
        var selected = Selector!.SelectedContent;
        foreach (var stale in _tabs.Keys.Where(k => !models.Contains(k, ReferenceEqualityComparer.Instance)).ToArray())
        {
            _tabs[stale].DetachModel();
            _tabs.Remove(stale);
        }

        var tabViews = new List<UIElement>();
        var contentViews = new List<UIElement>();
        foreach (var model in models)
        {
            if (!_tabs.TryGetValue(model, out var tab))
            {
                tab = CreateTabItem(model) ?? throw new InvalidOperationException("The tab factory returned null.");
                if (VisualTreeHelper.GetParent(tab) != null || _tabs.Values.Contains(tab) || (tab.Model != null && !ReferenceEquals(tab.Model, model)))
                    throw new InvalidOperationException("The tab factory must return an unattached, unshared tab for the requested model.");
                tab.Model = model;
                _tabs.Add(model, tab);
            }

            tab.Update(surface.Manager);
            tabViews.Add(tab);
            var item = surface.Manager.GetLayoutItemFromModel(model);
            item.UpdateView();
            var presenter = ReferenceEquals(model, selected) ? item.View : item.ExistingView;
            if (presenter != null)
            {
                presenter.Visibility = ReferenceEquals(model, selected) ? Visibility.Visible : Visibility.Collapsed;
                contentViews.Add(presenter);
            }
        }

        VisualParenting.ReconcilePanel(_headers, tabViews);
        VisualParenting.ReconcilePanel(_content, contentViews);
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
        if (IsOverHeaderElement(_titlePresenter, surfacePoint, surface))
            return Pane?.ChildrenCount ?? 0;
        var index = 0;
        foreach (var model in Items)
        {
            if (!_tabs.TryGetValue(model, out var tab))
            {
                index++;
                continue;
            }

            var bounds = DockCoordinates.Bounds(tab, new Rect(0, 0, tab.ActualWidth, tab.ActualHeight), surface, surface.Manager.CrossWindowCoordinates);
            if (FlowDirection == FlowDirection.RightToLeft ? surfacePoint.X > bounds.X + bounds.Width / 2 : surfacePoint.X < bounds.X + bounds.Width / 2)
                return index;
            index++;
        }

        return index;
    }

    internal bool IsOverHeader(Point point, DockSurface surface) => IsOverHeaderElement(_scroll, point, surface) || IsOverHeaderElement(_titlePresenter, point, surface);
    private static bool IsOverHeaderElement(FrameworkElement header, Point point, DockSurface surface)
    {
        if (header.ActualWidth <= 0 || header.ActualHeight <= 0)
            return false;
        for (DependencyObject? element = header; element != null; element = VisualTreeHelper.GetParent(element))
            if (element is UIElement { Visibility: Visibility.Collapsed })
                return false;
        try
        {
            var local = DockCoordinates.Translate(surface, point, header, surface.Manager.CrossWindowCoordinates);
            return new Rect(0, 0, header.ActualWidth, header.ActualHeight).Contains(local);
        }
        catch (Exception e) when (DockCoordinates.IsUnavailable(e))
        {
            return false;
        }
    }

    internal bool ScrollHeaderAt(Point point, DockSurface surface, double seconds)
    {
        if (!IsOverHeaderElement(_scroll, point, surface) || _scroll.ScrollableWidth <= 0)
            return false;
        var local = DockCoordinates.Translate(surface, point, _scroll, surface.Manager.CrossWindowCoordinates);
        var delta = DockInteractionGeometry.AutoScrollDelta(local.X, _scroll.ActualWidth, _scroll.HorizontalOffset, _scroll.ScrollableWidth, seconds, FlowDirection == FlowDirection.RightToLeft);
        return delta != 0 && _scroll.ChangeView(Math.Clamp(_scroll.HorizontalOffset + delta, 0, _scroll.ScrollableWidth), null, null, true);
    }

    internal LayoutTabItemBase? TabFor(LayoutContent model) => _tabs.GetValueOrDefault(model);
    protected override Microsoft.UI.Xaml.Automation.Peers.AutomationPeer OnCreateAutomationPeer() => new LayoutPaneAutomationPeer(this);
    internal void NavigateHeader(LayoutContent from, Windows.System.VirtualKey key)
    {
        var items = Items.Where(m => m.IsEnabled && m is not LayoutDocument { IsVisible: false }).ToArray();
        if (items.Length == 0)
            return;
        var index = Array.IndexOf(items, from);
        var direction = key == Windows.System.VirtualKey.Left ? -1 : 1;
        if (FlowDirection == FlowDirection.RightToLeft)
            direction = -direction;
        index = key == Windows.System.VirtualKey.Home ? 0 : key == Windows.System.VirtualKey.End ? items.Length - 1 : (index + direction + items.Length) % items.Length;
        items[index].IsActive = true;
        TabFor(items[index])?.FocusLabel();
    }

    internal void ReleaseViews()
    {
        foreach (var tab in _tabs.Values)
            tab.DetachModel();
        _tabs.Clear();
        _headers.Children.Clear();
        _content.Children.Clear();
        _titleModel = null;
        _titleManager = null;
        _paneObserver?.Dispose();
        _paneObserver = null;
        _headerGeneration++;
        _lastHeaderSelection = null;
        _lastHeaderIndex = -1;
    }
}
