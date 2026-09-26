namespace UnoDock.Controls;
/// <summary>Per-item binding definitions for native Uno/WinUI Style setters.
/// Definitions may be shared; each adapter owns its own native Binding instance.</summary>
public static class LayoutItemBindings
{
    public static readonly DependencyProperty TitleProperty = Register("Title");
    public static LayoutBinding? GetTitle(DependencyObject owner) => (LayoutBinding?)owner.GetValue(TitleProperty);
    public static void SetTitle(DependencyObject owner, LayoutBinding? value) => owner.SetValue(TitleProperty, value);
    public static readonly DependencyProperty ContentIdProperty = Register("ContentId");
    public static LayoutBinding? GetContentId(DependencyObject owner) => (LayoutBinding?)owner.GetValue(ContentIdProperty);
    public static void SetContentId(DependencyObject owner, LayoutBinding? value) => owner.SetValue(ContentIdProperty, value);
    public static readonly DependencyProperty IconSourceProperty = Register("IconSource");
    public static LayoutBinding? GetIconSource(DependencyObject owner) => (LayoutBinding?)owner.GetValue(IconSourceProperty);
    public static void SetIconSource(DependencyObject owner, LayoutBinding? value) => owner.SetValue(IconSourceProperty, value);
    public static readonly DependencyProperty CanCloseProperty = Register("CanClose");
    public static LayoutBinding? GetCanClose(DependencyObject owner) => (LayoutBinding?)owner.GetValue(CanCloseProperty);
    public static void SetCanClose(DependencyObject owner, LayoutBinding? value) => owner.SetValue(CanCloseProperty, value);
    public static readonly DependencyProperty CanFloatProperty = Register("CanFloat");
    public static LayoutBinding? GetCanFloat(DependencyObject owner) => (LayoutBinding?)owner.GetValue(CanFloatProperty);
    public static void SetCanFloat(DependencyObject owner, LayoutBinding? value) => owner.SetValue(CanFloatProperty, value);
    public static readonly DependencyProperty IsSelectedProperty = Register("IsSelected");
    public static LayoutBinding? GetIsSelected(DependencyObject owner) => (LayoutBinding?)owner.GetValue(IsSelectedProperty);
    public static void SetIsSelected(DependencyObject owner, LayoutBinding? value) => owner.SetValue(IsSelectedProperty, value);
    public static readonly DependencyProperty IsActiveProperty = Register("IsActive");
    public static LayoutBinding? GetIsActive(DependencyObject owner) => (LayoutBinding?)owner.GetValue(IsActiveProperty);
    public static void SetIsActive(DependencyObject owner, LayoutBinding? value) => owner.SetValue(IsActiveProperty, value);
    public static readonly DependencyProperty CanHideProperty = Register("CanHide");
    public static LayoutBinding? GetCanHide(DependencyObject owner) => (LayoutBinding?)owner.GetValue(CanHideProperty);
    public static void SetCanHide(DependencyObject owner, LayoutBinding? value) => owner.SetValue(CanHideProperty, value);
    public static readonly DependencyProperty DescriptionProperty = Register("Description");
    public static LayoutBinding? GetDescription(DependencyObject owner) => (LayoutBinding?)owner.GetValue(DescriptionProperty);
    public static void SetDescription(DependencyObject owner, LayoutBinding? value) => owner.SetValue(DescriptionProperty, value);
    private static DependencyProperty Register(string name) => DependencyProperty.RegisterAttached(name, typeof(LayoutBinding), typeof(LayoutItemBindings), new PropertyMetadata(null, OnChanged));
    private static void OnChanged(DependencyObject owner, DependencyPropertyChangedEventArgs args)
    {
        if (owner is LayoutItem item)
            item.RefreshBindingDefinitions();
    }

    internal static IEnumerable<(DependencyProperty Target, LayoutBinding? Definition)> Definitions(LayoutItem item)
    {
        yield return (LayoutItem.TitleProperty, GetTitle(item));
        yield return (LayoutItem.ContentIdProperty, GetContentId(item));
        yield return (LayoutItem.IconSourceProperty, GetIconSource(item));
        yield return (LayoutItem.CanCloseProperty, GetCanClose(item));
        yield return (LayoutItem.CanFloatProperty, GetCanFloat(item));
        yield return (LayoutItem.IsSelectedProperty, GetIsSelected(item));
        yield return (LayoutItem.IsActiveProperty, GetIsActive(item));
        if (item is LayoutAnchorableItem)
            yield return (LayoutAnchorableItem.CanHideProperty, GetCanHide(item));
        if (item is LayoutDocumentItem)
            yield return (LayoutDocumentItem.DescriptionProperty, GetDescription(item));
    }
}
