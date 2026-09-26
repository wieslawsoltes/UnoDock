namespace UnoDock.Themes;
/// <summary>Uno/WinUI semantic colors with compact docking geometry. The default
/// constructor follows the owner; an explicit Light/Dark theme remains coherent
/// even when the containing window requests the other theme.</summary>
public sealed class FluentTheme : DictionaryTheme
{
    private readonly Dictionary<string, Brush> _published = new(StringComparer.Ordinal);
    public FluentTheme() : this(ElementTheme.Default)
    {
    }

    public FluentTheme(ElementTheme theme)
    {
        if (!Enum.IsDefined(theme))
            throw new ArgumentOutOfRangeException(nameof(theme));
        RequestedTheme = theme;
    }

    public static readonly DependencyProperty RequestedThemeProperty = DependencyProperty.Register(nameof(RequestedTheme), typeof(ElementTheme), typeof(FluentTheme), new PropertyMetadata(ElementTheme.Default, (owner, args) => ((FluentTheme)owner).ChangeRequestedTheme(args)));
    private bool _restoringTheme;
    public ElementTheme RequestedTheme
    {
        get => (ElementTheme)GetValue(RequestedThemeProperty);
        set
        {
            if (!Enum.IsDefined(value))
                throw new ArgumentOutOfRangeException(nameof(value));
            SetValue(RequestedThemeProperty, value);
        }
    }

    internal event EventHandler? Changed;
    private void ChangeRequestedTheme(DependencyPropertyChangedEventArgs args)
    {
        if (_restoringTheme)
            return;
        if (!Enum.IsDefined((ElementTheme)args.NewValue))
        {
            _restoringTheme = true;
            try
            {
                SetValue(RequestedThemeProperty, args.OldValue);
            }
            finally
            {
                _restoringTheme = false;
            }

            throw new ArgumentOutOfRangeException(nameof(RequestedTheme));
        }

        foreach (var (key, owned) in _published)
        {
            if (ThemeResourceDictionary.TryGetValue(key, out var current) && ReferenceEquals(current, owned))
                ThemeResourceDictionary.Remove(key);
        }

        _published.Clear();
        if (RequestedTheme != ElementTheme.Default)
            foreach (var slot in Internal.DockThemeResources.Slots(Internal.DockChrome.Default(RequestedTheme == ElementTheme.Dark)))
                if (!ThemeResourceDictionary.Keys.Contains("UnoDock." + slot.Dock))
                    Publish(slot.Dock, slot.Fallback);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    internal void UpdateResources(DockingManager manager)
    {
        if (RequestedTheme == ElementTheme.Default)
            return;
        foreach (var slot in Internal.DockThemeResources.Slots(Internal.DockChrome.Default(RequestedTheme == ElementTheme.Dark)))
        {
            var key = "UnoDock." + slot.Dock;
            if (ThemeResourceDictionary.TryGetValue(key, out var current) && (!_published.TryGetValue(key, out var previous) || !ReferenceEquals(current, previous)))
                continue;
            var brush = Internal.DockThemeResources.Find(manager, key, ThemeResourceDictionary) as Brush ?? Internal.DockThemeResources.Find(manager, slot.System) as Brush ?? slot.Fallback;
            Publish(slot.Dock, brush);
        }
    }

    private void Publish(string name, Brush brush)
    {
        var key = "UnoDock." + name;
        if (!_published.TryGetValue(key, out var previous) || !ReferenceEquals(previous, brush) || !ThemeResourceDictionary.TryGetValue(key, out var value) || !ReferenceEquals(value, brush))
            ThemeResourceDictionary[key] = brush;
        _published[key] = brush;
    }
}
