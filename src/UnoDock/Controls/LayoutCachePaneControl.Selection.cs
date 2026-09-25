using UnoDock.Layout;

namespace UnoDock.Controls;
public partial class LayoutCachePaneControl
{
    public static readonly DependencyProperty SelectedIndexProperty = DependencyProperty.Register(nameof(SelectedIndex), typeof(int), typeof(LayoutCachePaneControl), new PropertyMetadata(-1, (d, e) => ((LayoutCachePaneControl)d).SelectionRequested(true)));
    public static readonly DependencyProperty SelectedItemProperty = DependencyProperty.Register(nameof(SelectedItem), typeof(object), typeof(LayoutCachePaneControl), new PropertyMetadata(null, (d, e) => ((LayoutCachePaneControl)d).SelectionRequested(false)));
    private bool _writingSelection, _synchronizingSelection, _selectionDirty;
    private LayoutContent? _lastSelection;
    private PaneObserver? _paneObserver;
    public int SelectedIndex { get => (int)GetValue(SelectedIndexProperty); set => SetValue(SelectedIndexProperty, value); }
    public object? SelectedItem { get => GetValue(SelectedItemProperty); set => SetValue(SelectedItemProperty, value); }

    // Binding the model does not call a virtual member from a constructor.
    internal void BindPane(ILayoutGroup pane)
    {
        if (ReferenceEquals(Pane, pane) && _paneObserver != null)
            return;
        _paneObserver?.Dispose();
        Pane = pane;
        Selector = (ILayoutContentSelector)pane;
        _lastSelection = Selector.SelectedContent;
        _writingSelection = true;
        try
        {
            SetValue(SelectedIndexProperty, Selector.SelectedContentIndex);
            SetValue(SelectedItemProperty, _lastSelection);
        }
        finally
        {
            _writingSelection = false;
        }

        _paneObserver = new(this, (INotifyPropertyChanged)pane);
    }

    private void SelectionRequested(bool index)
    {
        if (_writingSelection || Selector == null || Pane == null)
            return;
        try
        {
            if (index)
                Selector.SelectedContentIndex = SelectedIndex;
            else if (SelectedItem == null)
                Selector.SelectedContentIndex = -1;
            else if (SelectedItem is LayoutContent model && Pane.IndexOfChild(model)is var position && position >= 0)
                Selector.SelectedContentIndex = position;
        }
        finally
        {
            SynchronizeSelection();
        }
    }

    private void SynchronizeSelection()
    {
        _selectionDirty = true;
        if (_synchronizingSelection)
            return;
        _synchronizingSelection = true;
        try
        {
            for (var iteration = 0; _selectionDirty && Selector != null; iteration++)
            {
                if (iteration == 64)
                    throw new InvalidOperationException("Pane selection bindings did not converge after 64 passes.");
                _selectionDirty = false;
                var selected = Selector.SelectedContent;
                var previous = _lastSelection;
                _lastSelection = selected;
                _writingSelection = true;
                try
                {
                    SetValue(SelectedIndexProperty, Selector.SelectedContentIndex);
                    SetValue(SelectedItemProperty, selected);
                }
                finally
                {
                    _writingSelection = false;
                }

                if (!ReferenceEquals(previous, selected))
                    OnSelectionChanged(new SelectionChangedEventArgs(previous == null ? Array.Empty<object>() : new object[] { previous }, selected == null ? Array.Empty<object>() : new object[] { selected }));
                if (!ReferenceEquals(selected, Selector.SelectedContent) || SelectedIndex != Selector.SelectedContentIndex)
                    _selectionDirty = true;
            }
        }
        finally
        {
            _synchronizingSelection = false;
        }
    }

    protected override void OnSelectionChanged(SelectionChangedEventArgs e)
    {
        // Existing cached presenters respond synchronously, without creating views
        // for unvisited tabs. The manager's normal invalidation realizes a new tab.
        foreach (var(model, tab)in _tabs)
            if (tab.LayoutItem?.ExistingView is { } view)
                view.Visibility = ReferenceEquals(model, Selector?.SelectedContent) ? Visibility.Visible : Visibility.Collapsed;
        base.OnSelectionChanged(e);
        QueueSelectionAutomation();
    }

    protected override IEnumerator LogicalChildren => _content.Children.ToArray().GetEnumerator();

    protected void ActivateSelection()
    {
        if (Selector?.SelectedContent is { IsEnabled: true, Root: not null } model)
            model.IsActive = true;
    }

    private sealed class PaneObserver : IDisposable
    {
        private readonly WeakReference<LayoutCachePaneControl> _owner;
        private INotifyPropertyChanged? _source;
        internal PaneObserver(LayoutCachePaneControl owner, INotifyPropertyChanged source)
        {
            _owner = new(owner);
            _source = source;
            source.PropertyChanged += Changed;
        }

        private void Changed(object? sender, PropertyChangedEventArgs e)
        {
            if (!_owner.TryGetTarget(out var target))
            {
                Dispose();
                return;
            }

            if (e.PropertyName is null or "" or nameof(ILayoutContentSelector.SelectedContent) or nameof(ILayoutContentSelector.SelectedContentIndex))
                target.SynchronizeSelection();
        }

        public void Dispose()
        {
            if (_source is { } source)
            {
                _source = null;
                source.PropertyChanged -= Changed;
            }
        }
    }
}
