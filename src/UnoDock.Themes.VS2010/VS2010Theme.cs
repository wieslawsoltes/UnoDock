namespace UnoDock.Themes;
/// <summary>VS2010-style docking chrome. The palette is a fixed light design, so
/// the docking chrome keeps its colors when the application switches to Dark.</summary>
public class VS2010Theme : Theme
{
    public override Uri GetResourceUri() => new("ms-appx:///UnoDock.Themes.VS2010/Theme.xaml");
    protected override ElementTheme ChromeTheme => ElementTheme.Light;
}
