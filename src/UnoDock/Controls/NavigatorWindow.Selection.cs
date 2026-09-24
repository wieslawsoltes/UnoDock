using UnoDock.Layout;

namespace UnoDock.Controls;

public partial class NavigatorWindow
{
    private bool _handlingDirectSelection, _hasDirectSelection;
    private LayoutItem? _directSelection;
    private bool _directSelectionIsDocument;
    private long _directSelectionVersion;

    /// <summary>Highlight a document without invoking its activation command.
    /// Unlike SelectedDocument assignment, this is an explicit preview operation.</summary>
    public void PreviewDocument(LayoutDocumentItem? item) => PreviewItem(item);

    /// <summary>Highlight a tool without invoking its activation command.
    /// Unlike SelectedAnchorable assignment, this is an explicit preview operation.</summary>
    public void PreviewAnchorable(LayoutAnchorableItem? item) => PreviewItem(item);

    private void PreviewItem(LayoutItem? item)
    {
        if (!DispatcherQueue.HasThreadAccess) throw new InvalidOperationException("Navigator selection requires its owning UI thread.");
        if (IsWindowClosed || _sessionRoot == null) return;
        Select(item);
    }

    internal bool IsSelectionWindowClosed => IsWindowClosed;
    internal bool RequestSelectionClose()
    {
        var args = new CancelEventArgs();
        OnClosing(args);
        return !args.Cancel;
    }
    internal void CompleteSelectionClose() => CompleteWindowClose();

    private void DirectSelectionChanged(DependencyPropertyChangedEventArgs e, bool document)
    {
        if (!DispatcherQueue.HasThreadAccess) throw new InvalidOperationException("Navigator selection requires its owning UI thread.");
        // Ignore only our own current publication. A different value assigned by
        // an application callback is a real request, even during publication.
        LayoutItem? published = document ? _selected as LayoutDocumentItem : _selected as LayoutAnchorableItem;
        if (_publishing && ReferenceEquals(e.NewValue, published)) return;
        _directSelectionVersion++;
        _directSelection = e.NewValue as LayoutItem;
        _directSelectionIsDocument = document;
        _hasDirectSelection = true;
        DrainDirectSelection();
    }

    private void DrainDirectSelection()
    {
        if (_selecting || _handlingDirectSelection || !_hasDirectSelection) return;
        _handlingDirectSelection = true;
        try
        {
            var remaining = 64;
            while (_hasDirectSelection)
            {
                if (--remaining < 0) throw new InvalidOperationException("Navigator direct-selection callbacks did not converge.");
                var item = _directSelection;
                var document = _directSelectionIsDocument;
                var request = _directSelectionVersion;
                _hasDirectSelection = false;
                if (IsWindowClosed || _sessionRoot == null) continue;
                if (!ReferenceEquals(item, GetValue(document ? SelectedDocumentProperty : SelectedAnchorableProperty))) continue;
                if (item != null && !Eligible(item)) continue;
                // Direct setters keep the other category's DP. Clearing that other
                // category updates its list but must not clear the active preview.
                if (item != null || document == (_selected is LayoutDocumentItem))
                {
                    _selected = item; _selectionVersion++;
                    if (item is LayoutDocumentItem) _lastDocument = item;
                    if (item is LayoutAnchorableItem) _lastAnchorable = item;
                }
                var session = _sessionVersion;
                var previous = _publishing; _publishing = true;
                try
                {
                    _documentsList.SelectedItem = SelectedDocument;
                    if (session != _sessionVersion || request != _directSelectionVersion) continue;
                    _anchorablesList.SelectedItem = SelectedAnchorable;
                }
                finally { _publishing = previous; }
                if (session != _sessionVersion || request != _directSelectionVersion) continue;
                UpdateSelectedDetails(); QueueReveal();
                if (item != null && request == _directSelectionVersion)
                    _manager.Surface?.CommitDirectNavigatorSelection(this, !document, () => request == _directSelectionVersion);
            }
        }
        finally
        {
            _handlingDirectSelection = false; _hasDirectSelection = false; _directSelection = null;
        }
    }
}
