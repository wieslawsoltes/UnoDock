using UnoDock.Layout;

namespace UnoDock.Controls;

public abstract partial class LayoutGridControl<T>
{
    private DockResizeRange ReadResizeRange(LayoutGridResizerControl splitter, int index)
    {
        if (!IsLoaded || !splitter.IsLoaded || index < 0 || index + 1 >= _displayed.Length || !Children.Contains(splitter) || _group.Root is not LayoutRoot { Manager: { } manager } root || !ReferenceEquals(root, manager.Layout) || !_group.Children.OfType<ILayoutPanelElement>().Where(c => c.IsVisible).SequenceEqual(_displayed, ReferenceEqualityComparer.Instance) || Orientation != _lastOrientation || _displayed[index] is not ILayoutPositionableElement before || _displayed[index + 1] is not ILayoutPositionableElement after)
            return DockResizeRange.Unavailable;
        var horizontal = Orientation == Orientation.Horizontal;
        var a = horizontal ? ColumnDefinitions[index * 2].ActualWidth : RowDefinitions[index * 2].ActualHeight;
        var b = horizontal ? ColumnDefinitions[index * 2 + 2].ActualWidth : RowDefinitions[index * 2 + 2].ActualHeight;
        var minimum = horizontal ? before.DockMinWidth : before.DockMinHeight;
        var otherMinimum = horizontal ? after.DockMinWidth : after.DockMinHeight;
        var extent = a + b;
        if (!double.IsFinite(extent + minimum + otherMinimum) || a < 0 || b < 0 || extent <= 0)
            return DockResizeRange.Unavailable;
        // Impossible minima describe a noninteractive compressed layout, never a
        // reversed numeric range. Zero-size endpoints cannot start the transaction.
        if (minimum + otherMinimum > extent || a <= 0 || b <= 0)
            return new(0, extent, a, true);
        // Arranged pixels can miss a fractional model minimum by layout rounding.
        // Include the actual current value without changing the model constraint.
        return new(Math.Min(minimum, a), Math.Max(extent - otherMinimum, a), a, _resize != null || !splitter.IsEnabled);
    }

    private void ResizeToValue(LayoutGridResizerControl splitter, int index, double value)
    {
        var range = ReadResizeRange(splitter, index);
        if (range.IsReadOnly)
            throw new InvalidOperationException("The splitter's workspace or endpoints are no longer available.");
        if (!double.IsFinite(value) || value < range.Minimum || value > range.Maximum)
            throw new ArgumentOutOfRangeException(nameof(value));
        ResizeOnce(splitter, index, value - range.Value, absolutePixels: true);
    }

    private readonly List<ResizeEndpointObserver> _resizeObservers = [];
    private void AttachResizeObservers()
    {
        if (!IsLoaded)
            return;
        foreach (var view in Children.OfType<FrameworkElement>().Where(v => v is ILayoutControl))
            if (!_resizeObservers.Any(observer => ReferenceEquals(observer.View, view)))
                _resizeObservers.Add(new(this, view));
    }

    private void DetachResizeObservers()
    {
        foreach (var observer in _resizeObservers)
            observer.Dispose();
        _resizeObservers.Clear();
    }

    // A retained view transferred out of this grid cannot keep an obsolete grid
    // alive. Unload/rebuild detaches deterministically; a later size event also
    // removes a stale subscription after an application reparents the endpoint.
    private sealed class ResizeEndpointObserver : IDisposable
    {
        private readonly WeakReference<LayoutGridControl<T>> _owner;
        internal FrameworkElement? View
        {
            get;
            private set;
        }

        internal ResizeEndpointObserver(LayoutGridControl<T> owner, FrameworkElement view)
        {
            _owner = new(owner);
            View = view;
            view.SizeChanged += Changed;
        }

        private void Changed(object sender, SizeChangedEventArgs e)
        {
            if (View is not { } view || !_owner.TryGetTarget(out var owner) || !owner.IsLoaded || !ReferenceEquals(VisualTreeHelper.GetParent(view), owner))
            {
                Dispose();
                return;
            }

            owner.RefreshResizeAutomation();
        }

        public void Dispose()
        {
            if (View is { } view)
            {
                View = null;
                view.SizeChanged -= Changed;
            }
        }
    }

    private void RefreshResizeAutomation()
    {
        foreach (var splitter in Children.OfType<LayoutGridResizerControl>())
            splitter.RefreshAutomation();
    }
}
