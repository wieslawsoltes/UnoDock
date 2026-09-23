using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Input;
using UnoDock.Compatibility;

namespace UnoDock.Controls;

/// <summary>
/// Composed control with the named AvalonDock mouse/focus extension points. These
/// stages run locally when Uno delivers the native event, not on a fabricated WPF tunnel.
/// </summary>
public abstract class DockInputControl : ContentControl
{
    private readonly Dictionary<uint, DockMouseButton> _pressed = [];
    private WeakReference<DependencyObject>? _lastFocus, _incomingOldFocus, _incomingNewFocus;
    internal virtual FrameworkElement DockCaptureElement => this;
    internal event PointerEventHandler? DockDragMoved, DockDragReleased;
    internal event EventHandler<uint>? DockInputCancelled;
    protected DockInputControl()
    {
        AddHandler(PointerPressedEvent, new PointerEventHandler(Pressed), true);
        AddHandler(PointerReleasedEvent, new PointerEventHandler(Released), true);
        AddHandler(PointerMovedEvent, new PointerEventHandler(Moved), true);
        PointerEntered += (_, e) => Dispatch(e, OnMouseEnter);
        PointerExited += (_, e) => Dispatch(e, OnMouseLeave);
        PointerCanceled += (_, e) => CancelInput(e.Pointer.PointerId);
        PointerCaptureLost += Cancelled;
        Unloaded += (_, _) => CancelAllInput();
        IsEnabledChanged += (_, _) => { if (!IsEnabled) CancelAllInput(); };
        GettingFocus += (_, e) =>
        {
            var args = new DockKeyboardFocusChangedEventArgs(e, e.OldFocusedElement, e.NewFocusedElement);
            try { OnPreviewGotKeyboardFocus(args); }
            finally
            {
                args.Complete();
                _incomingOldFocus = !e.Cancel && e.OldFocusedElement != null ? new(e.OldFocusedElement) : null;
                _incomingNewFocus = !e.Cancel && e.NewFocusedElement != null ? new(e.NewFocusedElement) : null;
            }
        };
        GotFocus += (_, e) =>
        {
            DependencyObject? previous = null;
            _lastFocus?.TryGetTarget(out previous);
            var current = XamlRoot == null ? e.OriginalSource as DependencyObject : FocusManager.GetFocusedElement(XamlRoot) as DependencyObject;
            if (_incomingNewFocus?.TryGetTarget(out var expected) == true && ReferenceEquals(expected, current))
                _incomingOldFocus?.TryGetTarget(out previous);
            _incomingOldFocus = null; _incomingNewFocus = null;
            var args = new DockKeyboardFocusChangedEventArgs(e, previous, current);
            _lastFocus = current == null ? null : new(current);
            OnGotKeyboardFocus(args);
        };
    }
    /// <summary>Allows a composed control to exclude interactive action buttons from its header input.</summary>
    protected virtual bool AcceptsPointerEvent(PointerRoutedEventArgs e) => true;
    private void Dispatch(PointerRoutedEventArgs e, Action<DockMouseEventArgs> action)
    {
        if (!IsEnabled || !AcceptsPointerEvent(e)) return;
        var args = new DockMouseEventArgs(e, this);
        try { action(args); } finally { args.Complete(); }
    }
    private void Pressed(object sender, PointerRoutedEventArgs e)
    {
        if (!IsEnabled || !AcceptsPointerEvent(e)) return;
        var button = ChangedButton(e, true);
        if (button == null) return;
        _pressed.TryAdd(e.Pointer.PointerId, button.Value);
        var args = new DockMouseButtonEventArgs(e, this, button.Value, true);
        try
        {
            if (button == DockMouseButton.Left) OnPreviewMouseLeftButtonDown(args);
            else if (button == DockMouseButton.Right) OnPreviewMouseRightButtonDown(args);
            if (!args.Handled) OnMouseDown(args);
        }
        catch { CancelInput(e.Pointer.PointerId); throw; }
        finally { args.Complete(); }
    }
    private void Released(object sender, PointerRoutedEventArgs e)
    {
        var id = e.Pointer.PointerId;
        if (!_pressed.TryGetValue(id, out var initial)) return;
        var button = ChangedButton(e, false) ?? initial;
        // A secondary button's release must not end the primary captured gesture.
        if (button == initial) _pressed.Remove(id);
        var args = new DockMouseButtonEventArgs(e, this, button, false);
        try
        {
            if (button == DockMouseButton.Right) OnPreviewMouseRightButtonUp(args);
            if (!args.Handled && IsEnabled)
            {
                if (button == DockMouseButton.Left) OnMouseLeftButtonUp(args);
                else if (button == DockMouseButton.Right) OnMouseRightButtonUp(args);
            }
        }
        catch { CancelInput(id); throw; }
        finally
        {
            args.Complete();
            // A vetoed release still ends capture, without executing a drop.
            if (button == initial) DockInputCancelled?.Invoke(this, id);
        }
    }
    private DockMouseButton? ChangedButton(PointerRoutedEventArgs e, bool pressed)
    {
        var point = e.GetCurrentPoint(this);
        return point.Properties.PointerUpdateKind switch
        {
            PointerUpdateKind.LeftButtonPressed or PointerUpdateKind.LeftButtonReleased => DockMouseButton.Left,
            PointerUpdateKind.RightButtonPressed or PointerUpdateKind.RightButtonReleased => DockMouseButton.Right,
            PointerUpdateKind.MiddleButtonPressed or PointerUpdateKind.MiddleButtonReleased => DockMouseButton.Middle,
            PointerUpdateKind.XButton1Pressed or PointerUpdateKind.XButton1Released => DockMouseButton.XButton1,
            PointerUpdateKind.XButton2Pressed or PointerUpdateKind.XButton2Released => DockMouseButton.XButton2,
            _ when e.Pointer.PointerDeviceType != PointerDeviceType.Mouse => DockMouseButton.Left,
            _ when pressed && point.Properties.IsLeftButtonPressed => DockMouseButton.Left,
            _ when pressed && point.Properties.IsRightButtonPressed => DockMouseButton.Right,
            _ when pressed && point.Properties.IsMiddleButtonPressed => DockMouseButton.Middle,
            _ => null
        };
    }
    private void Moved(object sender, PointerRoutedEventArgs e)
    {
        // WinUI reports changes to secondary mouse buttons as PointerMoved while
        // another button is held. Preserve those WPF-style button transitions.
        var kind = e.GetCurrentPoint(this).Properties.PointerUpdateKind;
        if (kind is PointerUpdateKind.LeftButtonPressed or PointerUpdateKind.RightButtonPressed or PointerUpdateKind.MiddleButtonPressed or PointerUpdateKind.XButton1Pressed or PointerUpdateKind.XButton2Pressed)
        { Pressed(sender, e); return; }
        if (kind is PointerUpdateKind.LeftButtonReleased or PointerUpdateKind.RightButtonReleased or PointerUpdateKind.MiddleButtonReleased or PointerUpdateKind.XButton1Released or PointerUpdateKind.XButton2Released)
        { Released(sender, e); return; }
        try { Dispatch(e, OnMouseMove); }
        catch { CancelInput(e.Pointer.PointerId); throw; }
    }
    private void Cancelled(object sender, PointerRoutedEventArgs e)
    {
        // A bubbled loss from an unrelated child is not loss of this control's
        // chosen capture element (the inner label for composed tab headers).
        if (ReferenceEquals(e.OriginalSource, DockCaptureElement)) CancelInput(e.Pointer.PointerId);
    }
    private void CancelInput(uint id) { _pressed.Remove(id); DockInputCancelled?.Invoke(this, id); }
    private void CancelAllInput()
    {
        var pointers = _pressed.Keys.ToArray(); _pressed.Clear();
        foreach (var id in pointers) DockInputCancelled?.Invoke(this, id);
    }
    protected virtual void OnMouseDown(DockMouseButtonEventArgs e)
    {
        if (e.ChangedButton == DockMouseButton.Left) OnMouseLeftButtonDown(e);
        else if (e.ChangedButton == DockMouseButton.Right) OnMouseRightButtonDown(e);
    }
    protected virtual void OnMouseLeftButtonDown(DockMouseButtonEventArgs e) { }
    protected virtual void OnMouseRightButtonDown(DockMouseButtonEventArgs e) { }
    protected virtual void OnMouseLeftButtonUp(DockMouseButtonEventArgs e)
    { if (!e.Handled) DockDragReleased?.Invoke(this, e.NativeEvent); }
    protected virtual void OnMouseRightButtonUp(DockMouseButtonEventArgs e) { }
    protected virtual void OnPreviewMouseLeftButtonDown(DockMouseButtonEventArgs e) { }
    protected virtual void OnPreviewMouseRightButtonDown(DockMouseButtonEventArgs e) { }
    protected virtual void OnPreviewMouseRightButtonUp(DockMouseButtonEventArgs e) { }
    protected virtual void OnMouseEnter(DockMouseEventArgs e) { }
    protected virtual void OnMouseLeave(DockMouseEventArgs e) { }
    protected virtual void OnMouseMove(DockMouseEventArgs e)
    { if (!e.Handled) DockDragMoved?.Invoke(this, e.NativeEvent); }
    protected virtual void OnGotKeyboardFocus(DockKeyboardFocusChangedEventArgs e) { }
    protected virtual void OnPreviewGotKeyboardFocus(DockKeyboardFocusChangedEventArgs e) { }
    protected virtual IEnumerator LogicalChildren => (Content is DependencyObject child ? new[] { child } : Array.Empty<DependencyObject>()).GetEnumerator();
}

/// <summary>Selection event extension for the independent cached tab host.</summary>
public abstract class DockSelectionControl : DockInputControl
{
    public event SelectionChangedEventHandler? SelectionChanged;
    protected virtual void OnSelectionChanged(SelectionChangedEventArgs e) => SelectionChanged?.Invoke(this, e);
}
