using UnoDock.Internal;

namespace UnoDock.Controls;

public partial class NavigatorWindow
{
    private long _revealVersion;
    private ListBox? _waitingForLayout;
    private EventHandler<object>? _layoutReady;
    private void CancelReveal()
    {
        _revealVersion++;
        if (_waitingForLayout != null && _layoutReady != null) _waitingForLayout.LayoutUpdated -= _layoutReady;
        _waitingForLayout = null; _layoutReady = null;
    }
    private void QueueReveal()
    {
        CancelReveal();
        if (_sessionRoot == null || _selected == null) return;
        var version = _revealVersion; var session = _sessionVersion; var selected = _selected;
        var list = selected is LayoutDocumentItem ? _documentsList : _anchorablesList;
        DispatcherQueue.TryEnqueue(() => Attempt(0));
        void Attempt(int attempts)
        {
            if (version != _revealVersion || session != _sessionVersion || _sessionRoot == null ||
                !ReferenceEquals(selected, _selected) || !Eligible(selected) ||
                !ReferenceEquals(list, selected is LayoutDocumentItem ? _documentsList : _anchorablesList)) return;
            if (TryReveal(list, selected)) return;
            // Realization is asynchronous. Wait for a bounded number of actual
            // layout completions; never keep a permanent frame/timer subscription.
            if (attempts >= 4) return;
            _waitingForLayout = list;
            EventHandler<object>? handler = null;
            handler = (_, _) =>
            {
                list.LayoutUpdated -= handler;
                if (version != _revealVersion || session != _sessionVersion) return;
                _waitingForLayout = null; _layoutReady = null;
                DispatcherQueue.TryEnqueue(() => Attempt(attempts + 1));
            };
            _layoutReady = handler; list.LayoutUpdated += handler;
        }
    }
    private bool TryReveal(ListBox list, LayoutItem selected)
    {
        if (XamlRoot == null || !ReferenceEquals(list.XamlRoot, XamlRoot) ||
            list.ContainerFromItem(selected) is not FrameworkElement container || container.ActualHeight <= 0) return false;
        ScrollViewer? scroll = null;
        for (var parent = VisualTreeHelper.GetParent(container); parent != null && !ReferenceEquals(parent, list); parent = VisualTreeHelper.GetParent(parent))
            if (parent is ScrollViewer viewer) { scroll = viewer; break; }
        if (scroll?.Content is not UIElement content || scroll.ViewportHeight <= 0) return false;
        var bounds = container.TransformToVisual(content).TransformBounds(new(0, 0, container.ActualWidth, container.ActualHeight));
        var top = bounds.Top; var bottom = bounds.Bottom; var current = scroll.VerticalOffset;
        if (!double.IsFinite(top) || !double.IsFinite(bottom) || !double.IsFinite(current)) return true;
        var next = top < current ? top : bottom > current + scroll.ViewportHeight ? bottom - scroll.ViewportHeight : current;
        next = Math.Clamp(next, 0, Math.Max(0, scroll.ScrollableHeight));
        return Math.Abs(next - current) < .25 || scroll.ChangeView(null, next, null, true);
    }
}
