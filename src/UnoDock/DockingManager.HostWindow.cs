using System.Diagnostics;
using Microsoft.Windows.Shell;

namespace UnoDock;

public partial class DockingManager
{
    private Window? _hostWindow;
    private IDisposable? _hostWindowLease;
    private bool _hostWindowDiagnosed;
    private nint _hostHandle;
    private IDisposable? _hostIslandLease;
#if WINDOWS
    private Microsoft.UI.Windowing.AppWindow? _hostAppWindow;
#endif
    /// <summary>The top-level window handle of the host island on WinUI, where the
        /// hosting Window object cannot be resolved; zero elsewhere.</summary>
        internal nint HostHandle => _hostHandle;
    /// <summary>The native window hosting this manager, resolved whenever it loads.
        /// It owns the floating windows, which close together with it.</summary>
        internal Window? HostWindow => _hostWindow;

    private void AttachHostWindow()
    {
        var window = ResolveHostWindow();
        // Unchanged host: the same Window, or (without one) an attached island.
        if (window != null ? ReferenceEquals(window, _hostWindow) : _hostWindow == null && _hostHandle != 0)
            return;
        DetachHostWindow();
        if (window == null)
        {
            if (!AttachHostIsland())
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

    /// <summary>WinUI: own floating windows through the island's top-level window
        /// handle and close them when that AppWindow is destroyed.</summary>
        private bool AttachHostIsland()
    {
#if WINDOWS
        try
        {
            if (XamlRoot?.ContentIslandEnvironment?.AppWindowId is not { } id || id.Value == 0)
                return false;
            var handle = Microsoft.UI.Win32Interop.GetWindowFromWindowId(id);
            if (handle == 0)
                return false;
            _hostHandle = handle;
            _hostIslandLease = WindowRegistry.RegisterHost(XamlRoot, handle);
            _hostAppWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(id);
            if (_hostAppWindow != null)
                _hostAppWindow.Destroying += OnHostAppWindowDestroying;
            return true;
        }
        catch (Exception error) when (error is InvalidOperationException or System.Runtime.InteropServices.COMException or ArgumentException)
        {
            return false;
        }
#else
        return false;
#endif
    }

#if WINDOWS
    private void OnHostAppWindowDestroying(Microsoft.UI.Windowing.AppWindow sender, object args)
    {
        // WinUI faults when the system destroys its windows with their owner, and
        // windows cannot be closed inside Destroying. Release ownership now (the
        // owner still exists) and close the floating windows once it is gone.
        if (!ReferenceEquals(sender, _hostAppWindow))
            return;
        foreach (var window in _floating.ToArray())
            window.PrepareOwnerShutdown();
        DispatcherQueue.TryEnqueue(CloseWithHost);
    }

#endif
    private void ReportUnresolvedHostWindow()
    {
        if (_hostWindowDiagnosed || OperatingSystem.IsBrowser() || OperatingSystem.IsAndroid() || OperatingSystem.IsIOS())
            return;
        _hostWindowDiagnosed = true;
        Debug.WriteLine("UnoDock: the Window hosting this DockingManager could not be resolved. Native floating windows will have no owner and will not close with it.");
    }

    private void OnHostWindowClosed(object sender, WindowEventArgs args)
    {
        if (ReferenceEquals(sender, _hostWindow))
            CloseWithHost();
    }

    private void CloseWithHost()
    {
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
#if WINDOWS
        if (_hostAppWindow is { } appWindow)
            appWindow.Destroying -= OnHostAppWindowDestroying;
        _hostAppWindow = null;
#endif
        _hostHandle = 0;
        var island = _hostIslandLease;
        _hostIslandLease = null;
        island?.Dispose();
    }
}
