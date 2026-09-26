namespace UnoDock.Themes;

public abstract partial class Theme : DependencyObject
{
    public Theme()
    {
    }

    internal event EventHandler? Changed;
    /// <summary>Notify attached managers after a declarative theme setting changes.</summary>
    protected void InvalidateTheme() => Changed?.Invoke(this, EventArgs.Empty);
    public abstract Uri GetResourceUri();
    public virtual ResourceDictionary GetResourceDictionary() => new()
    {
        Source = GetResourceUri()
    };
}
