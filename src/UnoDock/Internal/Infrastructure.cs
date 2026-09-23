using Microsoft.UI.Xaml.Input;

namespace UnoDock.Internal;

internal sealed class ActionDisposable(Action action) : IDisposable
{
    private Action? _action = action;
    public void Dispose() => Interlocked.Exchange(ref _action, null)?.Invoke();
}
internal sealed class SourceObserver : IDisposable
{
    private readonly WeakReference<DockingManager> _manager;
    private INotifyCollectionChanged? _source;
    internal SourceObserver(DockingManager manager, IEnumerable source, bool documents)
    {
        _manager = new(manager); _source = source as INotifyCollectionChanged;
        if (_source != null) _source.CollectionChanged += Changed;
    }
    private void Changed(object? sender, NotifyCollectionChangedEventArgs args)
    { if (_source is { } source && _manager.TryGetTarget(out var manager)) manager.ReceiveSourceEvent(this, source, args); else Dispose(); }
    public void Dispose() { if (Interlocked.Exchange(ref _source, null) is { } source) source.CollectionChanged -= Changed; }
}
public sealed class DelegateCommand(Action<object?> execute, Predicate<object?>? canExecute = null) : ICommand
{
    public DelegateCommand(Action execute, Func<bool>? canExecute = null) : this(_ => execute(), canExecute == null ? null : _ => canExecute()) { }
    public event EventHandler? CanExecuteChanged;
    public bool CanExecute(object? parameter) => canExecute?.Invoke(parameter) ?? true;
    public void Execute(object? parameter) { if (CanExecute(parameter)) execute(parameter); }
    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
internal static class InputState
{
    public static bool ControlDown => Down(Windows.System.VirtualKey.Control);
    public static bool ShiftDown => Down(Windows.System.VirtualKey.Shift);
    private static bool Down(Windows.System.VirtualKey key) => (Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(key) & Windows.UI.Core.CoreVirtualKeyStates.Down) != 0;
}
internal static class VisualParenting
{
    internal static void Detach(UIElement element)
    {
        var parent = VisualTreeHelper.GetParent(element);
        switch (parent)
        {
            case Panel panel: panel.Children.Remove(element); break;
            case Border border when ReferenceEquals(border.Child, element): border.Child = null; break;
            case ContentPresenter presenter when ReferenceEquals(presenter.Content, element): presenter.Content = null; break;
            case ContentControl control when ReferenceEquals(control.Content, element): control.Content = null; break;
        }
    }
    internal static void ReconcilePanel(Panel panel, IReadOnlyList<UIElement> wanted)
    {
        var keep = new HashSet<UIElement>(wanted, ReferenceEqualityComparer.Instance);
        for (var i = panel.Children.Count - 1; i >= 0; i--) if (!keep.Contains(panel.Children[i])) panel.Children.RemoveAt(i);
        for (var i = 0; i < wanted.Count; i++)
        {
            if (i < panel.Children.Count && ReferenceEquals(panel.Children[i], wanted[i])) continue;
            Detach(wanted[i]); panel.Children.Insert(i, wanted[i]);
        }
    }
}
