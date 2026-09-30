namespace UnoDock.Themes;
/// <summary>Metro-style docking chrome. The palette is a fixed light design, so
/// the docking chrome keeps its colors when the application switches to Dark.</summary>
public class MetroTheme : Theme
{
    public override Uri GetResourceUri() => new("ms-appx:///UnoDock.Themes.Metro/Theme.xaml");
    protected override ElementTheme ChromeTheme => ElementTheme.Light;
}
