namespace UnoDock.Controls;
/// <summary>
/// Portable Uno/WinUI counterpart of binding-valued AvalonDock container setters.
/// Native Style setters carry definitions; each LayoutItem gets its own Binding.
/// </summary>
public static class LayoutItemBindings
{
    public static readonly DependencyProperty BindingsProperty = DependencyProperty.RegisterAttached("Bindings", typeof(LayoutItemBindingCollection), typeof(LayoutItemBindings), new PropertyMetadata(null, (owner, _) =>
    {
        if (owner is not LayoutItem item)
            throw new ArgumentException("LayoutItemBindings requires a LayoutItem target.");
        item.RefreshXamlBindings();
    }));
    public static LayoutItemBindingCollection? GetBindings(DependencyObject owner) => (LayoutItemBindingCollection?)owner.GetValue(BindingsProperty);
    public static void SetBindings(DependencyObject owner, LayoutItemBindingCollection? value) => owner.SetValue(BindingsProperty, value);
}
