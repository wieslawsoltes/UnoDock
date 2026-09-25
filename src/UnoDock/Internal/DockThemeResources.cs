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
        return Application.Current is { } app ? Find(app.Resources, key, theme, skip, new(ReferenceEqualityComparer.Instance)) : null;
    }
    private static object? Find(ResourceDictionary dictionary, string key, ElementTheme theme, ResourceDictionary? skip, HashSet<ResourceDictionary> visited)
    {
        if (ReferenceEquals(dictionary, skip) || !visited.Add(dictionary)) return null;
        if (dictionary.Keys.Contains(key)) return dictionary[key];
        var name = theme == ElementTheme.Dark ? "Dark" : "Light";
        if (dictionary.ThemeDictionaries.TryGetValue(name, out var themed) && themed is ResourceDictionary selected &&
            Find(selected, key, theme, skip, visited) is { } selectedValue) return selectedValue;
        if (dictionary.ThemeDictionaries.TryGetValue("Default", out var defaultTheme) && defaultTheme is ResourceDictionary defaults &&
            Find(defaults, key, theme, skip, visited) is { } defaultValue) return defaultValue;
        for (var i = dictionary.MergedDictionaries.Count - 1; i >= 0; i--)
            if (Find(dictionary.MergedDictionaries[i], key, theme, skip, visited) is { } merged) return merged;
        // A recursive lookup must not rediscover an explicitly excluded alias
        // dictionary through the parent's framework TryGetValue fallback.
        return skip == null && dictionary.TryGetValue(key, out var value) ? value : null;
    }
}
