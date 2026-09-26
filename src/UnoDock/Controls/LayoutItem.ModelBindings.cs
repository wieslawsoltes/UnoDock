using Microsoft.UI.Xaml.Data;
using UnoDock.Layout;

namespace UnoDock.Controls;

public abstract partial class LayoutItem
{
    // A source-backed item replaces its model-default binding. Keep the reverse
    // leg of an explicitly TwoWay definition alive for command/application model
    // changes, without replacing its owned native binding expression.
    private void SynchronizeXamlModelValue(string? name)
    {
        if (_disposed || _attaching || LayoutElement == null)
            return;
        var (property, value) = name switch
        {
            nameof(LayoutContent.Title) => (TitleProperty, (object?)LayoutElement.Title),
            nameof(LayoutContent.ContentId) => (ContentIdProperty, LayoutElement.ContentId),
            nameof(LayoutContent.IconSource) => (IconSourceProperty, LayoutElement.IconSource),
            nameof(LayoutContent.CanClose) => (CanCloseProperty, LayoutElement.CanClose),
            nameof(LayoutContent.CanFloat) => (CanFloatProperty, LayoutElement.CanFloat),
            nameof(LayoutContent.IsActive) => (IsActiveProperty, LayoutElement.IsActive),
            nameof(LayoutContent.IsSelected) => (IsSelectedProperty, LayoutElement.IsSelected),
            nameof(LayoutDocument.Description) when LayoutElement is LayoutDocument document => (LayoutDocumentItem.DescriptionProperty, document.Description),
            nameof(LayoutDocument.CanMove) when LayoutElement is LayoutDocument document => (LayoutDocumentItem.CanMoveProperty, document.CanMove),
            nameof(LayoutAnchorable.CanHide) when LayoutElement is LayoutAnchorable tool => (LayoutAnchorableItem.CanHideProperty, tool.CanHide),
            nameof(LayoutAnchorable.CanAutoHide) when LayoutElement is LayoutAnchorable tool => (LayoutAnchorableItem.CanAutoHideProperty, tool.CanAutoHide),
            nameof(LayoutAnchorable.CanDockAsTabbedDocument) when LayoutElement is LayoutAnchorable tool => (LayoutAnchorableItem.CanDockAsTabbedDocumentProperty, tool.CanDockAsTabbedDocument),
            _ => ((DependencyProperty?)null, (object?)null)
        };
        if (property == null || !_xamlBindings.TryGetValue(property, out var binding) || binding.Mode != BindingMode.TwoWay || !ReferenceEquals(GetBindingExpression(property)?.ParentBinding, binding) || Equals(GetValue(property), value))
            return;
        SetValue(property, value);
    }
}
