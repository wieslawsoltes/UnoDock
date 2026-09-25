using System.Xml;

namespace UnoDock.Layout;

/// <summary>Selection is tracked by identity, not by an index that becomes stale during collection edits.</summary>
internal sealed class PaneSelection(ILayoutGroup pane, Action<string> notify)
{
    private LayoutContent? _selected;
    private bool _changing, _pending;
    private LayoutContent? _requested;
    public LayoutContent? Content => _selected;
    public int Index
    {
        get => _selected == null ? -1 : pane.IndexOfChild(_selected);
        set
        {
            if (value < -1 || value >= pane.ChildrenCount) throw new ArgumentOutOfRangeException(nameof(value));
            Select(value < 0 ? null : (LayoutContent)pane.Children.ElementAt(value));
        }
    }
    private void Select(LayoutContent? value)
    {
        if (_changing)
        {
            // Last explicit request wins, including reselecting the current item
            // to withdraw a request queued by an earlier callback.
            _requested = value; _pending = !ReferenceEquals(_selected, value); return;
        }
        if (ReferenceEquals(_selected, value)) return;
        _requested = value; _pending = true;
        _changing = true;
        try
        {
            for (var pass = 0; _pending; pass++)
            {
                if (pass == 64) throw new InvalidOperationException("Pane selection observers did not converge after 64 transitions.");
                value = _requested; _pending = false;
                if (value != null && pane.IndexOfChild(value) < 0) value = null;
                if (ReferenceEquals(_selected, value)) continue;
                var previous = _selected; _selected = value;
                if (previous != null) previous.IsSelected = false;
                // Application callbacks can edit Children while IsSelected changes.
                foreach (var c in pane.Children.OfType<LayoutContent>().ToArray())
                    if (!ReferenceEquals(c, value) && c.IsSelected && pane.IndexOfChild(c) >= 0) c.IsSelected = false;
                if (value != null && pane.IndexOfChild(value) >= 0) value.IsSelected = true;
                else if (value != null) _selected = null;
                notify(nameof(ILayoutContentSelector.SelectedContent));
                notify(nameof(ILayoutContentSelector.SelectedContentIndex));
            }
        }
        finally { _changing = false; _pending = false; _requested = null; }
    }
    public void CollectionChanged()
    {
        if (_selected != null && pane.IndexOfChild(_selected) >= 0)
        { notify(nameof(ILayoutContentSelector.SelectedContentIndex)); return; }
        Select(pane.Children.OfType<LayoutContent>().FirstOrDefault(c => c.IsSelected && c.IsEnabled)
            ?? pane.Children.OfType<LayoutContent>().FirstOrDefault(c => c.IsEnabled));
    }
}
