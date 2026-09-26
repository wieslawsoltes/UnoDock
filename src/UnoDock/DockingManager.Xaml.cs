using UnoDock.Themes;

namespace UnoDock;

public partial class DockingManager
{
    private Theme? _observedTheme;
    private Microsoft.Windows.Shell.SystemParameters2? _themeParameters;
    private void ObserveXamlTheme(Theme? theme)
    {
        if (ReferenceEquals(theme, _observedTheme))
            return;
        if (_observedTheme != null)
            _observedTheme.Changed -= OnXamlThemeChanged;
        _observedTheme = theme;
        if (theme != null)
            theme.Changed += OnXamlThemeChanged;
    }

    private void OnXamlThemeChanged(object? sender, EventArgs args)
    {
        if (_disposed || !ReferenceEquals(sender, Theme))
            return;
        UpdateThemeDictionary();
        InvalidateView();
    }

    private void UpdateThemeDictionary()
    {
        var dictionary = Theme?.GetResourceDictionary();
        if (ReferenceEquals(_themeResources, dictionary))
            return;
        if (_themeResources != null)
            Resources.MergedDictionaries.Remove(_themeResources);
        _themeResources = dictionary;
        if (dictionary != null)
            Resources.MergedDictionaries.Add(dictionary);
    }

    private void ObserveThemeParameters()
    {
        if (_themeParameters != null)
            return;
        _themeParameters = Microsoft.Windows.Shell.SystemParameters2.Current;
        _themeParameters.PropertyChanged += OnThemeParametersChanged;
    }

    private void ReleaseThemeParameters()
    {
        if (_themeParameters == null)
            return;
        _themeParameters.PropertyChanged -= OnThemeParametersChanged;
        _themeParameters = null;
    }

    private void OnThemeParametersChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (!_disposed && _loaded && args.PropertyName is "HighContrast" or "WindowGlassColor" or "UxThemeName")
            InvalidateView();
    }
}
