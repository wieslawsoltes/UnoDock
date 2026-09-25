using Microsoft.UI.Xaml.Input;

namespace UnoDock.Internal;
public sealed class DelegateCommand(Action<object?> execute, Predicate<object?>? canExecute = null) : ICommand
{
    public DelegateCommand(Action execute, Func<bool>? canExecute = null) : this(_ => execute(), canExecute == null ? null : _ => canExecute())
    {
    }

    public event EventHandler? CanExecuteChanged;
    public bool CanExecute(object? parameter) => canExecute?.Invoke(parameter) ?? true;
    public void Execute(object? parameter)
    {
        if (CanExecute(parameter))
            execute(parameter);
    }

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
