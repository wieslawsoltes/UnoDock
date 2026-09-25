using Microsoft.UI.Xaml.Input;

namespace UnoDock.Internal;
internal sealed class ActionDisposable(Action action) : IDisposable
{
    private Action? _action = action;
    public void Dispose() => Interlocked.Exchange(ref _action, null)?.Invoke();
}
