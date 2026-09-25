using System.Runtime.CompilerServices;
using System.Windows.Input;
using Windows.Storage;

namespace UnoDock.Gallery;
internal sealed class WorkspaceCommand(Action execute, Func<bool>? canExecute = null) : ICommand
{
    private Action? _execute = execute;
    private Func<bool>? _canExecute = canExecute;
    public bool CanExecute(object? parameter) => _execute != null && (_canExecute?.Invoke() ?? true);
    public void Execute(object? parameter)
    {
        if (CanExecute(parameter))
            _execute?.Invoke();
    }

    public event EventHandler? CanExecuteChanged;
    internal void Refresh() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    internal void Detach()
    {
        _execute = null;
        _canExecute = null;
        Refresh();
        CanExecuteChanged = null;
    }
}
