namespace UnoDock;

public partial class DockingManager
{
    public static readonly DependencyProperty ChromeDensityProperty = DependencyProperty.Register(nameof(ChromeDensity), typeof(DockChromeDensity), typeof(DockingManager), new PropertyMetadata(DockChromeDensity.Default, (owner, args) => ((DockingManager)owner).ChangeChromeDensity(args)));
    private bool _restoringDensity;
    /// <summary>Changes retained chrome geometry without changing layout ownership or editor instances.</summary>
    public DockChromeDensity ChromeDensity
    {
        get => (DockChromeDensity)GetValue(ChromeDensityProperty);
        set
        {
            if (!Enum.IsDefined(value))
                throw new ArgumentOutOfRangeException(nameof(value));
            SetValue(ChromeDensityProperty, value);
        }
    }

    private void ChangeChromeDensity(DependencyPropertyChangedEventArgs args)
    {
        if (_restoringDensity)
            return;
        if (!Enum.IsDefined((DockChromeDensity)args.NewValue))
        {
            _restoringDensity = true;
            try
            {
                SetValue(ChromeDensityProperty, args.OldValue);
            }
            finally
            {
                _restoringDensity = false;
            }

            throw new ArgumentOutOfRangeException(nameof(ChromeDensity));
        }

        InvalidateView();
    }
}
