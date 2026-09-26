namespace UnoDock.Internal;
/// <summary>Feeds palette brushes to native template states without copying their
/// ControlTemplates or replacing application-owned brush objects. Normal theme
/// dictionaries do not override the platform's HighContrast state resources.</summary>
internal sealed class DockControlStateResources
{
    private readonly ResourceDictionary _brushes = new();
    internal DockControlStateResources(FrameworkElement owner)
    {
        owner.Resources.ThemeDictionaries["Light"] = _brushes;
        owner.Resources.ThemeDictionaries["Dark"] = _brushes;
    }

    internal void Set(string key, Brush brush)
    {
        if (!_brushes.TryGetValue(key, out var current) || !ReferenceEquals(current, brush))
            _brushes[key] = brush;
    }
}
