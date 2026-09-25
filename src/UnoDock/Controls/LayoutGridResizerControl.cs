using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Input;
using UnoDock.Internal;
using Windows.System;

namespace UnoDock.Controls;
/// <summary>Composes WinUI's sealed Thumb while retaining explicit drag lifetime and cancellation.</summary>
public partial class LayoutGridResizerControl : ContentControl
{
    public static readonly DependencyProperty BackgroundWhileDraggingProperty = DependencyProperty.Register(nameof(BackgroundWhileDragging), typeof(Brush), typeof(LayoutGridResizerControl), new PropertyMetadata(null));
    public static readonly DependencyProperty OpacityWhileDraggingProperty = DependencyProperty.Register(nameof(OpacityWhileDragging), typeof(double), typeof(LayoutGridResizerControl), new PropertyMetadata(.5d));
    public Brush? BackgroundWhileDragging { get => (Brush? )GetValue(BackgroundWhileDraggingProperty); set => SetValue(BackgroundWhileDraggingProperty, value); }
    public double OpacityWhileDragging { get => (double)GetValue(OpacityWhileDraggingProperty); set => SetValue(OpacityWhileDraggingProperty, value); }

    private readonly Thumb _thumb;
    private readonly Border _feedback = new()
    {
        IsHitTestVisible = false,
        Visibility = Visibility.Collapsed
    };
    private uint? _pointer;
    private Point _origin;
    private UIElement? _coordinateSpace;
    private bool _dragging, _ending, _horizontal;
    private long _generation;
    private NativeCompletion? _pendingCompletion;
    private sealed record NativeCompletion(DragCompletedEventArgs Args, long Generation, uint? Pointer)
    {
        internal bool Released { get; set; }
    }

    public bool IsDragging => _dragging;

    public event DragStartedEventHandler? DragStarted;
    public event DragDeltaEventHandler? DragDelta;
    public event DragCompletedEventHandler? DragCompleted;
    internal bool Horizontal
    {
        get => _horizontal;
        set
        {
            if (_horizontal == value)
                return;
            CancelDrag();
            _horizontal = value;
            UpdateCursor();
        }
    }

    internal event EventHandler? ResizeStarted;
    internal event EventHandler<double>? ResizePreview;
    internal event EventHandler<bool>? ResizeFinished;
    internal event EventHandler<double>? ResizeBy;
    public LayoutGridResizerControl()
    {
        BackgroundWhileDragging = new SolidColorBrush(Microsoft.UI.Colors.Black);
        IsTabStop = true;
        HorizontalContentAlignment = HorizontalAlignment.Stretch;
        VerticalContentAlignment = VerticalAlignment.Stretch;
        AutomationProperties.SetName(this, "Resize docked panes");
        _thumb = new Thumb
        {
            IsTabStop = false,
            Template = DockChrome.ThumbTemplate,
            Background = DockChrome.Transparent,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        var chrome = new Grid();
        chrome.Children.Add(_thumb);
        chrome.Children.Add(_feedback);
        Content = chrome;
        InitializeAutomation(chrome);
        _thumb.DragStarted += (_, e) =>
        {
            try
            {
                if (_pendingCompletion is { } previous)
                    CompleteNativeDrag(previous);
                BeginResize();
                DragStarted?.Invoke(this, e);
            }
            catch
            {
                CancelDrag();
                throw;
            }
        };
        _thumb.DragDelta += (_, e) =>
        {
            // Native displacement conventions differ across hosts. For actual pointer
            // input, the routed point in a stationary parent space is authoritative.
            try
            {
                DragDelta?.Invoke(this, e);
            }
            catch
            {
                CancelDrag();
                throw;
            }
        };
        _thumb.DragCompleted += (_, e) =>
        {
            // Thumb may finish with Canceled=false on capture loss, before the owner's
            // Unloaded/IsEnabledChanged or the routed PointerReleased event is delivered.
            // One UI-queue continuation allows the actual release to authorize commit.
            var completion = new NativeCompletion(e, _generation, _pointer);
            _pendingCompletion = completion;
            if (!_dragging || !DispatcherQueue.TryEnqueue(() => CompleteNativeDrag(completion)))
                CompleteNativeDrag(completion);
        };
        AddHandler(PointerReleasedEvent, new PointerEventHandler((_, e) =>
        {
            if (_pendingCompletion is { } pending && pending.Pointer == e.Pointer.PointerId && !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
                pending.Released = true;
        }), true);
        AddHandler(PointerPressedEvent, new PointerEventHandler((_, e) =>
        {
            if (!_dragging || _pointer != null)
                return;
            _coordinateSpace = VisualTreeHelper.GetParent(this) as UIElement ?? this;
            _pointer = e.Pointer.PointerId;
            _origin = e.GetCurrentPoint(_coordinateSpace).Position;
            Focus(FocusState.Pointer);
        }), true);
        AddHandler(PointerMovedEvent, new PointerEventHandler((_, e) =>
        {
            if (!_dragging || _pointer != e.Pointer.PointerId)
                return;
            var point = e.GetCurrentPoint(_coordinateSpace).Position;
            UpdateResize(Horizontal ? point.X - _origin.X : point.Y - _origin.Y);
        }), true);
        Unloaded += (_, _) => CancelDrag();
        IsEnabledChanged += (_, _) =>
        {
            if (!IsEnabled)
                CancelDrag();
        };
        RegisterPropertyChangedCallback(FlowDirectionProperty, (_, _) => CancelDrag());
        UpdateCursor();
    }

    private void CompleteNativeDrag(NativeCompletion completion)
    {
        if (!ReferenceEquals(_pendingCompletion, completion))
            return;
        _pendingCompletion = null;
        var e = completion.Args;
        var canceled = e.Canceled || !completion.Released || completion.Generation != _generation || !_dragging || !_thumb.IsEnabled || !IsEnabled || !IsLoaded;
        try
        {
            if (completion.Generation == _generation)
                EndResize(canceled);
        }
        finally
        {
            DragCompleted?.Invoke(this, canceled == e.Canceled ? e : new DragCompletedEventArgs(e.HorizontalChange, e.VerticalChange, canceled));
        }
    }

    private void UpdateCursor() => ProtectedCursor = Microsoft.UI.Input.InputSystemCursor.Create(Horizontal ? Microsoft.UI.Input.InputSystemCursorShape.SizeWestEast : Microsoft.UI.Input.InputSystemCursorShape.SizeNorthSouth);
    internal void BeginResize()
    {
        if (_dragging || _ending || !IsEnabled)
            return;
        _generation++;
        _dragging = true;
        RefreshAutomation();
        try
        {
            ResizeStarted?.Invoke(this, EventArgs.Empty);
            if (!_dragging)
                return;
            _feedback.Background = BackgroundWhileDragging;
            _feedback.Opacity = double.IsFinite(OpacityWhileDragging) ? Math.Clamp(OpacityWhileDragging, 0, 1) : 1;
            _feedback.Visibility = Visibility.Visible;
        }
        catch
        {
            CancelDrag();
            throw;
        }
    }

    internal void UpdateResize(double displacement)
    {
        if (!_dragging)
            return;
        if (!double.IsFinite(displacement))
        {
            CancelDrag();
            throw new ArgumentOutOfRangeException(nameof(displacement));
        }

        try
        {
            ResizePreview?.Invoke(this, displacement);
        }
        catch
        {
            CancelDrag();
            throw;
        }
    }

    internal void EndResize(bool canceled)
    {
        if (!_dragging || _ending)
            return;
        canceled |= !IsEnabled || !_thumb.IsEnabled || !IsLoaded;
        _dragging = false;
        _ending = true;
        _pointer = null;
        _coordinateSpace = null;
        _feedback.Visibility = Visibility.Collapsed;
        try
        {
            ResizeFinished?.Invoke(this, canceled);
        }
        finally
        {
            _ending = false;
            RefreshAutomation();
        }
    }

    /// <summary>Abandons the pending preview without changing persisted pane lengths.</summary>
    public void CancelDrag()
    {
        try
        {
            EndResize(true);
        }
        finally
        {
            if (_thumb.IsDragging)
                _thumb.CancelDrag();
        }
    }

    internal bool ResizeFromKey(VirtualKey key)
    {
        if (!IsEnabled)
            return false;
        if (key == VirtualKey.Escape && IsDragging)
        {
            CancelDrag();
            return true;
        }

        if (IsDragging)
            return false;
        if (ResizeBoundaryFromKey(key))
            return true;
        var delta = Horizontal ? key switch
        {
            VirtualKey.Left => -10,
            VirtualKey.Right => 10,
            _ => 0
        } : key switch
        {
            VirtualKey.Up => -10,
            VirtualKey.Down => 10,
            _ => 0
        };
        if (delta == 0)
            return false;
        if (Horizontal && FlowDirection == FlowDirection.RightToLeft)
            delta = -delta;
        ResizeBy?.Invoke(this, delta);
        return true;
    }

    protected override void OnKeyDown(KeyRoutedEventArgs e)
    {
        base.OnKeyDown(e);
        if (!e.Handled && ResizeFromKey(e.Key))
            e.Handled = true;
    }
}
