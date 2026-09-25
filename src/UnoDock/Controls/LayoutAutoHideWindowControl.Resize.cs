using System.Runtime.ExceptionServices;
using UnoDock.Internal;
using UnoDock.Layout;

namespace UnoDock.Controls;

public partial class LayoutAutoHideWindowControl
{
    private ResizeSession? _resizeSession;
    private sealed record ResizeSession(LayoutAnchorable Model, DockingManager Manager, LayoutRoot Root, DockSurface Surface, AnchorSide Side, FlowDirection Flow, Rect Viewport, double Original, double Initial, double Minimum, double Maximum, double Gutter, Point Origin, Canvas Layer, Border Ghost)
    {
        internal double Extent { get; set; } = Initial;
        internal bool Horizontal => Side is AnchorSide.Left or AnchorSide.Right;
        internal double Sign => Side is AnchorSide.Left or AnchorSide.Top ? 1 : -1;
    }

    private void BeginResize()
    {
        if (_resizeSession != null)
            return;
        if (_model is not { IsAutoHidden: true, IsEnabled: true } model || _manager?.Surface is not { } surface || !IsLoaded || !IsEnabled || !ReferenceEquals(model.Root, _manager.Layout))
        {
            _resizer.CancelDrag();
            return;
        }

        var side = model.GetSide();
        var horizontal = side is AnchorSide.Left or AnchorSide.Right;
        var initial = Math.Max(0, (horizontal ? ActualWidth : ActualHeight) - _gutter);
        var maximum = Math.Max(0, (horizontal ? _viewport.Width : _viewport.Height) - _gutter);
        if (initial <= 0 || maximum <= 0)
        {
            _resizer.CancelDrag();
            return;
        }

        var layer = new Canvas
        {
            IsHitTestVisible = false
        };
        var ghost = new Border
        {
            Name = "PART_AutoHideResizePreview",
            IsHitTestVisible = false,
            Width = _resizer.ActualWidth,
            Height = _resizer.ActualHeight,
            Background = _resizer.BackgroundWhileDragging,
            Opacity = double.IsFinite(_resizer.OpacityWhileDragging) ? Math.Clamp(_resizer.OpacityWhileDragging, 0, 1) : .5
        };
        layer.Children.Add(ghost);
        Canvas.SetZIndex(layer, 1000);
        var origin = _resizer.TransformToVisual(surface.AutoHideLayer).TransformPoint(default);
        var s = new ResizeSession(model, _manager, _manager.Layout, surface, side, FlowDirection, _viewport, horizontal ? model.AutoHideWidth : model.AutoHideHeight, initial, horizontal ? model.AutoHideMinWidth : model.AutoHideMinHeight, maximum, _gutter, origin, layer, ghost);
        _resizeSession = s;
        s.Root.Updated += ResizeInvalidated;
        s.Manager.LayoutChanging += ResizeInvalidated;
        surface.AutoHideLayer.Children.Add(layer);
        surface.StopAutoHideTimer();
        PlacePreview(s);
    }

    private bool Current(ResizeSession s) => IsLoaded && IsEnabled && _resizer.IsEnabled && s.Model.IsEnabled && s.Model.IsAutoHidden && ReferenceEquals(_model, s.Model) && ReferenceEquals(s.Manager, _manager) && ReferenceEquals(s.Model.Root, s.Root) && ReferenceEquals(s.Manager.Layout, s.Root) && ReferenceEquals(s.Manager.Surface, s.Surface) && s.Model.GetSide() == s.Side && FlowDirection == s.Flow && _gutter == s.Gutter && _viewport == s.Viewport && s.Surface.AutoHideClientRect == s.Viewport && (s.Horizontal ? s.Model.AutoHideWidth : s.Model.AutoHideHeight) == s.Original && (s.Horizontal ? s.Model.AutoHideMinWidth : s.Model.AutoHideMinHeight) == s.Minimum;
    private void ResizeInvalidated(object? sender, EventArgs e) => ValidateResize();
    private void ValidateResize()
    {
        if (_resizeSession is { } s && !Current(s))
            CancelResize();
    }

    private void CancelResize()
    {
        if (_resizeSession is not { } s)
            return;
        DetachResize(s);
        _resizer.CancelDrag();
    }

    private void DetachResize(ResizeSession s)
    {
        if (!ReferenceEquals(s, _resizeSession))
            return;
        _resizeSession = null;
        s.Root.Updated -= ResizeInvalidated;
        s.Manager.LayoutChanging -= ResizeInvalidated;
        s.Surface.AutoHideLayer.Children.Remove(s.Layer);
        s.Layer.Children.Clear();
    }

    private void PreviewResize(double displacement)
    {
        if (_resizeSession is not { } s)
            return;
        if (!Current(s))
        {
            CancelResize();
            return;
        }

        s.Extent = Math.Clamp(s.Initial + displacement * s.Sign, Math.Min(s.Minimum, s.Maximum), s.Maximum);
        PlacePreview(s);
    }

    private static void PlacePreview(ResizeSession s)
    {
        var displacement = (s.Extent - s.Initial) * s.Sign;
        Canvas.SetLeft(s.Ghost, s.Origin.X + (s.Horizontal ? displacement : 0));
        Canvas.SetTop(s.Ghost, s.Origin.Y + (s.Horizontal ? 0 : displacement));
    }

    private void FinishResize(bool canceled)
    {
        if (_resizeSession is not { } s)
            return;
        var commit = !canceled && Current(s) && Math.Abs(s.Extent - s.Initial) > .000001;
        DetachResize(s);
        try
        {
            if (!commit)
                return;
            using var batch = s.Root.BeginUpdate();
            try
            {
                Write(s.Extent);
            }
            catch (Exception error)
            {
                // Only our value may be rolled back; an application replacement wins.
                if ((s.Horizontal ? s.Model.AutoHideWidth : s.Model.AutoHideHeight) == s.Extent)
                {
                    try
                    {
                        Write(s.Original);
                    }
                    catch (Exception rollback)
                    {
                        throw new AggregateException("Auto-hide resize and rollback observers failed.", error, rollback);
                    }
                }

                ExceptionDispatchInfo.Capture(error).Throw();
            }
        }
        finally
        {
            if (ReferenceEquals(_model, s.Model) && ReferenceEquals(_manager?.Layout, s.Root))
            {
                s.Surface.PositionAutoHide();
                if (!RetainOpen)
                    s.Surface.StartAutoHideTimer();
            }
        }

        void Write(double value)
        {
            if (s.Horizontal)
                s.Model.AutoHideWidth = value;
            else
                s.Model.AutoHideHeight = value;
        }
    }
}
