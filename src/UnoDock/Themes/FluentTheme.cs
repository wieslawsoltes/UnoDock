namespace UnoDock.Themes;
/// <summary>Uno/WinUI semantic colors with compact docking geometry. The default
/// constructor follows the owner; an explicit Light/Dark theme remains coherent
/// even when the containing window requests the other theme.</summary>
public sealed class FluentTheme : DictionaryTheme
{
    public static readonly DependencyProperty RequestedThemeProperty = DependencyProperty.Register(nameof(RequestedTheme), typeof(ElementTheme), typeof(FluentTheme), new PropertyMetadata(ElementTheme.Default, (owner, args) => ((FluentTheme)owner).OnSettingChanged(args, true)));
    public static readonly DependencyProperty DensityProperty = DependencyProperty.Register(nameof(Density), typeof(DockDensity), typeof(FluentTheme), new PropertyMetadata(DockDensity.Compact, (owner, args) => ((FluentTheme)owner).OnSettingChanged(args, false)));
    private bool _restoringSetting;
    private readonly Dictionary<string, Brush> _published = new(StringComparer.Ordinal);
    public FluentTheme() : this(ElementTheme.Default)
    {
    }

    public FluentTheme(ElementTheme theme)
    {
        if (!Enum.IsDefined(theme))
            throw new ArgumentOutOfRangeException(nameof(theme));
        RequestedTheme = theme;
        // Preserve the explicit-theme dictionary contract. These aliases are
        // refreshed from application resources on use, never cloned/recolored.
        if (theme != ElementTheme.Default)
            foreach (var slot in Internal.DockThemeResources.Slots(Internal.DockChrome.Default(theme == ElementTheme.Dark)))
                Publish(slot.Dock, slot.Fallback);
    }

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

    public DockDensity Density
    {
        get => (DockDensity)GetValue(DensityProperty);
        set
        {
            if (!Enum.IsDefined(value))
                throw new ArgumentOutOfRangeException(nameof(value));
            SetValue(DensityProperty, value);
        }
    }

    private void OnSettingChanged(DependencyPropertyChangedEventArgs args, bool theme)
    {
        if (_restoringSetting)
            return;
        if (theme ? !Enum.IsDefined((ElementTheme)args.NewValue) : !Enum.IsDefined((DockDensity)args.NewValue))
        {
            _restoringSetting = true;
            try
            {
                SetValue(theme ? RequestedThemeProperty : DensityProperty, args.OldValue);
            }
            finally
            {
                _restoringSetting = false;
            }

            throw new ArgumentOutOfRangeException(theme ? nameof(RequestedTheme) : nameof(Density));
        }

        if (theme)
        {
            // Retire only aliases we supplied; never erase consumer overrides.
            foreach (var pair in _published)
                if (ThemeResourceDictionary.TryGetValue(pair.Key, out var value) && ReferenceEquals(value, pair.Value))
                    ThemeResourceDictionary.Remove(pair.Key);
            _published.Clear();
        }

        InvalidateTheme();
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
