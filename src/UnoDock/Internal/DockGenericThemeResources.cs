namespace UnoDock.Internal;
/// <summary>State keys of the Generic theme that its historical base palette
/// cannot express. They apply only while <see cref = "Themes.GenericTheme"/> is
/// assigned and only when neither the application nor the theme dictionary
/// supplies the key, so the automatic (null) theme and every override keep
/// their existing presentation. Values come from rendered observations.</summary>
internal static class DockGenericThemeResources
{
    [ThreadStatic]
    private static Dictionary<string, object>? _values;
    internal static object? Find(DockingManager manager, string key) => manager.Theme is Themes.GenericTheme && (_values ??= Create()).TryGetValue(key, out var value) ? value : null;
    private static Dictionary<string, object> Create() => new(StringComparer.Ordinal)
    {
        ["DisabledForegroundBrush"] = DockChrome.Color(0x8d8d8d),
        ["FloatingBorderBrush"] = DockChrome.Color(0xa0a0a0),
        ["ActiveFloatingBorderBrush"] = DockChrome.Color(0x0078d4),
        ["FloatingBorderThickness"] = 3d,
        ["ChromeButtonHoverBorderBrush"] = DockChrome.Color(0xbfdfff),
        ["SelectedTabRaise"] = 2d,
        ["FloatingDocumentMenuButton"] = false
    };
}
