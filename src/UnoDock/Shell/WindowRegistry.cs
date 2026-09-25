using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Controls.Primitives;
using UnoDock;
using UnoDock.Controls;
using UnoDock.Compatibility;

namespace Microsoft.Windows.Shell;

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
