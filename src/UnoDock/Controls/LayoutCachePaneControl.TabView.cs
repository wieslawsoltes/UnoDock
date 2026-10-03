using UnoDock.Internal;
using UnoDock.Layout;

namespace UnoDock.Controls;

public partial class LayoutCachePaneControl
{
    private TabView? _tabView;
    private bool _syncingTabView;
    private readonly Dictionary<LayoutContent, TabViewItem> _tabViewItems = new(ReferenceEqualityComparer.Instance);
    internal bool UsesTabView => _tabView is { Visibility: Visibility.Visible };

    private static bool WantsTabView(ILayoutGroup pane, DockingManager manager) => pane is LayoutDocumentPane && manager.DocumentTabStripMode == DocumentTabStripMode.TabView;
    /// <summary>Host the pane's tabs in a platform TabView. Each TabViewItem shows
        /// the retained LayoutTabItemBase as its header, so drag, context menu,
        /// templates and automation keep working; the TabView supplies Fluent
        /// visuals, close buttons, selection and scrolling.</summary>
        private void ReconcileTabView(IReadOnlyList<LayoutTabItemBase> tabs, LayoutContent? selected, DockingManager manager)
    {
        if (_tabView == null)
        {
            _tabView = new TabView
            {
                Name = "PART_DocumentTabView",
                IsAddTabButtonVisible = false,
                CanReorderTabs = false,
                CanDragTabs = false,
                AllowDrop = false,
                TabWidthMode = TabViewWidthMode.SizeToContent,
                VerticalAlignment = VerticalAlignment.Top,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Padding = new(0)
            };
            _tabView.SelectionChanged += OnTabViewSelectionChanged;
            _tabView.TabCloseRequested += (_, args) =>
            {
                if (args.Tab?.Tag is LayoutContent model && model.IsEnabled)
                    DockVisuals.CloseOrHide(model);
            };
            Grid.SetColumn(_tabView, 0);
            _tabBar.Children.Insert(0, _tabView);
        }

        _tabView.Visibility = Visibility.Visible;
        _syncingTabView = true;
        try
        {
            foreach (var stale in _tabViewItems.Keys.Where(k => !tabs.Any(t => ReferenceEquals(t.Model, k))).ToArray())
            {
                var old = _tabViewItems[stale];
                old.Header = null;
                _tabView.TabItems.Remove(old);
                _tabViewItems.Remove(stale);
            }

            for (var i = 0; i < tabs.Count; i++)
            {
                var tab = tabs[i];
                var model = tab.Model!;
                if (!_tabViewItems.TryGetValue(model, out var item))
                {
                    item = new TabViewItem
                    {
                        Tag = model,
                        Padding = new(4, 0, 2, 0)
                    };
                    _tabViewItems.Add(model, item);
                }

                if (!ReferenceEquals(item.Header, tab))
                {
                    VisualParenting.Detach(tab);
                    item.Header = tab;
                    VisualParenting.Hosted(item, tab);
                }

                tab.IsEmbeddedInTabView = true;
                tab.Update(manager);
                item.IsClosable = model.CanClose && model.IsEnabled;
                item.IsEnabled = model.IsEnabled;
                var index = _tabView.TabItems.IndexOf(item);
                if (index != i)
                {
                    if (index >= 0)
                        _tabView.TabItems.RemoveAt(index);
                    _tabView.TabItems.Insert(i, item);
                }
            }

            _tabView.SelectedItem = selected != null && _tabViewItems.TryGetValue(selected, out var current) ? current : null;
        }
        finally
        {
            _syncingTabView = false;
        }
    }

    private void OnTabViewSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncingTabView || _tabView?.SelectedItem is not TabViewItem { Tag: LayoutContent model } || !model.IsEnabled || model.IsSelected && model.IsActive)
            return;
        model.IsActive = true;
    }

    /// <summary>Return the tabs to the docking strip and hide the TabView.</summary>
    private void ReleaseTabView()
    {
        if (_tabView == null || _tabView.Visibility == Visibility.Collapsed && _tabViewItems.Count == 0)
            return;
        _syncingTabView = true;
        try
        {
            foreach (var (model, item) in _tabViewItems)
            {
                if (item.Header is LayoutTabItemBase tab)
                    tab.IsEmbeddedInTabView = false;
                item.Header = null;
            }

            _tabViewItems.Clear();
            _tabView.TabItems.Clear();
            _tabView.Visibility = Visibility.Collapsed;
        }
        finally
        {
            _syncingTabView = false;
        }
    }

    /// <summary>The scroll viewer that currently hosts the pane's tab headers.</summary>
    private ScrollViewer HeaderScroll => UsesTabView && _tabView!.FindVisualChildren<ScrollViewer>().FirstOrDefault() is { } inner ? inner : _scroll;
}
