namespace UnoDock.Layout;

public partial class LayoutDocument
{
    static LayoutDocument()
    {
    }

    public static readonly DependencyProperty CanMoveProperty = LayoutXamlProperty.Register<LayoutDocument, bool>(nameof(CanMove), true, owner => owner.CanMove, (owner, value) => owner.CanMove = value);
    public static readonly DependencyProperty DescriptionProperty = LayoutXamlProperty.Register<LayoutDocument, string?>(nameof(Description), null, owner => owner.Description, (owner, value) => owner.Description = value);
}
