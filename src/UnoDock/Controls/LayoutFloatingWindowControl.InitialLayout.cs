namespace UnoDock.Controls;

public abstract partial class LayoutFloatingWindowControl
{
    private void QueueInitialNativeLayout(Window window)
    {
        if (!OperatingSystem.IsLinux())
            return;
        // Run after synchronous owner/chrome initialization has finished. The
        // captured window may have closed or been replaced before this dispatch.
        DispatcherQueue.TryEnqueue(() =>
        {
            if (_hostDisposed || _closingHost || !ReferenceEquals(_window, window))
                return;
            try
            {
                _dragCoordinates.RefreshInitialNativeLayout(window);
            }
            catch (Exception error)
            {
                ReportFilterFailure(error);
            }
        });
    }
}
