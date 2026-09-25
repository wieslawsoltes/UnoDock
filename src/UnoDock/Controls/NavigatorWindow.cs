using Microsoft.UI.Xaml.Input;
using UnoDock.Internal;
using UnoDock.Layout;

namespace UnoDock.Controls;

[TemplatePart(Name = "PART_AnchorableListBox", Type = typeof(ListBox))]
[TemplatePart(Name = "PART_DocumentListBox", Type = typeof(ListBox))]
public partial class NavigatorWindow : DockWindowControl
{
    public static readonly DependencyProperty DocumentsProperty = DependencyProperty.Register(nameof(Documents), typeof(LayoutDocumentItem[]), typeof(NavigatorWindow), new PropertyMetadata(null));
    public static readonly DependencyProperty AnchorablesProperty = DependencyProperty.Register(nameof(Anchorables), typeof(IEnumerable<LayoutAnchorableItem>), typeof(NavigatorWindow), new PropertyMetadata(null));
    public static readonly DependencyProperty SelectedDocumentProperty = DependencyProperty.Register(nameof(SelectedDocument), typeof(LayoutDocumentItem), typeof(NavigatorWindow), new PropertyMetadata(null, (d, e) => ((NavigatorWindow)d).OnSelectedDocumentChanged(e)));
    public static readonly DependencyProperty SelectedAnchorableProperty = DependencyProperty.Register(nameof(SelectedAnchorable), typeof(LayoutAnchorableItem), typeof(NavigatorWindow), new PropertyMetadata(null, (d, e) => ((NavigatorWindow)d).OnSelectedAnchorableChanged(e)));
    public static readonly DependencyProperty LayoutDocumentsLabelProperty = DependencyProperty.Register(nameof(LayoutDocumentsLabel), typeof(string), typeof(NavigatorWindow), new PropertyMetadata("Active Files", (d, _) => ((NavigatorWindow)d).UpdateSelectedDetails()));
    public static readonly DependencyProperty LayoutAnchorablesLabelProperty = DependencyProperty.Register(nameof(LayoutAnchorablesLabel), typeof(string), typeof(NavigatorWindow), new PropertyMetadata("Active Tool Windows", (d, _) => ((NavigatorWindow)d).UpdateSelectedDetails()));
    private readonly DockingManager _manager;
    private readonly ListBox _defaultDocuments = CreateList("PART_DocumentListBox");
    private readonly ListBox _defaultAnchorables = CreateList("PART_AnchorableListBox");
    private ListBox _documentsList, _anchorablesList;
    private LayoutItem[] _ordered = [];
    private LayoutDocumentItem[] _documentItems = [];
    private LayoutAnchorableItem[] _anchorableItems = [];
    private readonly Dictionary<LayoutItem, int> _indices = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<LayoutItem, int> _groupIndices = new(ReferenceEqualityComparer.Instance);
    private LayoutItem? _selected, _lastDocument, _lastAnchorable, _pendingSelection;
    private LayoutRoot? _sessionRoot;
    private bool _selecting, _hasPendingSelection, _publishing, _refreshing, _refreshQueued, _itemsDirty;
    private long _sessionVersion, _selectionVersion;
    public NavigatorWindow(DockingManager manager)
    {
        _manager = manager ?? throw new ArgumentNullException(nameof(manager));
        _documentsList = _defaultDocuments;
        _anchorablesList = _defaultAnchorables;
        BuildDefaultChrome();
        Attach(_documentsList);
        Attach(_anchorablesList);
        Unloaded += (_, _) => EndSession();
    }

    public LayoutDocumentItem[] Documents => (LayoutDocumentItem[]?)GetValue(DocumentsProperty) ?? [];
    public IEnumerable<LayoutAnchorableItem> Anchorables => (IEnumerable<LayoutAnchorableItem>?)GetValue(AnchorablesProperty) ?? [];
    public LayoutDocumentItem? SelectedDocument
    {
        get => (LayoutDocumentItem?)GetValue(SelectedDocumentProperty);
        set => SetValue(SelectedDocumentProperty, value);
    }
    public LayoutAnchorableItem? SelectedAnchorable
    {
        get => (LayoutAnchorableItem?)GetValue(SelectedAnchorableProperty);
        set => SetValue(SelectedAnchorableProperty, value);
    }
    public string LayoutDocumentsLabel
    {
        get => (string)GetValue(LayoutDocumentsLabelProperty);
        set => SetValue(LayoutDocumentsLabelProperty, value);
    }
    public string LayoutAnchorablesLabel
    {
        get => (string)GetValue(LayoutAnchorablesLabelProperty);
        set => SetValue(LayoutAnchorablesLabelProperty, value);
    }

    protected void SetDocuments(LayoutDocumentItem[] value) => SetValue(DocumentsProperty, value);
    protected void SetAnchorables(IEnumerable<LayoutAnchorableItem> value) => SetValue(AnchorablesProperty, value);
    protected virtual void OnSelectedDocumentChanged(DependencyPropertyChangedEventArgs e) => DirectSelectionChanged(e, true);
    protected virtual void OnSelectedAnchorableChanged(DependencyPropertyChangedEventArgs e) => DirectSelectionChanged(e, false);
    protected override void OnApplyTemplate()
    {
        CancelReveal();
        Detach(_documentsList);
        Detach(_anchorablesList);
        base.OnApplyTemplate();
        _documentsList = GetTemplateChild("PART_DocumentListBox") as ListBox ?? _defaultDocuments;
        _anchorablesList = GetTemplateChild("PART_AnchorableListBox") as ListBox ?? _defaultAnchorables;
        Attach(_documentsList);
        Attach(_anchorablesList);
        PublishLists();
        PublishSelection();
        UpdateAppearance();
        QueueReveal();
    }

    private void Attach(ListBox list)
    {
        list.SelectionChanged += ListSelectionChanged;
        list.Tapped += ListTapped;
    }

    private void Detach(ListBox list)
    {
        list.SelectionChanged -= ListSelectionChanged;
        list.Tapped -= ListTapped;
    }

    private void ListSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_publishing && ((ListBox)sender).SelectedItem is LayoutItem item && !ReferenceEquals(item, _selected))
            Select(item);
    }

    private void ListTapped(object sender, TappedRoutedEventArgs e)
    {
        if (_sessionRoot == null || e.Handled)
            return;
        for (var current = e.OriginalSource as DependencyObject; current != null && !ReferenceEquals(current, sender); current = VisualTreeHelper.GetParent(current))
            if (current is ListBoxItem container)
            {
                var item = container.Content as LayoutItem ?? container.DataContext as LayoutItem;
                // Never activate the previous selection after tapping a stale container.
                if (!Eligible(item))
                    return;
                var session = _sessionVersion;
                Select(item);
                if (session == _sessionVersion && ReferenceEquals(_selected, item) && Eligible(item))
                    CloseNavigatorForInput(true);
                e.Handled = true;
                return;
            }
    }

    internal void Initialize()
    {
        EnsureInitialized();
        EndSession();
        _activationSession++;
        _selected = _lastDocument = _lastAnchorable = null;
        _sessionRoot = _manager.Layout;
        _sessionRoot.Updated += ModelUpdated;
        var subscribedSession = _sessionVersion;
        _sessionLayoutChanged = (sender, args) =>
        {
            if (subscribedSession == _sessionVersion)
                LayoutReplaced(sender, args);
        };
        _manager.LayoutChanged += _sessionLayoutChanged;
        var modelOrder = EnumerateItems().ToArray();
        _ordered = SortItems(modelOrder).ToArray();
        Reindex(modelOrder);
        var session = _sessionVersion;
        PublishLists();
        if (session != _sessionVersion)
            return;
        UpdateAppearance();
        var active = Array.FindIndex(_ordered, i => ReferenceEquals(i.LayoutElement, _manager.Layout.ActiveContent));
        var start = active >= 0 ? active : 0;
        Select(_ordered.Length == 0 ? null : _ordered[(start + (InputState.ShiftDown ? _ordered.Length - 1 : 1)) % _ordered.Length]);
    }

    private IEnumerable<LayoutItem> EnumerateItems() => _manager.Layout.Descendents().OfType<LayoutContent>().Where(Eligible).Select(_manager.GetLayoutItemFromModel);
    private static IOrderedEnumerable<LayoutItem> SortItems(IEnumerable<LayoutItem> items) => items.OrderByDescending(i => i.LayoutElement.LastActivationTimeStamp).ThenBy(i => i.ContentId, StringComparer.Ordinal);
    private bool Eligible(LayoutContent content) => ReferenceEquals(content.Root, _manager.Layout) && content.IsEnabled && content is not LayoutAnchorable { IsHidden: true } && content is not LayoutDocument { IsVisible: false };
    private bool Eligible(LayoutItem? item) => item != null && _indices.ContainsKey(item) && Eligible(item.LayoutElement);
    private void Reindex(LayoutItem[]? modelOrder = null)
    {
        _indices.Clear();
        _groupIndices.Clear();
        for (var i = 0; i < _ordered.Length; i++)
            _indices.Add(_ordered[i], i);
        var documents = _ordered.OfType<LayoutDocumentItem>().ToArray();
        // The stock navigator displays tool windows in model order, not document
        // MRU order. Keep that snapshot stable, appending new tools during a session.
        var existingTools = _anchorableItems.ToHashSet(ReferenceEqualityComparer.Instance);
        var anchorables = modelOrder != null ? modelOrder.OfType<LayoutAnchorableItem>().ToArray() : _anchorableItems.Where(item => _indices.ContainsKey(item)).Concat(EnumerateItems().OfType<LayoutAnchorableItem>().Where(item => !existingTools.Contains(item))).ToArray();
        if (!SameItems(_documentItems, documents))
            _documentItems = documents;
        if (!SameItems(_anchorableItems, anchorables))
            _anchorableItems = anchorables;
        for (var i = 0; i < _documentItems.Length; i++)
            _groupIndices.Add(_documentItems[i], i);
        for (var i = 0; i < _anchorableItems.Length; i++)
            _groupIndices.Add(_anchorableItems[i], i);
    }

    private static bool SameItems<T>(T[] a, T[] b)
        where T : class
    {
        if (a.Length != b.Length)
            return false;
        for (var i = 0; i < a.Length; i++)
            if (!ReferenceEquals(a[i], b[i]))
                return false;
        return true;
    }

    private void LayoutReplaced(object? sender, EventArgs e)
    {
        var session = _sessionVersion;
        CloseNavigatorForInput(false);
        if (session == _sessionVersion)
            EndSession();
    }

    private void ModelUpdated(object? sender, EventArgs e)
    {
        _itemsDirty = true;
        if (_refreshQueued || _sessionRoot == null)
            return;
        _refreshQueued = true;
        var version = _sessionVersion;
        if (!DispatcherQueue.TryEnqueue(() =>
        {
            if (version != _sessionVersion)
                return;
            _refreshQueued = false;
            if (_sessionRoot != null)
            {
                RefreshItems();
                UpdateAppearance();
            }
        }))
            _refreshQueued = false;
    }

    private void RefreshItems()
    {
        if (!_itemsDirty || _refreshing || _sessionRoot == null)
            return;
        _refreshing = true;
        _itemsDirty = false;
        var session = _sessionVersion;
        var selection = _selectionVersion;
        try
        {
            var items = EnumerateItems().ToArray();
            var old = _selected;
            var index = old != null && _indices.TryGetValue(old, out var i) ? i : 0;
            var set = items.ToHashSet(ReferenceEqualityComparer.Instance);
            // Linear identity reconciliation, preserving the frozen MRU prefix.
            // A title/description/activation change must not reset either ItemsSource.
            var ordered = _ordered.Where(set.Contains).Concat(SortItems(items.Where(item => !_indices.ContainsKey(item)))).ToArray();
            if (!SameItems(_ordered, ordered))
            {
                _ordered = ordered;
                Reindex();
                PublishLists();
                if (session != _sessionVersion)
                    return;
                if (selection == _selectionVersion)
                    Select(Eligible(old) ? old : _ordered.Length == 0 ? null : _ordered[Math.Clamp(index, 0, _ordered.Length - 1)]);
                QueueReveal();
            }
            else if (!Eligible(_selected))
                Select(null);
            UpdateSelectedDetails();
        }
        finally
        {
            _refreshing = false;
        }
    }

    private void PublishLists()
    {
        var previous = _publishing;
        _publishing = true;
        var session = _sessionVersion;
        try
        {
            if (!ReferenceEquals(Documents, _documentItems))
                SetDocuments(_documentItems);
            if (session != _sessionVersion)
                return;
            if (!ReferenceEquals(Anchorables, _anchorableItems))
                SetAnchorables(_anchorableItems);
            if (session != _sessionVersion)
                return;
            if (!ReferenceEquals(_documentsList.ItemsSource, _documentItems))
                _documentsList.ItemsSource = _documentItems;
            if (session != _sessionVersion)
                return;
            if (!ReferenceEquals(_anchorablesList.ItemsSource, _anchorableItems))
                _anchorablesList.ItemsSource = _anchorableItems;
        }
        finally
        {
            _publishing = previous;
        }
    }

    private void Select(LayoutItem? item)
    {
        _pendingSelection = item;
        _hasPendingSelection = true;
        if (_selecting)
            return;
        _selecting = true;
        try
        {
            var remaining = 64;
            while (_hasPendingSelection)
            {
                if (--remaining < 0)
                    throw new InvalidOperationException("Navigator selection callbacks did not converge.");
                item = _pendingSelection;
                _hasPendingSelection = false;
                if (item != null && !Eligible(item))
                    item = Eligible(_selected) ? _selected : null;
                var changed = !ReferenceEquals(item, _selected);
                _selected = item;
                if (changed)
                    _selectionVersion++;
                var session = _sessionVersion;
                PublishSelection();
                if (session != _sessionVersion)
                    return;
                if (item is LayoutDocumentItem)
                    _lastDocument = item;
                if (item is LayoutAnchorableItem)
                    _lastAnchorable = item;
                UpdateSelectedDetails();
                if (changed)
                    QueueReveal();
            }
        }
        catch
        {
            _hasDirectSelection = false;
            _directSelection = null;
            throw;
        }
        finally
        {
            _selecting = false;
            _hasPendingSelection = false;
            _pendingSelection = null;
        }

        DrainDirectSelection();
    }

    private void PublishSelection()
    {
        var previous = _publishing;
        _publishing = true;
        var session = _sessionVersion;
        try
        {
            SelectedDocument = _selected as LayoutDocumentItem;
            if (session != _sessionVersion)
                return;
            SelectedAnchorable = _selected as LayoutAnchorableItem;
            if (session != _sessionVersion)
                return;
            _documentsList.SelectedItem = _selected as LayoutDocumentItem;
            if (session != _sessionVersion)
                return;
            _anchorablesList.SelectedItem = _selected as LayoutAnchorableItem;
        }
        finally
        {
            _publishing = previous;
        }
    }

    internal void Advance(int delta)
    {
        RefreshItems();
        if (_ordered.Length == 0)
            return;
        var index = _selected != null && _indices.TryGetValue(_selected, out var i) ? i : 0;
        Select(_ordered[(int)(((long)index + delta % _ordered.Length + _ordered.Length) % _ordered.Length)]);
    }

    private LayoutItem[] SelectedGroup => _selected is LayoutAnchorableItem ? _anchorableItems : _documentItems;

    private void AdvanceGroup(int delta)
    {
        RefreshItems();
        var group = SelectedGroup;
        if (group.Length == 0)
        {
            Advance(delta);
            return;
        }

        var index = _selected != null && _groupIndices.TryGetValue(_selected, out var i) ? i : 0;
        Select(group[(int)(((long)index + delta % group.Length + group.Length) % group.Length)]);
    }

    private void SelectGroup(bool documents)
    {
        RefreshItems();
        var remembered = documents ? _lastDocument : _lastAnchorable;
        var first = documents ? _documentItems.FirstOrDefault() as LayoutItem : _anchorableItems.FirstOrDefault();
        // An empty category must not destroy a valid selection in the other one.
        Select(Eligible(remembered) ? remembered : first ?? _selected);
    }

    private void SelectBoundary(bool last)
    {
        RefreshItems();
        var group = SelectedGroup;
        if (group.Length > 0)
            Select(group[last ? group.Length - 1 : 0]);
    }

    internal void EndSession()
    {
        _sessionVersion++;
        _refreshQueued = _itemsDirty = false;
        CancelReveal();
        if (_sessionRoot != null)
            _sessionRoot.Updated -= ModelUpdated;
        _sessionRoot = null;
        if (_sessionLayoutChanged != null)
            _manager.LayoutChanged -= _sessionLayoutChanged;
        _sessionLayoutChanged = null;
    }

    protected override void OnPreviewKeyDown(KeyRoutedEventArgs e)
    {
        base.OnPreviewKeyDown(e);
        if (!e.Handled)
            HandleNavigatorKeyDown(e);
    }

    protected override void OnKeyDown(KeyRoutedEventArgs e)
    {
        base.OnKeyDown(e);
        if (!e.Handled)
            HandleNavigatorKeyDown(e);
    }

    private void HandleNavigatorKeyDown(KeyRoutedEventArgs e)
    {
        switch (e.Key)
        {
            case Windows.System.VirtualKey.Tab:
                Advance(InputState.ShiftDown ? -1 : 1);
                break;
            case Windows.System.VirtualKey.Down:
                AdvanceGroup(1);
                break;
            case Windows.System.VirtualKey.Up:
                AdvanceGroup(-1);
                break;
            case Windows.System.VirtualKey.Home:
                SelectBoundary(false);
                break;
            case Windows.System.VirtualKey.End:
                SelectBoundary(true);
                break;
            case Windows.System.VirtualKey.Left:
                SelectGroup(FlowDirection == FlowDirection.RightToLeft);
                break;
            case Windows.System.VirtualKey.Right:
                SelectGroup(FlowDirection != FlowDirection.RightToLeft);
                break;
            case Windows.System.VirtualKey.Enter:
                CloseNavigatorForInput(true);
                break;
            case Windows.System.VirtualKey.Escape:
                CloseNavigatorForInput(false);
                break;
            default:
                return;
        }

        e.Handled = true;
    }

    protected override void OnPreviewKeyUp(KeyRoutedEventArgs e)
    {
        base.OnPreviewKeyUp(e);
        if (!e.Handled)
            HandleNavigatorKeyUp(e);
    }

    protected override void OnKeyUp(KeyRoutedEventArgs e)
    {
        base.OnKeyUp(e);
        if (!e.Handled)
            HandleNavigatorKeyUp(e);
    }

    private void HandleNavigatorKeyUp(KeyRoutedEventArgs e)
    {
        if (e.Key is Windows.System.VirtualKey.Control or Windows.System.VirtualKey.LeftControl or Windows.System.VirtualKey.RightControl)
        {
            CloseNavigatorForInput(true);
            e.Handled = true;
        }
    }
}
