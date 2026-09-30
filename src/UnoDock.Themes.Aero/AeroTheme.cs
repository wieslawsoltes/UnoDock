namespace UnoDock.Themes;
/// <summary>Aero-style docking chrome. The palette is a fixed light design, so
/// the docking chrome keeps its colors when the application switches to Dark.</summary>
public class AeroTheme : Theme
{
    public override Uri GetResourceUri() => new("ms-appx:///UnoDock.Themes.Aero/Theme.xaml");
    protected override ElementTheme ChromeTheme => ElementTheme.Light;
}
