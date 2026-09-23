using UnoDock.Internal;
using UnoDock.Layout;

namespace UnoDock.Controls;

public abstract partial class LayoutGridControl<T>
{
    private ResizeSession? _resize;
    private sealed record ResizeSession(LayoutGridResizerControl Splitter, int Index,
        ILayoutPositionableElement Before, ILayoutPositionableElement After, LayoutRoot Root,
        DockingManager Manager, bool Horizontal, GridLength BeforeLength, GridLength AfterLength,
        double BeforePixels, double AfterPixels, double MinBefore, double MinAfter, ILayoutPanelElement[] Order, Canvas Adorner, Border Ghost)
    {
        internal double Displacement { get; set; }
    }
    private void BeginResize(LayoutGridResizerControl splitter, int index)
    {
        CancelResize();
        if (!IsLoaded || index < 0 || index + 1 >= _displayed.Length ||
            _displayed[index] is not ILayoutPositionableElement a || _displayed[index + 1] is not ILayoutPositionableElement b ||
            _group.Root is not LayoutRoot { Manager: { } manager } root || !ReferenceEquals(root, manager.Layout))
        { splitter.CancelDrag(); return; }
        var horizontal = Orientation == Orientation.Horizontal;
        var first = horizontal ? ColumnDefinitions[index * 2].ActualWidth : RowDefinitions[index * 2].ActualHeight;
        var second = horizontal ? ColumnDefinitions[index * 2 + 2].ActualWidth : RowDefinitions[index * 2 + 2].ActualHeight;
        var minA = horizontal ? a.DockMinWidth : a.DockMinHeight;
        var minB = horizontal ? b.DockMinWidth : b.DockMinHeight;
        if (!double.IsFinite(first + second + minA + minB) || first <= 0 || second <= 0 || minA + minB > first + second)
        { splitter.CancelDrag(); return; }
        var adorner = new Canvas { IsHitTestVisible = false };
        SetColumnSpan(adorner, Math.Max(1, ColumnDefinitions.Count)); SetRowSpan(adorner, Math.Max(1, RowDefinitions.Count));
        Canvas.SetZIndex(adorner, 1000);
        var ghost = new Border { Name = "PART_SplitterPreview", Width = splitter.ActualWidth, Height = splitter.ActualHeight,
            Background = splitter.BackgroundWhileDragging ?? DockChrome.Palette(manager).Accent,
            Opacity = double.IsFinite(splitter.OpacityWhileDragging) ? Math.Clamp(splitter.OpacityWhileDragging, 0, 1) : 1,
            IsHitTestVisible = false };
        adorner.Children.Add(ghost);
        _resize = new(splitter, index, a, b, root, manager, horizontal,
            horizontal ? a.DockWidth : a.DockHeight, horizontal ? b.DockWidth : b.DockHeight,
            first, second, minA, minB, _displayed, adorner, ghost);
        root.Updated += ResizeInvalidated; manager.LayoutChanged += ResizeInvalidated;
        Children.Add(adorner); PlacePreview(_resize);
    }
    private bool IsStructureCurrent(ResizeSession s) => IsLoaded && s.Splitter.IsEnabled && ReferenceEquals(s.Root, _group.Root) && ReferenceEquals(s.Root, s.Manager.Layout) &&
        (Orientation == Orientation.Horizontal) == s.Horizontal && Children.Contains(s.Splitter) &&
        _group.Children.OfType<ILayoutPanelElement>().Where(c => c.IsVisible).SequenceEqual(s.Order, ReferenceEqualityComparer.Instance) &&
        (s.Horizontal ? s.Before.DockMinWidth : s.Before.DockMinHeight) == s.MinBefore &&
        (s.Horizontal ? s.After.DockMinWidth : s.After.DockMinHeight) == s.MinAfter;
    private bool IsCurrent(ResizeSession s) => IsStructureCurrent(s) &&
        (s.Horizontal ? s.Before.DockWidth : s.Before.DockHeight) == s.BeforeLength &&
        (s.Horizontal ? s.After.DockWidth : s.After.DockHeight) == s.AfterLength;
    private void ResizeInvalidated(object? sender, EventArgs e) => ValidateResize();
    private void ValidateResize() { if (_resize is { } s && !IsCurrent(s)) CancelResize(); }
    private void CancelResize()
    {
        if (_resize is not { } s) return;
        DetachResize(s); s.Splitter.CancelDrag();
    }
    private void DetachResize(ResizeSession s)
    {
        if (!ReferenceEquals(_resize, s)) return;
        _resize = null; s.Root.Updated -= ResizeInvalidated; s.Manager.LayoutChanged -= ResizeInvalidated;
        Children.Remove(s.Adorner); s.Adorner.Children.Clear();
    }
    private void PreviewResize(LayoutGridResizerControl splitter, double displacement)
    {
        if (_resize is not { } s || !ReferenceEquals(s.Splitter, splitter)) return;
        if (!IsCurrent(s)) { CancelResize(); return; }
        var pair = DockSplitSolver.ResizePair(s.BeforePixels, s.AfterPixels, displacement, s.MinBefore, s.MinAfter);
        s.Displacement = pair.Before - s.BeforePixels; PlacePreview(s);
    }
    private void PlacePreview(ResizeSession s)
    {
        var origin = s.Splitter.TransformToVisual(this).TransformPoint(new(0, 0));
        Canvas.SetLeft(s.Ghost, origin.X + (s.Horizontal ? s.Displacement : 0));
        Canvas.SetTop(s.Ghost, origin.Y + (s.Horizontal ? 0 : s.Displacement));
    }
    private void EndResize(LayoutGridResizerControl splitter, bool canceled)
    {
        if (_resize is not { } s || !ReferenceEquals(s.Splitter, splitter)) return;
        var commit = !canceled && IsCurrent(s) && Math.Abs(s.Displacement) > 1e-8;
        // Clear capture/session subscriptions before publishing to arbitrary model observers.
        DetachResize(s);
        if (!commit) return;
        var pair = DockSplitSolver.ResizePair(s.BeforePixels, s.AfterPixels, s.Displacement, s.MinBefore, s.MinAfter);
        var bothStars = s.BeforeLength.IsStar && s.AfterLength.IsStar;
        var totalPixels = s.BeforePixels + s.AfterPixels;
        // Preserve the original star ratio plus the requested displacement. Rebuilding
        // the ratio from rounded device-aligned grid pixels causes a jump at drag start.
        var starDelta = bothStars ? (s.BeforeLength.Value + s.AfterLength.Value) * s.Displacement / totalPixels : 0;
        var a = s.BeforeLength.IsStar ? new GridLength(bothStars ? Math.Max(0, s.BeforeLength.Value + starDelta) : pair.Before / totalPixels, GridUnitType.Star) : new GridLength(pair.Before);
        var b = s.AfterLength.IsStar ? new GridLength(bothStars ? Math.Max(0, s.AfterLength.Value - starDelta) : pair.After / totalPixels, GridUnitType.Star) : new GridLength(pair.After);
        using var batch = s.Root.BeginUpdate();
        // Property notifications can replace the workspace or edit the other endpoint.
        // Revalidate after each callback, and roll back only values this operation owns.
        var attemptedAfter = false;
        try
        {
            Write(s.Before, a);
            if (!IsStructureCurrent(s) || Read(s.Before) != a || Read(s.After) != s.AfterLength)
            { Rollback(); return; }
            attemptedAfter = true;
            Write(s.After, b);
            if (!IsStructureCurrent(s) || Read(s.Before) != a || Read(s.After) != b) Rollback();
        }
        catch (Exception original)
        {
            try { Rollback(); }
            catch (Exception rollback) { throw new AggregateException("Resize and observer rollback both failed.", original, rollback); }
            throw;
        }
        GridLength Read(ILayoutPositionableElement item) => s.Horizontal ? item.DockWidth : item.DockHeight;
        void Write(ILayoutPositionableElement item, GridLength value)
        { if (s.Horizontal) item.DockWidth = value; else item.DockHeight = value; }
        void Rollback()
        {
            // Never overwrite an application's competing edit. Each setter may invoke
            // observers again; attempt both owned restorations even when one throws.
            List<Exception>? failures = null;
            try { if (attemptedAfter && Read(s.After) == b) Write(s.After, s.AfterLength); }
            catch (Exception e) { (failures ??= []).Add(e); }
            try { if (Read(s.Before) == a) Write(s.Before, s.BeforeLength); }
            catch (Exception e) { (failures ??= []).Add(e); }
            if (failures != null) throw new AggregateException("Resize rollback observers failed.", failures);
        }
    }
    private void ResizeOnce(LayoutGridResizerControl splitter, int index, double delta)
    {
        BeginResize(splitter, index);
        try { PreviewResize(splitter, delta); EndResize(splitter, false); }
        finally { CancelResize(); }
    }
}
