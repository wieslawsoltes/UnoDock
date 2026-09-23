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
/// <summary>Independent palette, not a copy of the reference toolkit's theme assets.</summary>
public sealed class FluentTheme : DictionaryTheme
{
    public FluentTheme() { }
    public FluentTheme(ElementTheme theme)
    {
        var dark = theme == ElementTheme.Dark;
        var palette = Internal.DockChrome.Default(dark);
        ThemeResourceDictionary["UnoDock.InactiveTabBrush"] = palette.Tab;
        ThemeResourceDictionary["UnoDock.BorderBrush"] = palette.Border;
        ThemeResourceDictionary["UnoDock.ForegroundBrush"] = palette.Foreground;
        ThemeResourceDictionary["UnoDock.HoverBrush"] = palette.Hover;
        ThemeResourceDictionary["UnoDock.PressedBrush"] = palette.Pressed;
        ThemeResourceDictionary["UnoDock.AccentBrush"] = palette.Accent;
        ThemeResourceDictionary["UnoDock.ActiveTitleBrush"] = palette.ActiveTitle;
        ThemeResourceDictionary["UnoDock.PaneBrush"] = new SolidColorBrush(dark ? Microsoft.UI.ColorHelper.FromArgb(255, 30, 34, 43) : Microsoft.UI.ColorHelper.FromArgb(255, 250, 251, 253));
        ThemeResourceDictionary["UnoDock.HeaderBrush"] = new SolidColorBrush(dark ? Microsoft.UI.ColorHelper.FromArgb(255, 39, 45, 57) : Microsoft.UI.ColorHelper.FromArgb(255, 232, 237, 245));
    }
}
