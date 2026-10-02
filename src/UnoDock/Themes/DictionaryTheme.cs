namespace UnoDock.Themes;

public abstract class DictionaryTheme : Theme
{
    public DictionaryTheme() : this(new())
    {
    }

    public DictionaryTheme(ResourceDictionary themeResourceDictionary) => ThemeResourceDictionary = themeResourceDictionary ?? throw new ArgumentNullException(nameof(themeResourceDictionary));
    public ResourceDictionary ThemeResourceDictionary
    {
        get;
        private set;
    }

    /// <summary>The dictionary's source URI.</summary>
        /// <exception cref = "InvalidOperationException">The dictionary was created in code and has no source; use <see cref = "GetResourceDictionary"/>.</exception>
        public override Uri GetResourceUri() => ThemeResourceDictionary.Source ?? throw new InvalidOperationException("This theme's dictionary was created in code and has no source URI; use GetResourceDictionary().");
    public override ResourceDictionary GetResourceDictionary() => ThemeResourceDictionary;
}
