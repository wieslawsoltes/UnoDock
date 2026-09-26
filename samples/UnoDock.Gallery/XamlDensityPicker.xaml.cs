namespace UnoDock.Gallery;
/// <summary>A sample of two-way native dependency-property binding through a compiled UserControl.</summary>
public sealed partial class XamlDensityPicker : UserControl
{
    public static readonly DependencyProperty DensityProperty = DependencyProperty.Register(nameof(Density), typeof(DockChromeDensity), typeof(XamlDensityPicker), new PropertyMetadata(DockChromeDensity.Comfortable, (owner, _) => ((XamlDensityPicker)owner).Synchronize()));
    private bool _ready, _synchronizing;
    public DockChromeDensity Density
    {
        get => (DockChromeDensity)GetValue(DensityProperty);
        set => SetValue(DensityProperty, value);
    }

    public XamlDensityPicker()
    {
        InitializeComponent();
        _ready = true;
        Synchronize();
    }

    private void Synchronize()
    {
        if (!_ready || _synchronizing)
            return;
        _synchronizing = true;
        try
        {
            Choices.SelectedIndex = (int)Density;
        }
        finally
        {
            _synchronizing = false;
        }
    }

    private void SelectionChanged(object sender, SelectionChangedEventArgs args)
    {
        if (_ready && !_synchronizing && Choices.SelectedIndex >= 0)
            Density = (DockChromeDensity)Choices.SelectedIndex;
    }
}
