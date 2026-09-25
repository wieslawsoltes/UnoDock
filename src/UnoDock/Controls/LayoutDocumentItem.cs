using Microsoft.UI.Xaml.Data;
using UnoDock.Internal;
using UnoDock.Layout;

namespace UnoDock.Controls;

    #if WINDOWS
    #else
    #endif
public partial class LayoutDocumentItem : LayoutItem
{
    protected override void Close() => LayoutElement.Close();
    protected override void OnVisibilityChanged() { if (LayoutElement is LayoutDocument document) document.IsVisible = Visibility == Visibility.Visible; }
    protected override void SetDefaultBindings() { base.SetDefaultBindings(); BindDefault(DescriptionProperty, nameof(LayoutDocument.Description)); }
}
