"""One-use integration; validate exact anchors in memory before writing any file."""
from pathlib import Path
import sys

pending = {}

def replace(path, old, new):
    text = pending.get(path, Path(path).read_text(encoding='utf-8'))
    if text.count(old) != 1:
        raise RuntimeError(f'{path}: expected exactly one integration anchor: {old[:90]!r}')
    pending[path] = text.replace(old, new)

phase = sys.argv[1]
if phase == 'register':
    replace('tests/UnoDock.VisualTests/XamlWorkbenchTests.cs',
            '        XamlModelBindingTests.Add(tests);',
            '        XamlModelBindingTests.Add(tests);\n        XamlBindingCleanupTests.Add(tests);')
elif phase == 'fix':
    replace('src/UnoDock/Controls/LayoutItem.Xaml.cs', '''    private void ClearXamlBindings()
    {
        var bindings = _xamlBindings.ToArray();
        _xamlBindings.Clear();
        foreach (var (property, binding) in bindings)
            if (ReferenceEquals(GetBindingExpression(property)?.ParentBinding, binding))
                ClearValue(property);
    }''', '''    private void ClearXamlBindings() => RetireOwnedBindings(_xamlBindings);''')
    replace('src/UnoDock/Controls/LayoutItem.cs', '''    protected virtual void ClearDefaultBindings()
    {
        foreach (var (property, binding) in _bindings)
            if (ReferenceEquals(GetBindingExpression(property)?.ParentBinding, binding))
                ClearValue(property);
        _bindings.Clear();
    }''', '''    protected virtual void ClearDefaultBindings() => RetireOwnedBindings(_bindings);''')
    replace('src/UnoDock/Controls/LayoutItem.cs', '''    protected virtual void ClearDefaultCommands()
    {
        foreach (var (property, command) in _commands)
            if (ReferenceEquals(GetValue(property), command))
                ClearValue(property);
        _commands.Clear();
    }''', '''    protected virtual void ClearDefaultCommands() => RetireOwnedCommands();''')
    replace('src/UnoDock/Controls/LayoutItem.cs', '''    {
        if (_disposed)
            return;
        _disposed = true;
        _defaultMenu?.Dispose();
        _defaultMenu = null;
        if (LayoutElement != null)
            LayoutElement.PropertyChanged -= ModelChanged;
        UnregisterPropertyChangedCallback(VisibilityProperty, _visibilityToken);
        ClearXamlBindings();
        ClearDefaultBindings();
        ClearDefaultCommands();
        if (_view is { } view)
        {
            view.GotFocus -= RememberFocus;
            _lastFocused = null;
            VisualParenting.Detach(view);
            view.Content = null;
            view.ContentTemplate = null;
            view.DataContext = null;
            _view = null;
        }

        _manager = null;
        Model = null;
        DataContext = null;
    }''', '''    {
        DisposeItemCore();
    }''')
else:
    raise RuntimeError('Unknown integration phase: ' + phase)

for path, text in pending.items():
    Path(path).write_text(text, encoding='utf-8')
    print(path)
