namespace UnoDock.Themes;

public abstract partial class Theme : DependencyObject
{
    public Theme() { }
    public abstract Uri GetResourceUri();
    public virtual ResourceDictionary GetResourceDictionary() => new() { Source = GetResourceUri() };
}
public abstract class DictionaryTheme : Theme
{
    public DictionaryTheme() : this(new()) { }
    public DictionaryTheme(ResourceDictionary themeResourceDictionary) => ThemeResourceDictionary = themeResourceDictionary ?? throw new ArgumentNullException(nameof(themeResourceDictionary));
    public ResourceDictionary ThemeResourceDictionary { get; private set; }
    public override Uri GetResourceUri() => ThemeResourceDictionary.Source!;
    public override ResourceDictionary GetResourceDictionary() => ThemeResourceDictionary;
}
public class GenericTheme : Theme
{
    public override Uri GetResourceUri() => new("ms-appx:///UnoDock/Themes/Generic.xaml");
}
/// <summary>Uno/WinUI semantic colors with compact docking geometry. The default
/// constructor follows the owner; an explicit Light/Dark theme remains coherent
/// even when the containing window requests the other theme.</summary>
public sealed class FluentTheme : DictionaryTheme
{
    private readonly Dictionary<string, Brush> _published = new(StringComparer.Ordinal);
    public FluentTheme() : this(ElementTheme.Default) { }
    public FluentTheme(ElementTheme theme)
    {
        if (!Enum.IsDefined(theme)) throw new ArgumentOutOfRangeException(nameof(theme));
        RequestedTheme = theme;
        // Preserve the explicit-theme dictionary contract. These aliases are
        // refreshed from application resources on use, never cloned/recolored.
        if (theme != ElementTheme.Default)
            foreach (var slot in Internal.DockThemeResources.Slots(Internal.DockChrome.Default(theme == ElementTheme.Dark)))
                Publish(slot.Dock, slot.Fallback);
    }
    internal ElementTheme RequestedTheme { get; }
    internal void UpdateResources(DockingManager manager)
    {
        if (RequestedTheme == ElementTheme.Default) return;
        foreach (var slot in Internal.DockThemeResources.Slots(Internal.DockChrome.Default(RequestedTheme == ElementTheme.Dark)))
        {
            var key = "UnoDock." + slot.Dock;
            if (ThemeResourceDictionary.TryGetValue(key, out var current) &&
                (!_published.TryGetValue(key, out var previous) || !ReferenceEquals(current, previous))) continue;
            var brush = Internal.DockThemeResources.Find(manager, key, ThemeResourceDictionary) as Brush ??
                Internal.DockThemeResources.Find(manager, slot.System) as Brush ?? slot.Fallback;
            Publish(slot.Dock, brush);
        }
    }
    private void Publish(string name, Brush brush)
    {
        var key = "UnoDock." + name;
        if (!_published.TryGetValue(key, out var previous) || !ReferenceEquals(previous, brush) ||
            !ThemeResourceDictionary.TryGetValue(key, out var value) || !ReferenceEquals(value, brush)) ThemeResourceDictionary[key] = brush;
        _published[key] = brush;
    }
}
