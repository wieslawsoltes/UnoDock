using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Input;
using UnoDock.Compatibility;
using UnoDock.Internal;

namespace UnoDock.Controls;

public class DropDownControlArea : UserControl
{
    public static readonly DependencyProperty DropDownContextMenuProperty = DependencyProperty.Register(nameof(DropDownContextMenu), typeof(MenuFlyout), typeof(DropDownControlArea), new PropertyMetadata(null, (d, _) => ((DropDownControlArea)d)._session?.Close()));
    public static readonly DependencyProperty DropDownContextMenuDataContextProperty = DependencyProperty.Register(nameof(DropDownContextMenuDataContext), typeof(object), typeof(DropDownControlArea), new PropertyMetadata(null, (d, _) => ((DropDownControlArea)d)._session?.Refresh()));
    private readonly DropDownMenuSession _session;
    private uint? _rightPointer;
    private bool _downHandled, _suppressMouseRightTap;
    private long _inputGeneration;
    public MenuFlyout? DropDownContextMenu
    {
        get => (MenuFlyout?)GetValue(DropDownContextMenuProperty);
        set => SetValue(DropDownContextMenuProperty, value);
    }
    public object? DropDownContextMenuDataContext
    {
        get => GetValue(DropDownContextMenuDataContextProperty);
        set => SetValue(DropDownContextMenuDataContextProperty, value);
    }

    public DropDownControlArea()
    {
        _session = new(this, () => DropDownContextMenu, () => DropDownContextMenuDataContext ?? DataContext, _ =>
        {
        });
        Unloaded += (_, _) =>
        {
            ResetPointer();
            _session.Close();
        };
        IsEnabledChanged += (_, _) =>
        {
            if (!IsEnabled)
            {
                ResetPointer();
                _session.Close();
            }
        };
        DataContextChanged += (_, _) => _session.Refresh();
        AddHandler(PointerPressedEvent, new PointerEventHandler(RightPressed), true);
        AddHandler(PointerReleasedEvent, new PointerEventHandler(RightReleased), true);
        AddHandler(PointerMovedEvent, new PointerEventHandler(ButtonTransition), true);
        ContextRequested += (_, e) =>
        {
            if (e.Handled || !IsEnabled)
                return;
            if (e.TryGetPosition(this, out var position))
            {
                if (_suppressMouseRightTap)
                {
                    e.Handled = true;
                    return;
                }

                _session.Open(position);
            }
            else
                _session.Open();
            if (_session.IsRequested)
                e.Handled = true;
        };
        PointerCanceled += (_, _) => ResetPointer();
        PointerCaptureLost += (_, e) =>
        {
            if (ReferenceEquals(e.OriginalSource, this))
                ResetPointer();
        };
    }

    public void OpenDropDown(Point? position = null) => _session.Open(position);
    public void CloseDropDown() => _session.Close();
    /// <summary>Local compatibility stage over the original native right-button press.</summary>
    protected virtual void OnMouseRightButtonDown(DockMouseButtonEventArgs e)
    {
    }

    /// <summary>Local compatibility stage, not a synthetic WPF tunnel. Mark Handled
        /// or omit the base call to suppress the default context-menu opening.</summary>
        protected virtual void OnPreviewMouseRightButtonUp(DockMouseButtonEventArgs e)
    {
        if (e.Handled)
            return;
        _session.Open(e.GetPosition(this));
        if (_session.IsRequested)
            e.Handled = true;
    }

    private void RightPressed(object sender, PointerRoutedEventArgs e)
    {
        if (!IsEnabled || e.Handled || e.GetCurrentPoint(this).Properties.PointerUpdateKind != PointerUpdateKind.RightButtonPressed)
            return;
        _inputGeneration++;
        _rightPointer = e.Pointer.PointerId;
        _suppressMouseRightTap = true;
        _downHandled = false;
        var args = new DockMouseButtonEventArgs(e, this, DockMouseButton.Right, true);
        try
        {
            OnMouseRightButtonDown(args);
            _downHandled = args.Handled;
        }
        catch
        {
            ResetPointer();
            throw;
        }
        finally
        {
            args.Complete();
        }
    }

    private void RightReleased(object sender, PointerRoutedEventArgs e)
    {
        if (_rightPointer != e.Pointer.PointerId || e.GetCurrentPoint(this).Properties.PointerUpdateKind != PointerUpdateKind.RightButtonReleased)
            return;
        _rightPointer = null;
        var generation = _inputGeneration;
        var args = new DockMouseButtonEventArgs(e, this, DockMouseButton.Right, false)
        {
            Handled = _downHandled || e.Handled
        };
        try
        {
            if (IsEnabled)
                OnPreviewMouseRightButtonUp(args);
        }
        finally
        {
            args.Complete();
            // Mouse RightTapped can arrive before or after PointerReleased. The
            // actual right-button protocol above is its only opening authority.
            DispatcherQueue.TryEnqueue(() =>
            {
                if (_inputGeneration == generation)
                    _suppressMouseRightTap = false;
            });
        }
    }

    private void ButtonTransition(object sender, PointerRoutedEventArgs e)
    {
        var kind = e.GetCurrentPoint(this).Properties.PointerUpdateKind;
        if (kind == PointerUpdateKind.RightButtonPressed)
            RightPressed(sender, e);
        else if (kind == PointerUpdateKind.RightButtonReleased)
            RightReleased(sender, e);
    }

    private void ResetPointer()
    {
        _inputGeneration++;
        _rightPointer = null;
        _downHandled = false;
        _suppressMouseRightTap = false;
    }

    protected override void OnRightTapped(RightTappedRoutedEventArgs e)
    {
        base.OnRightTapped(e);
        if (e.Handled || !IsEnabled)
            return;
        if (e.PointerDeviceType == PointerDeviceType.Mouse && _suppressMouseRightTap)
        {
            e.Handled = true;
            return;
        }

        _session.Open(e.GetPosition(this));
        if (_session.IsRequested)
            e.Handled = true;
    }

    protected override void OnKeyDown(KeyRoutedEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled)
            return;
        if (e.Key == Windows.System.VirtualKey.Escape && _session.IsRequested)
        {
            _session.Close();
            e.Handled = true;
            return;
        }

        if (DropDownKeyboard.IsContextRequest(this, e))
        {
            _session.Open();
            if (_session.IsRequested)
                e.Handled = true;
        }
    }
}
