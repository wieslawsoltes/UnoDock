using UnoDock.Layout;

namespace UnoDock.Controls;

public abstract partial class LayoutItem
{
    private void SynchronizeContainerStyle(long generation)
    {
        var model = LayoutElement;
        var parentVersion = model.ParentVersion;
        var root = model.Root;
        var properties = new List<(DependencyProperty Property, string Name)>
        {
            (TitleProperty, nameof(Title)),
            (ContentIdProperty, nameof(ContentId)),
            (IconSourceProperty, nameof(IconSource)),
            (CanCloseProperty, nameof(CanClose)),
            (CanFloatProperty, nameof(CanFloat))
        };
        if (this is LayoutDocumentItem)
            properties.Add((LayoutDocumentItem.DescriptionProperty, nameof(LayoutDocumentItem.Description)));
        if (this is LayoutAnchorableItem)
            properties.Add((LayoutAnchorableItem.CanHideProperty, nameof(LayoutAnchorableItem.CanHide)));
        // Selection and activation can invoke application policy; publish the
        // content identity and capabilities before either transition is requested.
        properties.Add((IsSelectedProperty, nameof(IsSelected)));
        properties.Add((IsActiveProperty, nameof(IsActive)));
        using var batch = (root as LayoutRoot)?.BeginUpdate();
        foreach (var (property, name) in properties)
        {
            if (_disposed || generation != _styleGeneration || model.ParentVersion != parentVersion || !ReferenceEquals(model.Root, root))
                break;
            if (HasStyleSetter(property) || _styleBindings.ContainsKey(property))
                SynchronizeAdapterProperty(name);
        }
    }
}
