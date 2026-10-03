namespace UnoDock.Internal;

internal static class ResourceKeys
{
    /// <summary>Whether the dictionary itself defines the key. Native WinUI's ContainsKey (and
    /// Keys.Contains) also search merged and theme dictionaries; Uno's Keys holds only the
    /// dictionary's own entries. See docs/native-winui.md.</summary>
    internal static bool Owns(this ResourceDictionary dictionary, object key)
    {
#if WINDOWS
        // Enumerating a WinUI dictionary crosses the interop boundary per entry, which is far
        // too slow for application dictionaries. A key is the dictionary's own unless a merged
        // or theme dictionary supplies the value the dictionary resolves.
        if (!dictionary.ContainsKey(key))
            return false;
        var value = dictionary[key];
        foreach (var merged in dictionary.MergedDictionaries)
            if (merged.ContainsKey(key) && Equals(merged[key], value))
                return false;
        foreach (var theme in dictionary.ThemeDictionaries.Values)
            if (theme is ResourceDictionary themed && themed.ContainsKey(key) && Equals(themed[key], value))
                return false;
        return true;
#else
        return dictionary.Keys.Contains(key);
#endif
    }
}
