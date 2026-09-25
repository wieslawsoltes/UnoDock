using UnoDock.Themes;

namespace UnoDock.Internal;

internal static class DockThemeResources
{
    // Null keeps the historical automatic light/dark palette. FluentTheme() is
    // the explicit Uno semantic theme and follows the owning RequestedTheme.
    internal static bool UsesFluent(DockingManager manager) => manager.Theme is FluentTheme;
    internal static ElementTheme EffectiveTheme(DockingManager manager) => manager.Theme switch
    {
        GenericTheme => ElementTheme.Light,
        FluentTheme { RequestedTheme: not ElementTheme.Default } fluent => fluent.RequestedTheme,
        _ => manager.ActualTheme
    };
    internal static (string Dock, string System, Brush Fallback)[] Slots(DockPalette p) => [("PaneBrush", "LayerFillColorDefaultBrush", p.Surface), ("HeaderBrush", "SolidBackgroundFillColorBaseBrush", p.Header), ("InactiveTabBrush", "ControlFillColorSecondaryBrush", p.Tab), ("BorderBrush", "ControlStrokeColorDefaultBrush", p.Border), ("ForegroundBrush", "TextFillColorPrimaryBrush", p.Foreground), ("HoverBrush", "SubtleFillColorSecondaryBrush", p.Hover), ("PressedBrush", "SubtleFillColorTertiaryBrush", p.Pressed), ("AccentBrush", "AccentFillColorDefaultBrush", p.Accent), ("ActiveTitleBrush", "ControlFillColorInputActiveBrush", p.ActiveTitle)];
    internal static Brush Brush(DockingManager manager, string dockKey, string systemKey, Brush fallback)
    {
        var key = "UnoDock." + dockKey;
        // Preserve the legacy override contract and its constant-time hot path;
        // legacy chrome does not need an application-wide semantic-resource walk.
        if (!UsesFluent(manager))
            return manager.Resources.TryGetValue(key, out var value) && value is Brush brush ? brush : fallback;
        return Find(manager, key) as Brush ?? Find(manager, systemKey) as Brush ?? fallback;
    }

    internal static object? Find(FrameworkElement owner, string key, ResourceDictionary? skip = null)
    {
        var theme = owner is DockingManager manager ? EffectiveTheme(manager) : owner.ActualTheme;
        for (FrameworkElement? current = owner; current != null; current = VisualTreeHelper.GetParent(current) as FrameworkElement)
            if (Find(current.Resources, key, theme, skip, new(ReferenceEqualityComparer.Instance)) is { } local)
                return local;
        if (Application.Current is not { } app)
            return null;
        if (Find(app.Resources, key, theme, skip, new(ReferenceEqualityComparer.Instance)) is { } application)
            return application;
        // Public Uno TryGetValue includes system resources. Use it only after all
        // explicit scopes, and never borrow the opposite application's palette.
        var appTheme = app.RequestedTheme == ApplicationTheme.Dark ? ElementTheme.Dark : ElementTheme.Light;
        return skip == null && theme == appTheme && app.Resources.TryGetValue(key, out var system) ? system : null;
    }

    private static object? Find(ResourceDictionary dictionary, string key, ElementTheme theme, ResourceDictionary? skip, HashSet<ResourceDictionary> visited)
    {
        if (ReferenceEquals(dictionary, skip) || !visited.Add(dictionary))
            return null;
        if (dictionary.Count > 0 && dictionary.Keys.Contains(key))
            return dictionary[key];
        var themes = dictionary.ThemeDictionaries;
        var name = theme == ElementTheme.Dark ? "Dark" : "Light";
        if (themes.Count > 0)
        {
            if (themes.Keys.Contains(name) && themes[name] is ResourceDictionary selected && Find(selected, key, theme, skip, visited) is { } selectedValue)
                return selectedValue;
            if (themes.Keys.Contains("Default") && themes["Default"] is ResourceDictionary defaults && Find(defaults, key, theme, skip, visited) is { } defaultValue)
                return defaultValue;
        }

        for (var i = dictionary.MergedDictionaries.Count - 1; i >= 0; i--)
            if (Find(dictionary.MergedDictionaries[i], key, theme, skip, visited) is { } merged)
                return merged;
        return null;
    }
}
