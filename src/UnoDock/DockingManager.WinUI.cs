#if WINDOWS
using Microsoft.UI.Xaml.Markup;

namespace UnoDock;

public partial class DockingManager
{
    // Generic.xaml's template for DockingManager, targeting Control so that it applies to any subclass.
    private const string SubclassTemplate = """
        <ControlTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" TargetType="Control">
          <Border Background="{TemplateBinding Background}" BorderBrush="{TemplateBinding BorderBrush}" BorderThickness="{TemplateBinding BorderThickness}" CornerRadius="{TemplateBinding CornerRadius}" Padding="{TemplateBinding Padding}">
            <Grid x:Name="PART_AutoHideArea">
              <ContentPresenter x:Name="PART_LayoutHost" HorizontalContentAlignment="Stretch" VerticalContentAlignment="Stretch" />
            </Grid>
          </Border>
        </ControlTemplate>
        """;
    [ThreadStatic]
    private static Style? _subclassStyle;
    /// <summary>Native WinUI resolves a default style's TargetType through XAML metadata, which
        /// does not describe subclasses created only in code: the DockingManager default style would
        /// fail to apply to them. Such subclasses get the same template through a style targeting
        /// Control instead. See docs/native-winui.md.</summary>
        private bool UseSubclassStyle()
    {
        if (GetType() == typeof(DockingManager))
            return false;
        Style = _subclassStyle ??= new(typeof(Control))
        {
            Setters =
            {
                new Setter(HorizontalContentAlignmentProperty, HorizontalAlignment.Stretch),
                new Setter(VerticalContentAlignmentProperty, VerticalAlignment.Stretch),
                new Setter(TemplateProperty, (ControlTemplate)XamlReader.Load(SubclassTemplate))
            }
        };
        return true;
    }
}
#endif
