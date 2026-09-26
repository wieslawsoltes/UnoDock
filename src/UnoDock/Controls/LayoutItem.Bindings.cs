using Microsoft.UI.Xaml.Data;

namespace UnoDock.Controls;

public abstract partial class LayoutItem
{
    private readonly Dictionary<DependencyProperty, Binding> _styleBindings = [];
    internal void RefreshBindingDefinitions()
    {
        if (!_attaching && !_disposed && LayoutElement != null)
            ApplyContainerStyle(Style);
    }

    private void ApplyBindingDefinitions()
    {
        foreach (var (target, definition) in LayoutItemBindings.Definitions(this))
        {
            // A consumer's explicit local value or binding always wins. The
            // adapter clears only the default/style bindings it actually owns.
            if (definition == null || ReadLocalValue(target) != DependencyProperty.UnsetValue)
                continue;
            var binding = definition.CreateBinding();
            _styleBindings.Add(target, binding);
            SetBinding(target, binding);
        }
    }

    private void ClearStyleBindings()
    {
        foreach (var (property, binding) in _styleBindings)
            if (ReferenceEquals(GetBindingExpression(property)?.ParentBinding, binding))
                ClearValue(property);
        _styleBindings.Clear();
    }
}
