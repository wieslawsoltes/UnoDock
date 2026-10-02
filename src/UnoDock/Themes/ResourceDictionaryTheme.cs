namespace UnoDock.Themes;
/// <summary>A consumer-authored XAML dictionary, including merged and theme dictionaries.</summary>
[ContentProperty(Name = nameof(Resources))]
public sealed partial class ResourceDictionaryTheme : Theme
{
    public static readonly DependencyProperty ResourcesProperty = DependencyProperty.Register(nameof(Resources), typeof(ResourceDictionary), typeof(ResourceDictionaryTheme), new PropertyMetadata(null, (owner, args) => ((ResourceDictionaryTheme)owner).OnResourcesChanged(args)));
    private bool _restoringResources;
    private void OnResourcesChanged(DependencyPropertyChangedEventArgs args)
    {
        if (_restoringResources)
            return;
        if (args.NewValue is not ResourceDictionary)
        {
            _restoringResources = true;
            try
            {
                SetValue(ResourcesProperty, args.OldValue);
            }
            finally
            {
                _restoringResources = false;
            }

            throw new ArgumentNullException(nameof(Resources));
        }

        InvalidateTheme();
    }

    public ResourceDictionaryTheme() => Resources = new ResourceDictionary();
    public ResourceDictionary Resources
    {
        get => (ResourceDictionary?)GetValue(ResourcesProperty) ?? throw new InvalidOperationException("A theme requires a resource dictionary.");
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            SetValue(ResourcesProperty, value);
        }
    }

    /// <summary>The dictionary's source URI.</summary>
        /// <exception cref = "InvalidOperationException">The dictionary was created in code and has no source; use <see cref = "GetResourceDictionary"/>.</exception>
        public override Uri GetResourceUri() => Resources.Source ?? throw new InvalidOperationException("This theme's dictionary was created in code and has no source URI; use GetResourceDictionary().");
    public override ResourceDictionary GetResourceDictionary() => Resources;
    /// <summary>Refresh attached managers after editing entries in an existing dictionary.</summary>
    public void Refresh() => InvalidateTheme();
}
