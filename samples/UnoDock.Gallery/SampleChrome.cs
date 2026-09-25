using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Shapes;
using PathShape = Microsoft.UI.Xaml.Shapes.Path;

namespace UnoDock.Gallery;

internal static class SampleChrome
{
    internal const double MenuHeight = 28;
    internal static SolidColorBrush Color(uint rgb) => new(Microsoft.UI.ColorHelper.FromArgb(255, (byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb));
    internal static SamplePalette Default(bool dark) => new(Color(dark ? 0xf2f2f2u : 0x202020u), Color(dark ? 0x444444u : 0xdceaf4u), Color(dark ? 0x555555u : 0xb9d8edu), Color(0x0067b8));
    internal static SampleButton Button(string label, Action action, string? name = null)
    {
        var button = new SampleButton
        {
            Content = label
        };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(button, name ?? label);
        ToolTipService.SetToolTip(button, name ?? label);
        button.Click += (_, _) => action();
        return button;
    }

    internal static MenuBar CreateMenuBar()
    {
        // A fixed Grid row does not override a Height set by the native control's
        // default style. Set the control itself, not just its clipping parent.
        var menu = new MenuBar
        {
            Height = MenuHeight,
            MinHeight = 0,
            Padding = new(2, 0, 2, 0),
            FontSize = 12
        };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(menu, "SampleMenu");
        return menu;
    }

    internal static MenuBarItem CreateMenuItem(string title)
    {
        // Independently authored sample chrome around the native MenuBarItem.
        // ContentButton is the Uno/WinUI template part responsible for the native
        // flyout, keyboard traversal and accessibility; none is reimplemented.
        var item = new MenuBarItem
        {
            Title = title,
            FontSize = 12,
            Height = MenuHeight,
            MinHeight = 0,
            MinWidth = 0
        };
        item.Template = (ControlTemplate)XamlReader.Load("""
            <ControlTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                             TargetType="MenuBarItem">
              <Grid>
                <VisualStateManager.VisualStateGroups>
                  <VisualStateGroup x:Name="CommonStates">
                    <VisualState x:Name="Normal"/>
                    <VisualState x:Name="PointerOver">
                      <VisualState.Setters>
                        <Setter Target="Frame.Background" Value="{ThemeResource MenuBarItemBackgroundPointerOver}"/>
                        <Setter Target="Frame.BorderBrush" Value="{ThemeResource MenuBarItemBorderBrushPointerOver}"/>
                      </VisualState.Setters>
                    </VisualState>
                    <VisualState x:Name="Pressed">
                      <VisualState.Setters>
                        <Setter Target="Frame.Background" Value="{ThemeResource MenuBarItemBackgroundPressed}"/>
                      </VisualState.Setters>
                    </VisualState>
                    <VisualState x:Name="Selected">
                      <VisualState.Setters>
                        <Setter Target="Frame.Background" Value="{ThemeResource MenuBarItemBackgroundSelected}"/>
                        <Setter Target="Frame.BorderBrush" Value="{ThemeResource MenuBarItemBorderBrushSelected}"/>
                      </VisualState.Setters>
                    </VisualState>
                  </VisualStateGroup>
                </VisualStateManager.VisualStateGroups>
                <Border x:Name="Frame" Background="{TemplateBinding Background}"
                        BorderBrush="{TemplateBinding BorderBrush}" BorderThickness="1">
                  <Button x:Name="ContentButton" Content="{TemplateBinding Title}"
                          Foreground="{TemplateBinding Foreground}" FontSize="{TemplateBinding FontSize}"
                          MinHeight="0" MinWidth="0" Padding="8,0" BorderThickness="0"
                          Background="Transparent" IsTabStop="False" AutomationProperties.AccessibilityView="Raw">
                    <Button.Template>
                      <ControlTemplate TargetType="Button">
                        <ContentPresenter Content="{TemplateBinding Content}"
                                          Foreground="{TemplateBinding Foreground}" FontSize="{TemplateBinding FontSize}"
                                          Padding="{TemplateBinding Padding}"
                                          HorizontalContentAlignment="Center" VerticalContentAlignment="Center"/>
                      </ControlTemplate>
                    </Button.Template>
                  </Button>
                </Border>
              </Grid>
            </ControlTemplate>
            """);
        return item;
    }
}
