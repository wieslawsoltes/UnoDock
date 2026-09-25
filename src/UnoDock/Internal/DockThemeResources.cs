using UnoDock.Themes;

namespace UnoDock.Internal;

/// <summary>Resolve the owning manager's theme across XamlRoots. Application
/// brushes remain application-owned; never cache them across theme replacement.</summary>
internal static class DockThemeResources
{
    internal static bool UsesFluent(DockingManager manager) => manager.Theme is null or FluentTheme;
    internal static ElementTheme EffectiveTheme(DockingManager manager) => manager.Theme switch
    {
        GenericTheme => ElementTheme.Light,
        FluentTheme { RequestedTheme: not ElementTheme.Default } fluent => fluent.RequestedTheme,
        _ => manager.ActualTheme
    };

    internal static Brush Brush(DockingManager manager, string dockKey, string systemKey, Brush fallback)
        => Find(manager, "UnoDock." + dockKey) as Brush ??
           (UsesFluent(manager) ? Find(manager, systemKey) as Brush : null) ?? fallback;

    internal static object? Find(FrameworkElement owner, string key)
    {
        var theme = owner is DockingManager manager ? EffectiveTheme(manager) : owner.ActualTheme;
        for (FrameworkElement? current = owner; current != null; current = VisualTreeHelper.GetParent(current) as FrameworkElement)
            if (Find(current.Resources, key, theme, new(ReferenceEqualityComparer.Instance)) is { } local) return local;
        return Application.Current is { } app
            ? Find(app.Resources, key, theme, new(ReferenceEqualityComparer.Instance)) : null;
    }

    private static object? Find(ResourceDictionary dictionary, string key, ElementTheme theme, HashSet<ResourceDictionary> visited)
    {
        if (!visited.Add(dictionary)) return null;
        // Keys does not materialize every lazy resource value, unlike enumerating
        // the entire dictionary. Local overrides precede theme/merged entries.
        if (dictionary.Keys.Contains(key)) return dictionary[key];
        var name = theme == ElementTheme.Dark ? "Dark" : "Light";
        if (dictionary.ThemeDictionaries.TryGetValue(name, out var themed) && themed is ResourceDictionary selected &&
            Find(selected, key, theme, visited) is { } selectedValue) return selectedValue;
        if (dictionary.ThemeDictionaries.TryGetValue("Default", out var defaultTheme) && defaultTheme is ResourceDictionary defaults &&
            Find(defaults, key, theme, visited) is { } defaultValue) return defaultValue;
        for (var i = dictionary.MergedDictionaries.Count - 1; i >= 0; i--)
            if (Find(dictionary.MergedDictionaries[i], key, theme, visited) is { } merged) return merged;
        return dictionary.TryGetValue(key, out var value) ? value : null;
    }
}
