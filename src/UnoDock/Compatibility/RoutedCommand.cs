namespace UnoDock.Compatibility;
/// <summary>A target-aware ICommand adapter. This preserves explicit CanExecute/Execute target
/// calls; it does not emulate WPF's routed CommandBinding or input-gesture infrastructure.</summary>
public sealed class RoutedCommand : ICommand
{
    private readonly Func<object?, object?, bool> _canExecute;
    private readonly Action<object?, object?> _execute;
    internal RoutedCommand(string name, Type ownerType, Func<object?, object?, bool> canExecute, Action<object?, object?> execute)
    {
        Name = name;
        OwnerType = ownerType;
        _canExecute = canExecute;
        _execute = execute;
    }

    public string Name { get; }
    public Type OwnerType { get; }

    public event EventHandler? CanExecuteChanged;
    public bool CanExecute(object? parameter) => _canExecute(parameter, null);
    public bool CanExecute(object? parameter, object? target) => _canExecute(parameter, target);
    public void Execute(object? parameter)
    {
        if (CanExecute(parameter))
            _execute(parameter, null);
    }

    public void Execute(object? parameter, object? target)
    {
        if (CanExecute(parameter, target))
            _execute(parameter, target);
    }

    internal void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
