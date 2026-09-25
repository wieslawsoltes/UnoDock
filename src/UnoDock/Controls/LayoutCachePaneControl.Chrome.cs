using UnoDock.Internal;
using UnoDock.Layout;

namespace UnoDock.Controls;
public partial class LayoutCachePaneControl
{
    private readonly Grid _tabBar = new();
    private readonly ContentPresenter _titlePresenter = new()
    {
        Name = "PART_ToolCaption"
    };
    private DockChromeButton _documentsButton = null!, _pinButton = null!, _hideButton = null!, _menuButton = null!;
    private LayoutContent? _lastHeaderSelection;
    private int _lastHeaderIndex = -1;
    private long _headerGeneration;
    private void RevealSelectedHeader(LayoutContent? selected)
    {
        var index = selected == null || Pane == null ? -1 : Pane.IndexOfChild(selected);
        if (ReferenceEquals(selected, _lastHeaderSelection) && index == _lastHeaderIndex)
            return;
        _lastHeaderSelection = selected;
        _lastHeaderIndex = index;
        var generation = ++_headerGeneration;
        if (selected == null || Pane is not LayoutDocumentPane)
            return;
        DispatcherQueue.TryEnqueue(() =>
        {
            if (generation != _headerGeneration || XamlRoot == null || !ReferenceEquals(Selector?.SelectedContent, selected) || _tabBar.Visibility != Visibility.Visible || !_tabs.TryGetValue(selected, out var tab))
                return;
            UpdateLayout();
            if (_scroll.ViewportWidth <= 0 || _scroll.ScrollableWidth <= 0)
                return;
            var bounds = tab.TransformToVisual(_headers).TransformBounds(new Rect(0, 0, tab.ActualWidth, tab.ActualHeight));
            var offset = _scroll.HorizontalOffset;
            var desired = bounds.Left < offset ? bounds.Left : bounds.Right > offset + _scroll.ViewportWidth ? bounds.Right - _scroll.ViewportWidth : offset;
            desired = Math.Clamp(desired, 0, _scroll.ScrollableWidth);
            if (Math.Abs(offset - desired) > .25)
                _scroll.ChangeView(desired, null, null, true);
        });
    }

    private void InitializeChrome()
    {
        Padding = new(0);
        MinHeight = 0;
        MinWidth = 0;
        _layout.Children.Remove(_scroll);
        _tabBar.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) });
        _tabBar.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        _tabBar.Children.Add(_scroll);
        _documentsButton = DockChrome.Icon(DockGlyph.Documents, ShowDocuments, "Open documents");
        Grid.SetColumn(_documentsButton, 1);
        _tabBar.Children.Add(_documentsButton);
        _layout.Children.Add(_tabBar);
        _scroll.HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden;
        _scroll.Padding = new(0);
        _scroll.MaxHeight = double.PositiveInfinity;
        _title.Margin = new(2, 0, 0, 0);
        _title.TextTrimming = TextTrimming.CharacterEllipsis;
        _titlePresenter.Content = _title;
        _titlePresenter.VerticalContentAlignment = VerticalAlignment.Center;
        _titleRow.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) });
        _titleRow.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        _titleRow.Children.Add(_titlePresenter);
        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center
        };
        _menuButton = DockChrome.Icon(DockGlyph.Menu, () =>
        {
            if (_titleModel != null && _titleManager != null)
                DockVisuals.Menu(_titleManager, _titleModel).ShowAt(_menuButton);
        }, "Tool window options");
        _pinButton = DockChrome.Icon(DockGlyph.Pin, () => _titleModel?.ToggleAutoHide(), "Auto-hide tool");
        _hideButton = DockChrome.Icon(DockGlyph.Close, () =>
        {
            if (_titleModel != null)
                DockVisuals.CloseOrHide(_titleModel);
        }, "Hide or close tool");
        actions.Children.Add(_menuButton);
        actions.Children.Add(_pinButton);
        actions.Children.Add(_hideButton);
        Grid.SetColumn(actions, 1);
        _titleRow.Children.Add(actions);
        _titlePresenter.PointerPressed += (_, e) =>
        {
            if (_titleModel != null)
                _titleManager?.BeginDrag(_titleModel, _titlePresenter, e);
        };
        _titlePresenter.DoubleTapped += (_, _) =>
        {
            if (_titleModel?.IsFloating == true)
                _titleModel.Dock();
            else
                _titleModel?.Float();
        };
    }

    private void UpdatePaneChrome(ILayoutGroup pane, LayoutContent[] models, DockingManager manager)
    {
        var p = DockChrome.Palette(manager);
        var tool = pane is LayoutAnchorablePane;
        _layout.BorderBrush = p.Border;
        _layout.BorderThickness = new(1);
        _layout.Background = p.Surface;
        _content.Background = p.Surface;
        _content.Margin = new(2, tool ? 0 : 1, 2, 2);
        _headers.Background = p.Header;
        _tabBar.Background = p.Header;
        Grid.SetRow(_tabBar, tool ? 2 : 0);
        Grid.SetRow(_content, 1);
        Grid.SetRow(_titleRow, 0);
        _layout.RowDefinitions[0].Height = new(tool ? p.TitleHeight : p.TabHeight);
        _layout.RowDefinitions[1].Height = new(1, GridUnitType.Star);
        _layout.RowDefinitions[2].Height = tool && models.Length > 1 ? new(p.ToolTabHeight) : new(0);
        var showHeader = tool ? models.Length > 1 : pane is not LayoutDocumentPane { ShowHeader: false } && models.Length != 0;
        _tabBar.Visibility = showHeader ? Visibility.Visible : Visibility.Collapsed;
        if (!tool && !showHeader)
            _layout.RowDefinitions[0].Height = new(0);
        _scroll.Visibility = Visibility.Visible;
        _scroll.MaxHeight = tool ? p.ToolTabHeight : p.TabHeight;
        _documentsButton.Visibility = tool ? Visibility.Collapsed : Visibility.Visible;
        _documentsButton.Configure(p);
        _tabBar.BorderBrush = p.Border;
        _tabBar.BorderThickness = tool ? new(0, 1, 0, 0) : new(0, 0, 0, 1);
        _titleRow.Background = Selector?.SelectedContent?.IsActive == true ? p.ActiveTitle : p.Header;
    }

    private void UpdateTitle(bool visible, LayoutAnchorable? selected, DockingManager manager)
    {
        _titleRow.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        _titleModel = selected;
        _titleManager = manager;
        var p = DockChrome.Palette(manager);
        _title.FontSize = p.FontSize;
        _title.Foreground = p.Foreground;
        _title.Text = selected?.Title ?? "Tools";
        var template = selected == null ? null : manager.HeaderTemplate(selected, _titlePresenter, title: true);
        _titlePresenter.ContentTemplate = template;
        _titlePresenter.Content = template == null ? _title : selected;
        _menuButton.Configure(p);
        _pinButton.Configure(p);
        _hideButton.Configure(p);
        _menuButton.IsEnabled = selected?.IsEnabled == true;
        _pinButton.Visibility = selected?.CanAutoHide == true ? Visibility.Visible : Visibility.Collapsed;
        _pinButton.IsEnabled = selected?.IsEnabled == true;
        _hideButton.Visibility = selected is { CanHide: true } or { CanClose: true } ? Visibility.Visible : Visibility.Collapsed;
        _hideButton.IsEnabled = selected?.IsEnabled == true;
    }

    private void ShowDocuments()
    {
        if (Pane?.Root?.Manager is not { } manager)
            return;
        var root = manager.Layout;
        var pane = Pane;
        var menu = new MenuFlyout();
        foreach (var model in Items.Where(c => c is not LayoutDocument { IsVisible: false }).ToArray())
        {
            var item = new ToggleMenuFlyoutItem
            {
                Text = model.Title ?? "Untitled",
                IsChecked = model.IsSelected,
                IsEnabled = model.IsEnabled
            };
            item.Click += (_, _) =>
            {
                // Revalidate after an asynchronously open menu; a reset can detach the model.
                if (ReferenceEquals(manager.Layout, root) && ReferenceEquals(model.Parent, pane) && model.IsEnabled && model is not LayoutDocument { IsVisible: false })
                    model.IsActive = true;
            };
            menu.Items.Add(item);
        }

        menu.ShowAt(_documentsButton);
    }
}
