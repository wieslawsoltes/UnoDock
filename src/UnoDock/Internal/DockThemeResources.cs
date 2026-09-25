using UnoDock.Themes;

namespace UnoDock.Internal;

internal static class DockThemeResources
{
    internal static bool UsesFluent(DockingManager manager) => manager.Theme is null or FluentTheme;
    internal static ElementTheme EffectiveTheme(DockingManager manager) => manager.Theme switch
    {
        GenericTheme => ElementTheme.Light,
        FluentTheme { RequestedTheme: not ElementTheme.Default } fluent => fluent.RequestedTheme,
        _ => manager.ActualTheme
    };
    internal static (string Dock, string System, Brush Fallback)[] Slots(DockPalette p) =>
    [
        ("PaneBrush", "LayerFillColorDefaultBrush", p.Surface),
        ("HeaderBrush", "SolidBackgroundFillColorBaseBrush", p.Header),
        ("InactiveTabBrush", "ControlFillColorSecondaryBrush", p.Tab),
        ("BorderBrush", "ControlStrokeColorDefaultBrush", p.Border),
        ("ForegroundBrush", "TextFillColorPrimaryBrush", p.Foreground),
        ("HoverBrush", "SubtleFillColorSecondaryBrush", p.Hover),
        ("PressedBrush", "SubtleFillColorTertiaryBrush", p.Pressed),
        ("AccentBrush", "AccentFillColorDefaultBrush", p.Accent),
        ("ActiveTitleBrush", "ControlFillColorInputActiveBrush", p.ActiveTitle)
    ];
    internal static Brush Brush(DockingManager manager, string dockKey, string systemKey, Brush fallback) =>
        Find(manager, "UnoDock." + dockKey) as Brush ??
        (UsesFluent(manager) ? Find(manager, systemKey) as Brush : null) ?? fallback;
    internal static object? Find(FrameworkElement owner, string key, ResourceDictionary? skip = null)
    {
        var theme = owner is DockingManager manager ? EffectiveTheme(manager) : owner.ActualTheme;
        for (FrameworkElement? current = owner; current != null; current = VisualTreeHelper.GetParent(current) as FrameworkElement)
            if (Find(current.Resources, key, theme, skip, new(ReferenceEqualityComparer.Instance)) is { } local) return local;
        if (Application.Current is not { } app) return null;
        if (Find(app.Resources, key, theme, skip, new(ReferenceEqualityComparer.Instance)) is { } application) return application;
        // Uno's public TryGetValue also searches system resources. Do that only
        // AFTER every explicit owner/ancestor/application scope, never inside
        // the recursive local search where it would hide a nearer customization.
        // An explicit opposite theme must not borrow the application's palette.
        var appTheme = app.RequestedTheme == ApplicationTheme.Dark ? ElementTheme.Dark : ElementTheme.Light;
        return skip == null && theme == appTheme && app.Resources.TryGetValue(key, out var system) ? system : null;
    }
    private static object? Find(ResourceDictionary dictionary, string key, ElementTheme theme, ResourceDictionary? skip, HashSet<ResourceDictionary> visited)
    {
        if (ReferenceEquals(dictionary, skip) || !visited.Add(dictionary)) return null;
        if (dictionary.Keys.Contains(key)) return dictionary[key];
        var name = theme == ElementTheme.Dark ? "Dark" : "Light";
        if (dictionary.ThemeDictionaries.Keys.Contains(name) && dictionary.ThemeDictionaries[name] is ResourceDictionary selected &&
            Find(selected, key, theme, skip, visited) is { } selectedValue) return selectedValue;
        if (dictionary.ThemeDictionaries.Keys.Contains("Default") && dictionary.ThemeDictionaries["Default"] is ResourceDictionary defaults &&
            Find(defaults, key, theme, skip, visited) is { } defaultValue) return defaultValue;
        for (var i = dictionary.MergedDictionaries.Count - 1; i >= 0; i--)
            if (Find(dictionary.MergedDictionaries[i], key, theme, skip, visited) is { } merged) return merged;
        return null;
    }
}
