using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Controls.Primitives;
using UnoDock;
using UnoDock.Controls;
using UnoDock.Compatibility;

namespace Microsoft.Windows.Shell;

/// <summary>System commands for native Uno/WinUI windows and compositional floating windows.
/// WinUI command bindings use a Window or FrameworkElement as CommandParameter; explicit
/// two-argument command execution accepts a target, without relying on Window.Current.</summary>
public static class SystemCommands
{
    public static RoutedCommand CloseWindowCommand { get; } = Create(nameof(CloseWindow), WindowAction.Close);
    public static RoutedCommand MaximizeWindowCommand { get; } = Create(nameof(MaximizeWindow), WindowAction.Maximize);
    public static RoutedCommand MinimizeWindowCommand { get; } = Create(nameof(MinimizeWindow), WindowAction.Minimize);
    public static RoutedCommand RestoreWindowCommand { get; } = Create(nameof(RestoreWindow), WindowAction.Restore);
    public static RoutedCommand ShowSystemMenuCommand { get; } = Create(nameof(ShowSystemMenu), WindowAction.Menu);

    public static void CloseWindow(DockWindowControl window) => Execute(window, WindowAction.Close);
    public static void MaximizeWindow(DockWindowControl window) => Execute(window, WindowAction.Maximize);
    public static void MinimizeWindow(DockWindowControl window) => Execute(window, WindowAction.Minimize);
    public static void RestoreWindow(DockWindowControl window) => Execute(window, WindowAction.Restore);
    public static void ShowSystemMenu(DockWindowControl window, Point screenLocation) => ShowMenu(window, screenLocation);
    public static void CloseWindow(ContentControl window) => Execute(window, WindowAction.Close);
    public static void MaximizeWindow(ContentControl window) => Execute(window, WindowAction.Maximize);
    public static void MinimizeWindow(ContentControl window) => Execute(window, WindowAction.Minimize);
    public static void RestoreWindow(ContentControl window) => Execute(window, WindowAction.Restore);
    public static void ShowSystemMenu(ContentControl window, Point screenLocation) => ShowMenu(window, screenLocation);
    public static void CloseWindow(Window window) => Execute(window, WindowAction.Close);
    public static void MaximizeWindow(Window window) => Execute(window, WindowAction.Maximize);
    public static void MinimizeWindow(Window window) => Execute(window, WindowAction.Minimize);
    public static void RestoreWindow(Window window) => Execute(window, WindowAction.Restore);
    public static void ShowSystemMenu(Window window, Point screenLocation) => ShowMenu(window, screenLocation);

    /// <summary>Register a native WinUI window so commands targeting its descendants can resolve
    /// the owner. Uno Skia can also discover windows through its public ApplicationHelper.</summary>
    public static IDisposable RegisterWindow(Window window) => WindowRegistry.Register(window);

    /// <summary>Builds a live managed menu for a native or in-surface window. It is also the
    /// non-Windows replacement for an OS system menu. Parameter ownership is explicit.</summary>
    public static MenuFlyout CreateSystemMenu(object window)
    {
        Validate(window);
        var menu = new MenuFlyout();
        Add("Restore", RestoreWindowCommand); Add("Minimize", MinimizeWindowCommand); Add("Maximize", MaximizeWindowCommand);
        menu.Items.Add(new MenuFlyoutSeparator()); Add("Close", CloseWindowCommand);
        return menu;
        void Add(string text, RoutedCommand command) => menu.Items.Add(new MenuFlyoutItem { Text = text, Command = command, CommandParameter = window });
    }
    internal static void InvalidateCommands()
    {
        foreach (var command in new[] { CloseWindowCommand, MaximizeWindowCommand, MinimizeWindowCommand, RestoreWindowCommand, ShowSystemMenuCommand })
            command.RaiseCanExecuteChanged();
    }
    private static RoutedCommand Create(string name, WindowAction action) => new(name, typeof(SystemCommands),
        (parameter, target) => CanExecute(target ?? parameter, action),
        (parameter, target) =>
        {
            var owner = target ?? parameter;
            if (action == WindowAction.Menu)
            {
                var anchor = owner is Window native ? native.Content as FrameworkElement : owner as FrameworkElement;
                if (anchor == null || !anchor.IsLoaded) return;
                if (parameter is Point point && target != null) ShowMenu(owner!, point);
                else CreateSystemMenu(owner!).ShowAt(anchor, new FlyoutShowOptions { Position = new Point(0, 0) });
            }
            else if (owner != null) Execute(owner, action);
        });
    private static bool CanExecute(object? target, WindowAction action)
    {
        if (target == null) return false;
        if (target is FrameworkElement element && !element.DispatcherQueue.HasThreadAccess) return false;
        if (target is LayoutFloatingWindowControl floating) return floating.CanPerformSystemAction(action);
        var window = target as Window ?? (target is FrameworkElement visual ? WindowRegistry.Find(visual) : null);
        if (window == null || !window.DispatcherQueue.HasThreadAccess || WindowRegistry.Observe(window)) return false;
        if (action == WindowAction.Close) return true;
        if (action == WindowAction.Menu) return window.Content is FrameworkElement { IsLoaded: true };
        if (window.AppWindow.Presenter is not OverlappedPresenter presenter) return false;
        return action switch
        {
            WindowAction.Maximize => presenter.IsMaximizable && presenter.State != OverlappedPresenterState.Maximized,
            WindowAction.Minimize => presenter.IsMinimizable && presenter.State != OverlappedPresenterState.Minimized,
            WindowAction.Restore => presenter.State != OverlappedPresenterState.Restored,
            _ => false
        };
    }
    private static void Execute(object window, WindowAction action)
    {
        Validate(window);
        if (!CanExecute(window, action)) return;
        if (window is LayoutFloatingWindowControl floating) floating.PerformSystemAction(action);
        else
        {
            var native = window as Window ?? WindowRegistry.Find((FrameworkElement)window)!;
            if (action == WindowAction.Close) native.Close();
            else if (native.AppWindow.Presenter is OverlappedPresenter presenter)
            {
                if (action == WindowAction.Maximize) presenter.Maximize();
                else if (action == WindowAction.Minimize) presenter.Minimize();
                else if (action == WindowAction.Restore) presenter.Restore();
            }
        }
        InvalidateCommands();
    }
    private static void ShowMenu(object window, Point screenLocation)
    {
        Validate(window);
        if (!double.IsFinite(screenLocation.X) || !double.IsFinite(screenLocation.Y)) throw new ArgumentOutOfRangeException(nameof(screenLocation));
        if (!CanExecute(window, WindowAction.Menu)) return;
        var native = window as Window ?? (window as LayoutFloatingWindowControl)?.NativeWindow;
        // Native Windows receives device pixels at the documented Win32 boundary.
        if (native != null && NativeChrome.TryShowSystemMenu(native, screenLocation)) return;
        var anchor = window is Window w ? w.Content as FrameworkElement : (FrameworkElement)window;
        if (anchor == null) return;
        using var coordinates = new DesktopWindowCoordinates();
        var position = coordinates.FromScreen(screenLocation, anchor);
        CreateSystemMenu(window).ShowAt(anchor, new FlyoutShowOptions { Position = position });
    }
    private static void Validate(object window)
    {
        ArgumentNullException.ThrowIfNull(window);
        if (window is not Window && window is not FrameworkElement) throw new ArgumentException("A command target must be a Window or FrameworkElement.", nameof(window));
        var dispatcher = window is Window w ? w.DispatcherQueue : ((FrameworkElement)window).DispatcherQueue;
        if (!dispatcher.HasThreadAccess) throw new InvalidOperationException("Window commands require the owning UI thread.");
    }
}
internal enum WindowAction { Close, Maximize, Minimize, Restore, Menu }

internal static class WindowRegistry
{
    private static readonly ConditionalWeakTable<Window, State> States = new();
    // Weak entries permit lookup on WinUI, which has no window enumeration API.
    private static readonly List<WeakReference<Window>> Windows = new();
    internal static IDisposable Register(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);
        if (!window.DispatcherQueue.HasThreadAccess) throw new InvalidOperationException("Register windows on their UI thread.");
        var state = States.GetValue(window, w => new State(w));
        if (state.Closed) throw new ObjectDisposedException(nameof(window));
        if (state.Leases++ == 0) lock (Windows) Windows.Add(new(window));
        return new Lease(state);
    }
    // Keep a weak-key closed-state tombstone even after the last lookup lease is
    // released. Otherwise a disposed registration could re-enable commands on a
    // destroyed host. The event cycle has no static strong root to the window.
    internal static bool Observe(Window window) => States.GetValue(window, w => new State(w)).Closed;
    internal static bool IsClosed(Window window) => States.TryGetValue(window, out var state) && state.Closed;
    internal static Window? Find(FrameworkElement element)
    {
        if (!element.DispatcherQueue.HasThreadAccess || element.XamlRoot == null) return null;
        lock (Windows)
        {
            for (var i = Windows.Count - 1; i >= 0; i--)
            {
                if (!Windows[i].TryGetTarget(out var window)) { Windows.RemoveAt(i); continue; }
                if (window.DispatcherQueue.HasThreadAccess && !IsClosed(window) && ReferenceEquals(window.Content?.XamlRoot, element.XamlRoot)) return window;
            }
        }
#if !WINDOWS
        return Uno.UI.ApplicationHelper.Windows.FirstOrDefault(w => ReferenceEquals(w.Content?.XamlRoot, element.XamlRoot));
#else
        return null;
#endif
    }
    internal static Window[] Snapshot()
    {
        var result = new HashSet<Window>(ReferenceEqualityComparer.Instance);
        lock (Windows)
            for (var i = Windows.Count - 1; i >= 0; i--)
            {
                if (!Windows[i].TryGetTarget(out var window)) { Windows.RemoveAt(i); continue; }
                if (window.DispatcherQueue.HasThreadAccess && !IsClosed(window)) result.Add(window);
            }
#if !WINDOWS
        foreach (var window in Uno.UI.ApplicationHelper.Windows.ToArray())
            if (window.DispatcherQueue.HasThreadAccess && !IsClosed(window)) result.Add(window);
#endif
        return result.ToArray();
    }
    private sealed class State
    {
        private readonly Window _window;
        internal int Leases;
        internal bool Closed;
        internal State(Window window)
        {
            _window = window;
            window.Closed += OnClosed; window.AppWindow.Changed += OnChanged;
        }
        private void OnChanged(AppWindow sender, AppWindowChangedEventArgs args) => SystemCommands.InvalidateCommands();
        private void OnClosed(object sender, WindowEventArgs args)
        { Closed = true; Detach(); SystemCommands.InvalidateCommands(); }
        internal void Release()
        {
            if (!_window.DispatcherQueue.HasThreadAccess) throw new InvalidOperationException("Dispose window registration on its UI thread.");
            if (--Leases == 0)
                lock (Windows) Windows.RemoveAll(w => !w.TryGetTarget(out var target) || ReferenceEquals(target, _window));
        }
        private void Detach()
        {
            _window.Closed -= OnClosed; _window.AppWindow.Changed -= OnChanged;
            lock (Windows) Windows.RemoveAll(w => !w.TryGetTarget(out var target) || ReferenceEquals(target, _window));
        }
    }
    private sealed class Lease(State state) : IDisposable
    {
        private State? _state = state;
        public void Dispose() { if (_state is { } value) { value.Release(); _state = null; } }
    }
}
