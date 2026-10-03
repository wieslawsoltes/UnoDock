using UnoDock.Controls;

namespace UnoDock.Internal;
/// <summary>Low-priority state resources for retained native templates. Palette
/// publication never replaces application dictionaries or brush instances.</summary>
internal sealed class DockControlStateResources
{
    private readonly FrameworkElement _owner;
    private readonly ResourceDictionary _resources = new();
    private readonly ResourceDictionary _light = new();
    private readonly ResourceDictionary _dark = new();
    private readonly Dictionary<string, object?> _lightValues = new(StringComparer.Ordinal);
    private readonly Dictionary<string, object?> _darkValues = new(StringComparer.Ordinal);
    private ResourceDictionary? _scope;
    private bool _dirty;
    private long _version;
    private bool _refreshing;
    internal DockControlStateResources(FrameworkElement owner)
    {
        _owner = owner;
        _resources.ThemeDictionaries["Light"] = _light;
        _resources.ThemeDictionaries["Dark"] = _dark;
        Attach();
    }

    internal void Set(string key, Brush brush)
    {
        Attach();
        var scope = _owner.Resources;
        // The normal internal-control path has only our fallback dictionary.
        // Avoid recursive walks/visited-set allocations on every tab refresh.
        var onlyFallback = scope.Count == 0 && scope.ThemeDictionaries.Count == 0 && scope.MergedDictionaries.Count == 1 && ReferenceEquals(scope.MergedDictionaries[0], _resources);
        Publish("Light", _light, _lightValues);
        Publish("Dark", _dark, _darkValues);
        void Publish(string theme, ResourceDictionary dictionary, Dictionary<string, object?> resolved)
        {
            object? application = null;
            // A same-scope theme entry is an application override too. A merged
            // fallback would otherwise outrank it in the native resource lookup.
            var overridden = !onlyFallback && TryFindApplicationValue(scope, theme, key, new(ReferenceEqualityComparer.Instance), out application);
            var value = overridden ? application : brush;
            if (!resolved.TryGetValue(key, out var previous) || !ReferenceEquals(previous, value))
            {
                resolved[key] = value;
                _dirty = true;
                _version++;
            }

            if (overridden)
            {
                if (dictionary.Owns(key))
                    dictionary.Remove(key);
            }
            else if (!(dictionary.TryGetValue(key, out var current) && ReferenceEquals(current, brush)))
                dictionary[key] = brush;
        }
    }

    private bool TryFindApplicationValue(ResourceDictionary dictionary, string theme, string key, HashSet<ResourceDictionary> visited, out object? value)
    {
        value = null;
        if (ReferenceEquals(dictionary, _resources) || !visited.Add(dictionary))
            return false;
        if (dictionary.Owns(key))
        {
            value = dictionary[key];
            return true;
        }

        for (var i = dictionary.MergedDictionaries.Count - 1; i >= 0; i--)
            if (TryFindApplicationValue(dictionary.MergedDictionaries[i], theme, key, visited, out value))
                return true;
        var themes = dictionary.ThemeDictionaries;
        var selected = themes.Keys.Contains(theme) ? themes[theme] : themes.Keys.Contains("Default") ? themes["Default"] : null;
        return selected is ResourceDictionary values && TryFindApplicationValue(values, theme, key, visited, out value);
    }

    private void Attach()
    {
        var resources = _owner.Resources;
        if (!ReferenceEquals(_scope, resources))
        {
            Detach();
            _scope = resources;
        }

        if (!resources.MergedDictionaries.Contains(_resources))
        {
            resources.MergedDictionaries.Insert(0, _resources);
            _dirty = true;
            _version++;
        }
    }

    internal void Detach()
    {
        _scope?.MergedDictionaries.Remove(_resources);
        _scope = null;
        _version++;
    }

    internal void Refresh(Control control)
    {
        if (!_dirty || _refreshing || !control.IsLoaded)
            return;
        _dirty = false;
#if WINDOWS
        // Native WinUI resolves ThemeResource references, state setters included, when the
        // template loads and when the element's theme changes; replacing a dictionary entry or
        // entering a state again does not. Re-applying the theme resolves them again.
        var requested = control.RequestedTheme;
        control.RequestedTheme = control.ActualTheme == ElementTheme.Dark ? ElementTheme.Light : ElementTheme.Dark;
        control.RequestedTheme = requested;
#endif
        // ThemeResource on a native state setter is resolved when that state is
        // entered. Dictionary replacement alone does not refresh an already-hot
        // state. Replay its native CommonStates transition, not pointer/keyboard
        // events, and retain the same template and focus-state group.
        var group = control.FindVisualChildren<FrameworkElement>().SelectMany(VisualStateManager.GetVisualStateGroups).FirstOrDefault(candidate => candidate.Name == "CommonStates");
        if (group?.CurrentState is not { Name: "PointerOver" or "Pressed" or "Disabled" } state)
            return;
        var template = control.Template;
        var version = _version;
        var interrupted = false;
        void Changed(object? sender, VisualStateChangedEventArgs args)
        {
            if (!ReferenceEquals(args.OldState, state) || args.NewState?.Name != "Normal")
                interrupted = true;
        }

        _refreshing = true;
        group.CurrentStateChanged += Changed;
        try
        {
            if (!VisualStateManager.GoToState(control, "Normal", false))
                return;
            group.CurrentStateChanged -= Changed;
            if (!interrupted && version == _version && ReferenceEquals(control.Template, template) && control.IsLoaded && group.CurrentState?.Name == "Normal")
                VisualStateManager.GoToState(control, state.Name, false);
        }
        catch
        {
            _dirty = true;
            throw;
        }
        finally
        {
            group.CurrentStateChanged -= Changed;
            _refreshing = false;
        }
    }
}
