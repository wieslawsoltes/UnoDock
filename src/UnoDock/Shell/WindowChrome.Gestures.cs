using Microsoft.UI.Xaml.Input;
using UnoDock.Controls;

namespace Microsoft.Windows.Shell;
public partial class WindowChrome
{
    private sealed partial class Attachment
    {
        private Pointer? _dragPointer;
        private Point _dragOrigin;
        private DockRect _dragBounds;
        private ChromeHit _dragHit;
        private void AttachGestures(FrameworkElement root)
        {
            root.PointerMoved += ManagedMove;
            root.PointerReleased += ManagedRelease;
            root.PointerCanceled += ManagedCanceled;
            root.PointerCaptureLost += ManagedCanceled;
            root.KeyDown += ManagedKeyDown;
            root.DoubleTapped += DoubleTapped;
        }

        private void DetachGestures(FrameworkElement root)
        {
            EndManagedDrag(restore: true);
            root.PointerMoved -= ManagedMove;
            root.PointerReleased -= ManagedRelease;
            root.PointerCanceled -= ManagedCanceled;
            root.PointerCaptureLost -= ManagedCanceled;
            root.KeyDown -= ManagedKeyDown;
            root.DoubleTapped -= DoubleTapped;
        }

        private void BeginManagedDrag(LayoutFloatingWindowControl window, ChromeHit hit, PointerRoutedEventArgs e)
        {
            if (_dragPointer != null || window.IsMaximized || _root == null)
                return;
            var point = e.GetCurrentPoint(null).Position;
            if (!_root.CapturePointer(e.Pointer))
                return;
            _dragBounds = window.Bounds;
            _dragOrigin = point;
            _dragHit = hit;
            _dragPointer = e.Pointer;
            e.Handled = true;
        }

        private void ManagedMove(object sender, PointerRoutedEventArgs e)
        {
            if (_dragPointer?.PointerId != e.Pointer.PointerId || _control is not LayoutFloatingWindowControl window)
                return;
            var point = e.GetCurrentPoint(null).Position;
            window.SetChromeBounds(ChromeResize.Apply(_dragBounds, _dragHit, point.X - _dragOrigin.X, point.Y - _dragOrigin.Y, window.MinWidth, window.MinHeight, window.MaxWidth, window.MaxHeight));
            e.Handled = true;
        }

        private void ManagedRelease(object sender, PointerRoutedEventArgs e)
        {
            if (_dragPointer?.PointerId != e.Pointer.PointerId)
                return;
            ManagedMove(sender, e);
            EndManagedDrag(restore: false);
            e.Handled = true;
        }

        private void ManagedCanceled(object sender, PointerRoutedEventArgs e)
        {
            if (_dragPointer?.PointerId == e.Pointer.PointerId)
                EndManagedDrag(restore: true);
        }

        private void ManagedKeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key != global::Windows.System.VirtualKey.Escape || _dragPointer == null)
                return;
            EndManagedDrag(restore: true);
            e.Handled = true;
        }

        private void EndManagedDrag(bool restore)
        {
            if (_dragPointer == null)
                return;
            var pointer = _dragPointer;
            _dragPointer = null;
            if (restore && _control is LayoutFloatingWindowControl window)
                window.SetChromeBounds(_dragBounds);
            _root?.ReleasePointerCapture(pointer);
        }

        private void DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            if (e.Handled || _control is not LayoutFloatingWindowControl window || _root?.IsLoaded != true || Hit(e.GetPosition(_root)) != ChromeHit.Caption)
                return;
            var command = window.IsMaximized ? SystemCommands.RestoreWindowCommand : SystemCommands.MaximizeWindowCommand;
            if (command.CanExecute(window))
            {
                command.Execute(window);
                e.Handled = true;
            }
        }
    }
}
