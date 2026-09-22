using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Data;
using Xceed.Wpf.AvalonDock.Internal;
using Xceed.Wpf.AvalonDock.Layout;

namespace Xceed.Wpf.AvalonDock.Controls;

[TemplatePart(Name = "PART_AnchorableListBox", Type = typeof(ListBox))]
[TemplatePart(Name = "PART_DocumentListBox", Type = typeof(ListBox))]
public class NavigatorWindow : DockWindowControl
{
    public static readonly DependencyProperty DocumentsProperty = DependencyProperty.Register(nameof(Documents), typeof(LayoutDocumentItem[]), typeof(NavigatorWindow), new PropertyMetadata(null));
    public static readonly DependencyProperty AnchorablesProperty = DependencyProperty.Register(nameof(Anchorables), typeof(IEnumerable<LayoutAnchorableItem>), typeof(NavigatorWindow), new PropertyMetadata(null));
    public static readonly DependencyProperty SelectedDocumentProperty = DependencyProperty.Register(nameof(SelectedDocument), typeof(LayoutDocumentItem), typeof(NavigatorWindow), new PropertyMetadata(null, (d, e) => ((NavigatorWindow)d).OnSelectedDocumentChanged(e)));
    public static readonly DependencyProperty SelectedAnchorableProperty = DependencyProperty.Register(nameof(SelectedAnchorable), typeof(LayoutAnchorableItem), typeof(NavigatorWindow), new PropertyMetadata(null, (d, e) => ((NavigatorWindow)d).OnSelectedAnchorableChanged(e)));
    public static readonly DependencyProperty LayoutDocumentsLabelProperty = DependencyProperty.Register(nameof(LayoutDocumentsLabel), typeof(string), typeof(NavigatorWindow), new PropertyMetadata("Documents"));
    public static readonly DependencyProperty LayoutAnchorablesLabelProperty = DependencyProperty.Register(nameof(LayoutAnchorablesLabel), typeof(string), typeof(NavigatorWindow), new PropertyMetadata("Tools"));
    private readonly DockingManager _manager;
    private readonly ListBox _defaultDocuments = CreateList("PART_DocumentListBox");
    private readonly ListBox _defaultAnchorables = CreateList("PART_AnchorableListBox");
    private ListBox _documentsList, _anchorablesList;
    private LayoutItem[] _ordered = [];
    private LayoutItem? _selected, _lastDocument, _lastAnchorable;
    private LayoutRoot? _sessionRoot;
    private bool _selecting, _refreshQueued;
    private long _sessionVersion;

    public NavigatorWindow(DockingManager manager)
    {
        _manager = manager ?? throw new ArgumentNullException(nameof(manager));
        _documentsList = _defaultDocuments; _anchorablesList = _defaultAnchorables;
        Width = 620; MaxWidth = 760; Padding = new(18); BorderThickness = new(1); IsTabStop = true;
        var grid = new Grid { ColumnSpacing = 16, RowSpacing = 12 };
        grid.ColumnDefinitions.Add(new() { Width = new(2, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new() { Width = new(3, GridUnitType.Star) });
        grid.RowDefinitions.Add(new() { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new() { Height = new(1, GridUnitType.Star) });
        grid.RowDefinitions.Add(new() { Height = GridLength.Auto });
        var documents = new TextBlock { FontSize = 20 }; var anchorables = new TextBlock { FontSize = 20 };
        documents.SetBinding(TextBlock.TextProperty, new Binding { Source = this, Path = new PropertyPath(nameof(LayoutDocumentsLabel)) });
        anchorables.SetBinding(TextBlock.TextProperty, new Binding { Source = this, Path = new PropertyPath(nameof(LayoutAnchorablesLabel)) });
        Grid.SetColumn(documents, 1); grid.Children.Add(anchorables); grid.Children.Add(documents);
        Grid.SetRow(_defaultAnchorables, 1); Grid.SetRow(_defaultDocuments, 1); Grid.SetColumn(_defaultDocuments, 1);
        grid.Children.Add(_defaultAnchorables); grid.Children.Add(_defaultDocuments);
        var hint = new TextBlock { Text = "Ctrl+Tab: recent items · ← →: tools/documents · ↑ ↓: select · Enter: activate · Esc: cancel",
            TextWrapping = TextWrapping.Wrap, Opacity = .7 };
        Grid.SetRow(hint, 2); Grid.SetColumnSpan(hint, 2); grid.Children.Add(hint); Content = grid;
        Attach(_documentsList); Attach(_anchorablesList);
        Unloaded += (_, _) => EndSession();
    }
    private static ListBox CreateList(string name) => new()
    {
        Name = name, DisplayMemberPath = nameof(LayoutItem.Title), SelectionMode = SelectionMode.Single,
        MinHeight = 90, MaxHeight = 380, HorizontalAlignment = HorizontalAlignment.Stretch
    };
    public LayoutDocumentItem[] Documents => (LayoutDocumentItem[]?)GetValue(DocumentsProperty) ?? [];
    public IEnumerable<LayoutAnchorableItem> Anchorables => (IEnumerable<LayoutAnchorableItem>?)GetValue(AnchorablesProperty) ?? [];
    public LayoutDocumentItem? SelectedDocument { get => (LayoutDocumentItem?)GetValue(SelectedDocumentProperty); set => SetValue(SelectedDocumentProperty, value); }
    public LayoutAnchorableItem? SelectedAnchorable { get => (LayoutAnchorableItem?)GetValue(SelectedAnchorableProperty); set => SetValue(SelectedAnchorableProperty, value); }
    public string LayoutDocumentsLabel { get => (string)GetValue(LayoutDocumentsLabelProperty); set => SetValue(LayoutDocumentsLabelProperty, value); }
    public string LayoutAnchorablesLabel { get => (string)GetValue(LayoutAnchorablesLabelProperty); set => SetValue(LayoutAnchorablesLabelProperty, value); }
    protected void SetDocuments(LayoutDocumentItem[] value) => SetValue(DocumentsProperty, value);
    protected void SetAnchorables(IEnumerable<LayoutAnchorableItem> value) => SetValue(AnchorablesProperty, value);
    protected virtual void OnSelectedDocumentChanged(DependencyPropertyChangedEventArgs e)
    {
        if (!_selecting) Select(e.NewValue as LayoutItem);
    }
    protected virtual void OnSelectedAnchorableChanged(DependencyPropertyChangedEventArgs e)
    {
        if (!_selecting) Select(e.NewValue as LayoutItem);
    }
    protected override void OnApplyTemplate()
    {
        Detach(_documentsList); Detach(_anchorablesList);
        base.OnApplyTemplate();
        _documentsList = GetTemplateChild("PART_DocumentListBox") as ListBox ?? _defaultDocuments;
        _anchorablesList = GetTemplateChild("PART_AnchorableListBox") as ListBox ?? _defaultAnchorables;
        Attach(_documentsList); Attach(_anchorablesList);
        PublishLists();
    }
    private void Attach(ListBox list) { list.SelectionChanged += ListSelectionChanged; list.Tapped += ListTapped; }
    private void Detach(ListBox list) { list.SelectionChanged -= ListSelectionChanged; list.Tapped -= ListTapped; }
    private void ListSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_selecting && ((ListBox)sender).SelectedItem is LayoutItem item) Select(item);
    }
    private void ListTapped(object sender, TappedRoutedEventArgs e)
    {
        for (var current = e.OriginalSource as DependencyObject; current != null && !ReferenceEquals(current, sender); current = VisualTreeHelper.GetParent(current))
            if (current is ListBoxItem)
            {
                Select(((ListBox)sender).SelectedItem as LayoutItem);
                _manager.Surface?.CloseNavigator(true); e.Handled = true; return;
            }
    }
    internal void Initialize()
    {
        EnsureInitialized(); EndSession();
        _sessionRoot = _manager.Layout; _sessionRoot.Updated += ModelUpdated;
        _manager.LayoutChanged += LayoutReplaced;
        _ordered = EligibleItems().ToArray();
        PublishLists();
        Background = DockVisuals.Brush(_manager, "UnoDock.PaneBrush", "LayerFillColorDefaultBrush");
        BorderBrush = DockVisuals.Brush(_manager, "UnoDock.AccentBrush", "AccentFillColorDefaultBrush");
        var active = Array.FindIndex(_ordered, i => ReferenceEquals(i.LayoutElement, _manager.Layout.ActiveContent));
        if (_ordered.Length > 0)
        {
            var start = active >= 0 ? active : 0;
            Select(_ordered[(start + (InputState.ShiftDown ? _ordered.Length - 1 : 1)) % _ordered.Length]);
        }
        else Select(null);
    }
    private IEnumerable<LayoutItem> EligibleItems() => _manager.Layout.Descendents().OfType<LayoutContent>()
        .Where(Eligible).OrderByDescending(c => c.LastActivationTimeStamp)
        .ThenBy(c => c.ContentId, StringComparer.Ordinal).Select(_manager.GetLayoutItemFromModel);
    private bool Eligible(LayoutContent content) => ReferenceEquals(content.Root, _manager.Layout) && content.IsEnabled &&
        content is not LayoutAnchorable { IsHidden: true } && content is not LayoutDocument { IsVisible: false };
    private bool Eligible(LayoutItem? item) => item != null && Eligible(item.LayoutElement) && _ordered.Contains(item, ReferenceEqualityComparer.Instance);
    private void LayoutReplaced(object? sender, EventArgs e)
    {
        // A root replacement invalidates the entire navigation session; no old
        // editor, command or queued update may act on the new workspace.
        _manager.Surface?.CloseNavigator(false); EndSession();
    }
    private void ModelUpdated(object? sender, EventArgs e)
    {
        if (_refreshQueued || _sessionRoot == null) return;
        _refreshQueued = true; var version = _sessionVersion;
        if (!DispatcherQueue.TryEnqueue(() =>
        {
            if (version != _sessionVersion) return;
            _refreshQueued = false;
            if (_sessionRoot != null) RefreshItems();
        })) _refreshQueued = false;
    }
    private void RefreshItems()
    {
        var items = EligibleItems().ToArray(); var old = _selected; var index = Array.IndexOf(_ordered, old);
        var set = items.ToHashSet(ReferenceEqualityComparer.Instance);
        // Preserve the session's MRU order. Focus/selection must not reshuffle it
        // under a held Ctrl key. New items are appended in deterministic order.
        _ordered = _ordered.Where(set.Contains).Concat(items.Where(i => !_ordered.Contains(i, ReferenceEqualityComparer.Instance))).ToArray();
        PublishLists();
        Select(old != null && _ordered.Contains(old, ReferenceEqualityComparer.Instance) ? old :
            _ordered.Length == 0 ? null : _ordered[Math.Clamp(index, 0, _ordered.Length - 1)]);
    }
    private void PublishLists()
    {
        _selecting = true;
        try
        {
            SetDocuments(_ordered.OfType<LayoutDocumentItem>().ToArray()); SetAnchorables(_ordered.OfType<LayoutAnchorableItem>().ToArray());
            _documentsList.ItemsSource = Documents; _anchorablesList.ItemsSource = Anchorables;
            _documentsList.SelectedItem = _selected as LayoutDocumentItem; _anchorablesList.SelectedItem = _selected as LayoutAnchorableItem;
        }
        finally { _selecting = false; }
    }
    private void Select(LayoutItem? item)
    {
        if (_selecting) return;
        // Public DP writes cannot smuggle in an item from another manager.
        if (item != null && !Eligible(item)) item = Eligible(_selected) ? _selected : null;
        _selecting = true;
        try
        {
            _selected = item;
            SelectedDocument = item as LayoutDocumentItem; SelectedAnchorable = item as LayoutAnchorableItem;
            _documentsList.SelectedItem = SelectedDocument; _anchorablesList.SelectedItem = SelectedAnchorable;
            if (SelectedDocument != null) { _lastDocument = item; _documentsList.ScrollIntoView(item); }
            if (SelectedAnchorable != null) { _lastAnchorable = item; _anchorablesList.ScrollIntoView(item); }
        }
        finally { _selecting = false; }
    }
    internal void Advance(int delta)
    {
        RefreshItems(); if (_ordered.Length == 0) return;
        var index = Math.Max(0, Array.IndexOf(_ordered, _selected));
        Select(_ordered[(int)(((long)index + delta % _ordered.Length + _ordered.Length) % _ordered.Length)]);
    }
    private void AdvanceGroup(int delta)
    {
        RefreshItems();
        var group = _selected is LayoutAnchorableItem ? _ordered.OfType<LayoutAnchorableItem>().Cast<LayoutItem>().ToArray() : Documents.Cast<LayoutItem>().ToArray();
        if (group.Length == 0) { Advance(delta); return; }
        var index = Math.Max(0, Array.IndexOf(group, _selected));
        Select(group[(index + delta + group.Length) % group.Length]);
    }
    private void SelectGroup(bool documents)
    {
        RefreshItems();
        var remembered = documents ? _lastDocument : _lastAnchorable;
        Select(Eligible(remembered) ? remembered : documents ? Documents.FirstOrDefault() : Anchorables.FirstOrDefault());
    }
    internal void CommitSelection()
    {
        var selected = _selected;
        if (!Eligible(selected)) return;
        var command = selected!.ActivateCommand;
        if (command?.CanExecute(null) == true) command.Execute(null);
    }
    internal void EndSession()
    {
        _sessionVersion++; _refreshQueued = false;
        if (_sessionRoot != null) _sessionRoot.Updated -= ModelUpdated;
        _sessionRoot = null; _manager.LayoutChanged -= LayoutReplaced;
    }
    protected override void OnPreviewKeyDown(KeyRoutedEventArgs e)
    {
        base.OnPreviewKeyDown(e); if (!e.Handled) HandleNavigatorKeyDown(e);
    }
    protected override void OnKeyDown(KeyRoutedEventArgs e)
    {
        base.OnKeyDown(e); if (!e.Handled) HandleNavigatorKeyDown(e);
    }
    private void HandleNavigatorKeyDown(KeyRoutedEventArgs e)
    {
        switch (e.Key)
        {
            case Windows.System.VirtualKey.Tab: Advance(InputState.ShiftDown ? -1 : 1); break;
            case Windows.System.VirtualKey.Down: AdvanceGroup(1); break;
            case Windows.System.VirtualKey.Up: AdvanceGroup(-1); break;
            case Windows.System.VirtualKey.Left: SelectGroup(FlowDirection == FlowDirection.RightToLeft); break;
            case Windows.System.VirtualKey.Right: SelectGroup(FlowDirection != FlowDirection.RightToLeft); break;
            case Windows.System.VirtualKey.Enter: _manager.Surface?.CloseNavigator(true); break;
            case Windows.System.VirtualKey.Escape: _manager.Surface?.CloseNavigator(false); break;
            default: return;
        }
        e.Handled = true;
    }
    protected override void OnPreviewKeyUp(KeyRoutedEventArgs e)
    {
        base.OnPreviewKeyUp(e);
        if (!e.Handled) HandleNavigatorKeyUp(e);
    }
    protected override void OnKeyUp(KeyRoutedEventArgs e)
    {
        base.OnKeyUp(e); if (!e.Handled) HandleNavigatorKeyUp(e);
    }
    private void HandleNavigatorKeyUp(KeyRoutedEventArgs e)
    {
        if (e.Key is Windows.System.VirtualKey.Control or Windows.System.VirtualKey.LeftControl or Windows.System.VirtualKey.RightControl)
        { _manager.Surface?.CloseNavigator(true); e.Handled = true; }
    }
}
