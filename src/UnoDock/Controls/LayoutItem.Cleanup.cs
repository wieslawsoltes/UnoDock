using Microsoft.UI.Xaml.Data;
using UnoDock.Internal;

namespace UnoDock.Controls;

public abstract partial class LayoutItem
{
    private void RetireOwnedBindings(Dictionary<DependencyProperty, Binding> owned)
    {
        var bindings = owned.ToArray();
        owned.Clear();
        var cleanup = new DockCleanup();
        foreach (var (property, binding) in bindings)
        {
            cleanup.Attempt(() =>
            {
                // ClearValue invokes application code. A callback may replace a
                // later binding; only the captured expression belongs to us.
                if (ReferenceEquals(GetBindingExpression(property)?.ParentBinding, binding))
                {
                    ClearValue(property);
                }
            });
        }

        cleanup.ThrowIfFailed();
    }

    private void RetireOwnedCommands()
    {
        var commands = _commands.ToArray();
        _commands.Clear();
        var cleanup = new DockCleanup();
        foreach (var (property, command) in commands)
        {
            cleanup.Attempt(() =>
            {
                if (ReferenceEquals(GetValue(property), command))
                {
                    ClearValue(property);
                }
            });
        }

        cleanup.ThrowIfFailed();
    }

    private void DisposeItemCore()
    {
        if (_disposed)
        {
            return;
        }

        // Retire authority and retained presentation references before any DP,
        // menu or visual-tree callback can reenter disposal or publication.
        _disposed = true;
        var menu = _defaultMenu;
        var view = _view;
        _defaultMenu = null;
        _view = null;
        _lastFocused = null;
        _manager = null;
        Model = null;
        var cleanup = new DockCleanup();
        cleanup.Attempt(() =>
        {
            if (LayoutElement != null)
            {
                LayoutElement.PropertyChanged -= ModelChanged;
            }
        });
        cleanup.Attempt(() => UnregisterPropertyChangedCallback(VisibilityProperty, _visibilityToken));
        cleanup.Attempt(() => menu?.Dispose());
        cleanup.Attempt(ClearXamlBindings);
        cleanup.Attempt(ClearDefaultBindings);
        cleanup.Attempt(ClearDefaultCommands);
        if (view != null)
        {
            cleanup.Attempt(() => view.GotFocus -= RememberFocus);
            cleanup.Attempt(() => VisualParenting.Detach(view));
            cleanup.Attempt(() => view.Content = null);
            cleanup.Attempt(() => view.ContentTemplate = null);
            cleanup.Attempt(() => view.DataContext = null);
        }

        cleanup.Attempt(() => DataContext = null);
        cleanup.ThrowIfFailed();
    }
}
