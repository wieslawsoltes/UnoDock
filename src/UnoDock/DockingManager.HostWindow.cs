using System.Diagnostics;
using Microsoft.Windows.Shell;

namespace UnoDock;

public partial class DockingManager
{
    private Window? _hostWindow;
    private IDisposable? _hostWindowLease;
    private bool _hostWindowDiagnosed;
    /// <summary>The native window hosting this manager, resolved whenever it loads.
        /// It owns the floating windows, which close together with it.</summary>
        internal Window? HostWindow => _hostWindow;

    private void AttachHostWindow()
    {
        var window = ResolveHostWindow();
        if (ReferenceEquals(window, _hostWindow))
            return;
        DetachHostWindow();
        if (window == null)
        {
            ReportUnresolvedHostWindow();
            return;
        }

        _hostWindow = window;
        window.Closed += OnHostWindowClosed;
        // Register the host so every lookup (commands, native coordinates and
        // floating-window ownership) resolves it, including on WinUI, which
        // cannot enumerate application windows.
        _hostWindowLease = WindowRegistry.Register(window);
    }

    private Window? ResolveHostWindow()
    {
        if (XamlRoot == null || !DispatcherQueue.HasThreadAccess)
            return null;
        var window = WindowRegistry.Find(this);
#if WINDOWS
        if (window == null)
        {
            try
            {
                // A registered window whose content moved to another island of the
                // same top-level window still owns this manager.
                var id = XamlRoot.ContentIslandEnvironment?.AppWindowId;
                if (id is { } value && value.Value != 0)
                    window = WindowRegistry.Snapshot().FirstOrDefault(w => w.AppWindow.Id.Value == value.Value);
            }
            catch (Exception error) when (error is InvalidOperationException or System.Runtime.InteropServices.COMException)
            {
            }
        }

#endif
        return window != null && !WindowRegistry.IsClosed(window) ? window : null;
    }

    private void ReportUnresolvedHostWindow()
    {
        if (_hostWindowDiagnosed || OperatingSystem.IsBrowser() || OperatingSystem.IsAndroid() || OperatingSystem.IsIOS())
            return;
        _hostWindowDiagnosed = true;
        Debug.WriteLine("UnoDock: the Window hosting this DockingManager could not be resolved. Native floating windows will have no owner and will not close with it. On WinUI register the window with SystemCommands.RegisterWindow(window).");
    }

    private void OnHostWindowClosed(object sender, WindowEventArgs args)
    {
        if (!ReferenceEquals(sender, _hostWindow))
            return;
        DetachHostWindow();
        // Floating windows belong to their owner. A hidden WinUI host would
        // otherwise outlive the main window and keep the process running.
        _loaded = false;
        _surface?.CancelDrag();
        var windows = _floating.ToArray();
        _floating.Clear();
        foreach (var window in windows)
            window.CloseHost();
    }

    private void DetachHostWindow()
    {
        if (_hostWindow is { } window)
            window.Closed -= OnHostWindowClosed;
        _hostWindow = null;
        var lease = _hostWindowLease;
        _hostWindowLease = null;
        lease?.Dispose();
    }
}
