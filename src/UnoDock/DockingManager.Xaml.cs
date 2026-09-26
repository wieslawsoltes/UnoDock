using UnoDock.Themes;

namespace UnoDock;

public partial class DockingManager
{
    private FluentTheme? _observedTheme;
    private void ObserveXamlTheme(FluentTheme? theme)
    {
        if (ReferenceEquals(theme, _observedTheme))
            return;
        if (_observedTheme != null)
            _observedTheme.Changed -= OnXamlThemeChanged;
        _observedTheme = theme;
        if (theme != null)
            theme.Changed += OnXamlThemeChanged;
    }

    private void OnXamlThemeChanged(object? sender, EventArgs args) => InvalidateView();
}
