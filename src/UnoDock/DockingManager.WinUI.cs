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
        // DockingManager itself, and subclasses the application's XAML metadata describes (named
        // in its markup), use the default style: an application style without a template then
        // keeps the default template.
        if (GetType() == typeof(DockingManager) || IsDescribedByXamlMetadata(GetType()))
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

    private static bool IsDescribedByXamlMetadata(Type type)
    {
        try
        {
            return Application.Current is IXamlMetadataProvider provider && provider.GetXamlType(type) is { UnderlyingType: var described } && described == type;
        }
        catch (Exception error) when (error is System.Runtime.InteropServices.COMException or InvalidOperationException)
        {
            return false;
        }
    }
}
#endif
