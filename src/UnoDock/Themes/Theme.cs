namespace UnoDock.Themes;

public abstract partial class Theme : DependencyObject
{
    public Theme() { }
    public abstract Uri GetResourceUri();
    public virtual ResourceDictionary GetResourceDictionary() => new() { Source = GetResourceUri() };
}
