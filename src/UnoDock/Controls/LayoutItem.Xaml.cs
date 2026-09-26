using Microsoft.UI.Xaml.Data;

namespace UnoDock.Controls;

public abstract partial class LayoutItem
{
    private readonly Dictionary<DependencyProperty, Binding> _xamlBindings = [];
    private bool _refreshingXamlBindings;
    private bool _xamlBindingPending;
    private long _xamlBindingRequest;
    internal void RefreshXamlBindings()
    {
        if (_disposed || _manager == null)
            return;
        _xamlBindingRequest++;
        _xamlBindingPending = true;
        if (_attaching || _refreshingXamlBindings)
            return;
        _refreshingXamlBindings = true;
        try
        {
            for (var pass = 0; _xamlBindingPending && !_disposed; pass++)
            {
                if (pass == 64)
                    throw new InvalidOperationException("Layout-item binding callbacks did not converge.");
                _xamlBindingPending = false;
                var request = _xamlBindingRequest;
                var configuration = LayoutItemBindings.GetBindings(this);
                bool Current() => !_disposed && request == _xamlBindingRequest && ReferenceEquals(configuration, LayoutItemBindings.GetBindings(this));
                var plans = new Dictionary<DependencyProperty, Binding>();
                if (configuration != null)
                    foreach (var definition in configuration.ToArray())
                    {
                        ArgumentNullException.ThrowIfNull(definition);
                        var property = BindingProperty(definition.Property);
                        if (!plans.TryAdd(property, definition.CreateBinding()))
                            throw new ArgumentException("Duplicate layout-item binding target: " + definition.Property);
                    }

                if (!Current())
                    continue;
                // Retiring a binding exposes defaults momentarily. This must not
                // clear the model's title, activate another tab or mutate policy.
                _attaching = true;
                try
                {
                    ClearXamlBindings();
                    foreach (var property in plans.Keys)
                    {
                        if (!Current())
                            break;
                        if (_bindings.TryGetValue(property, out var inherited) && ReferenceEquals(GetBindingExpression(property)?.ParentBinding, inherited))
                        {
                            _bindings.Remove(property);
                            ClearValue(property);
                        }

                        if (_commands.TryGetValue(property, out var command) && ReferenceEquals(GetValue(property), command))
                        {
                            _commands.Remove(property);
                            ClearValue(property);
                        }
                    }
                }
                finally
                {
                    _attaching = false;
                }

                if (!Current())
                    continue;
                foreach (var (property, binding) in plans)
                {
                    if (!Current())
                        break;
                    // Preserve every application-owned local binding/value.
                    if (ReadLocalValue(property) != DependencyProperty.UnsetValue)
                        continue;
                    _xamlBindings.Add(property, binding);
                    SetBinding(property, binding);
                }

                if (Current())
                {
                    SetDefaultBindings();
                    InitDefaultCommands();
                }
            }
        }
        finally
        {
            _refreshingXamlBindings = false;
        }
    }

    private void ClearXamlBindings()
    {
        var bindings = _xamlBindings.ToArray();
        _xamlBindings.Clear();
        foreach (var (property, binding) in bindings)
            if (ReferenceEquals(GetBindingExpression(property)?.ParentBinding, binding))
                ClearValue(property);
    }

    private void PublishLiteralStyleValues()
    {
        if (_disposed || LayoutElement == null)
            return;
        if (HasStyleSetter(TitleProperty))
            LayoutElement.Title = Title;
        if (HasStyleSetter(ContentIdProperty))
            LayoutElement.ContentId = ContentId;
        if (HasStyleSetter(IconSourceProperty))
            LayoutElement.IconSource = IconSource;
        if (HasStyleSetter(CanCloseProperty))
            LayoutElement.CanClose = CanClose;
        if (HasStyleSetter(CanFloatProperty))
            LayoutElement.CanFloat = CanFloat;
        if (HasStyleSetter(IsSelectedProperty))
            LayoutElement.IsSelected = IsSelected;
        if (HasStyleSetter(IsActiveProperty))
            LayoutElement.IsActive = IsActive;
        if (this is LayoutDocumentItem document && LayoutElement is Layout.LayoutDocument model && HasStyleSetter(LayoutDocumentItem.DescriptionProperty))
            model.Description = document.Description;
        if (this is LayoutAnchorableItem tool && LayoutElement is Layout.LayoutAnchorable anchorable && HasStyleSetter(LayoutAnchorableItem.CanHideProperty))
            anchorable.CanHide = tool.CanHide;
    }

    private DependencyProperty BindingProperty(string name) => name switch
    {
        nameof(Title) => TitleProperty,
        nameof(ContentId) => ContentIdProperty,
        nameof(IconSource) => IconSourceProperty,
        nameof(IsActive) => IsActiveProperty,
        nameof(IsSelected) => IsSelectedProperty,
        nameof(CanClose) => CanCloseProperty,
        nameof(CanFloat) => CanFloatProperty,
        nameof(ActivateCommand) => ActivateCommandProperty,
        nameof(CloseCommand) => CloseCommandProperty,
        nameof(CloseAllCommand) => CloseAllCommandProperty,
        nameof(CloseAllButThisCommand) => CloseAllButThisCommandProperty,
        nameof(FloatCommand) => FloatCommandProperty,
        nameof(DockAsDocumentCommand) => DockAsDocumentCommandProperty,
        nameof(NewHorizontalTabGroupCommand) => NewHorizontalTabGroupCommandProperty,
        nameof(NewVerticalTabGroupCommand) => NewVerticalTabGroupCommandProperty,
        nameof(MoveToNextTabGroupCommand) => MoveToNextTabGroupCommandProperty,
        nameof(MoveToPreviousTabGroupCommand) => MoveToPreviousTabGroupCommandProperty,
        "Description" when this is LayoutDocumentItem => LayoutDocumentItem.DescriptionProperty,
        "CanHide" when this is LayoutAnchorableItem => LayoutAnchorableItem.CanHideProperty,
        "HideCommand" when this is LayoutAnchorableItem => LayoutAnchorableItem.HideCommandProperty,
        "AutoHideCommand" when this is LayoutAnchorableItem => LayoutAnchorableItem.AutoHideCommandProperty,
        "DockCommand" when this is LayoutAnchorableItem => LayoutAnchorableItem.DockCommandProperty,
        _ => throw new ArgumentException("Unknown or incompatible layout-item binding target: " + name)
    };
}
