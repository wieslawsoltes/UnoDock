namespace UnoDock.Internal;

internal static class ResourceKeys
{
    /// <summary>Whether the dictionary itself defines the key. Native WinUI's ContainsKey (and
    /// Keys.Contains) also search merged and theme dictionaries; Uno's Keys holds only the
    /// dictionary's own entries. See docs/native-winui.md.</summary>
    internal static bool Owns(this ResourceDictionary dictionary, object key)
    {
#if WINDOWS
        foreach (var entry in dictionary)
            if (Equals(entry.Key, key))
                return true;
        return false;
#else
        return dictionary.Keys.Contains(key);
#endif
    }
}
