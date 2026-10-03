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

    // Native WinUI raises no routed GotFocus or LostFocus for some focus moves (an element that
    // keeps focus while its view is hidden and shown again); the application-wide focus event
    // still reports them, so the item remembers its editor from it.
    private bool _trackingFocus;
    private void TrackFocus(bool track)
    {
        if (track == _trackingFocus)
            return;
        _trackingFocus = track;
        if (track)
            Microsoft.UI.Xaml.Input.FocusManager.GotFocus += OnFocusManagerGotFocus;
        else
            Microsoft.UI.Xaml.Input.FocusManager.GotFocus -= OnFocusManagerGotFocus;
    }

    private void OnFocusManagerGotFocus(object? sender, Microsoft.UI.Xaml.Input.FocusManagerGotFocusEventArgs e)
    {
        if (_disposed || e.NewFocusedElement is not DependencyObject focused || !DispatcherQueue.HasThreadAccess)
            return;
        for (var node = focused; node != null; node = VisualTreeHelper.GetParent(node))
            if (node is ContentPresenter view)
                foreach (var item in _items.Values)
                    if (ReferenceEquals(item.ExistingView, view))
                    {
                        item.Remember(focused);
                        return;
                    }
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
