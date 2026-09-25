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
/// <summary>Uses the owning application's Uno/WinUI semantic brushes. Explicit
/// UnoDock resources still override individual slots. Geometry remains compact.</summary>
public sealed class FluentTheme : DictionaryTheme
{
    public FluentTheme() : this(ElementTheme.Default) { }
    public FluentTheme(ElementTheme theme)
    {
        if (!Enum.IsDefined(theme)) throw new ArgumentOutOfRangeException(nameof(theme));
        RequestedTheme = theme;
    }
    internal ElementTheme RequestedTheme { get; }
}
