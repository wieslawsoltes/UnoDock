using System.Diagnostics;
using Microsoft.UI.Xaml.Input;
using UnoDock.Controls;

namespace UnoDock.Internal;

internal sealed partial class DockSurface
{
    // A stationary pointer does not need guides re-resolved on every 16 ms tick;
    // refresh at a low rate so layout changes under it are still observed.
    private static readonly TimeSpan StationaryAdornerRefresh = TimeSpan.FromMilliseconds(250);
    private Point? _adornerPoint;
    private DockDropPlan? _adornerPlan;
    private long _adornerTick;
    private UIElement? _escapeRoot;
    private KeyEventHandler? _escapeHandler;
    private void StartDragTimer()
    {
        if (_dragScrollTimer.IsEnabled)
            return;
        _lastScrollTick = Stopwatch.GetTimestamp();
        _dragScrollTimer.Start();
        ObserveEscape();
    }

    private void StopDragTimer()
    {
        _dragScrollTimer.Stop();
        _adornerPoint = null;
        _adornerPlan = null;
        ReleaseEscape();
    }

    private DockDropPlan? RefreshDragAdorners(Point point)
    {
        var plan = UpdateDragAdorners(point);
        _adornerPoint = point;
        _adornerPlan = plan;
        _adornerTick = Stopwatch.GetTimestamp();
        return plan;
    }

    private DockDropPlan? CurrentDragAdorners(Point point, long now) => _adornerPoint == point && Stopwatch.GetElapsedTime(_adornerTick, now) < StationaryAdornerRefresh ? _adornerPlan : RefreshDragAdorners(point);
    /// <summary>Escape cancels a drag even while keyboard focus is outside the
        /// docking visuals: key events are observed at this window's root (handled
        /// ones included), and hosts with a global key query are polled per tick.</summary>
        private void ObserveEscape()
    {
        if (_escapeRoot != null || XamlRoot?.Content is not UIElement root)
            return;
        _escapeHandler ??= OnEscapeKey;
        _escapeRoot = root;
        root.AddHandler(KeyDownEvent, _escapeHandler, true);
    }

    private void ReleaseEscape()
    {
        if (_escapeRoot is { } root && _escapeHandler != null)
            root.RemoveHandler(KeyDownEvent, _escapeHandler);
        _escapeRoot = null;
    }

    private void OnEscapeKey(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Escape && _dragContent != null)
            CancelDrag();
    }
}
