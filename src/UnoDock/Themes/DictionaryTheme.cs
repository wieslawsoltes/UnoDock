namespace UnoDock.Themes;

public abstract class DictionaryTheme : Theme
{
    public DictionaryTheme() : this(new())
    {
    }

    public DictionaryTheme(ResourceDictionary themeResourceDictionary) => ThemeResourceDictionary = themeResourceDictionary ?? throw new ArgumentNullException(nameof(themeResourceDictionary));
    public ResourceDictionary ThemeResourceDictionary
    {
        get; private set;
    }

    public override Uri GetResourceUri() => ThemeResourceDictionary.Source!;
    public override ResourceDictionary GetResourceDictionary() => ThemeResourceDictionary;
}
