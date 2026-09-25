using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Input;
using UnoDock.Compatibility;
using UnoDock.Internal;

namespace UnoDock.Controls;
public class MenuItemEx : MenuFlyoutItem
{
    public static readonly DependencyProperty IconTemplateProperty = DependencyProperty.Register(nameof(IconTemplate), typeof(DataTemplate), typeof(MenuItemEx), new PropertyMetadata(null, (d, e) => ((MenuItemEx)d).OnIconTemplateChanged(e)));
    public static readonly DependencyProperty IconTemplateSelectorProperty = DependencyProperty.Register(nameof(IconTemplateSelector), typeof(DataTemplateSelector), typeof(MenuItemEx), new PropertyMetadata(null, (d, e) => ((MenuItemEx)d).OnIconTemplateSelectorChanged(e)));
    private DataTemplate? _appliedTemplate;
    private IconElement? _generatedIcon;
    public DataTemplate? IconTemplate { get => (DataTemplate? )GetValue(IconTemplateProperty); set => SetValue(IconTemplateProperty, value); }
    public DataTemplateSelector? IconTemplateSelector { get => (DataTemplateSelector? )GetValue(IconTemplateSelectorProperty); set => SetValue(IconTemplateSelectorProperty, value); }

    public MenuItemEx() => DataContextChanged += (_, _) => UpdateIcon();
    protected virtual void OnIconTemplateChanged(DependencyPropertyChangedEventArgs e) => UpdateIcon();
    protected virtual void OnIconTemplateSelectorChanged(DependencyPropertyChangedEventArgs e) => UpdateIcon();
    private void UpdateIcon()
    {
        var template = IconTemplateSelector?.SelectTemplate(DataContext, this) ?? IconTemplate;
        if (!ReferenceEquals(template, _appliedTemplate))
        {
            var icon = template?.LoadContent();
            if (icon != null && icon is not IconElement)
                throw new InvalidOperationException("A WinUI menu icon template must produce an IconElement (for example FontIcon or PathIcon).");
            if (template != null || ReferenceEquals(Icon, _generatedIcon))
                Icon = (IconElement? )icon;
            _generatedIcon = icon as IconElement;
            _appliedTemplate = template;
        }

        if (_generatedIcon != null)
            _generatedIcon.DataContext = DataContext;
    }
}
