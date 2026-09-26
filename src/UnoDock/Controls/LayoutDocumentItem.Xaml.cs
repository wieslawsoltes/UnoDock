namespace UnoDock.Controls;

public partial class LayoutDocumentItem
{
    public static readonly DependencyProperty CanMoveProperty = DependencyProperty.Register(nameof(CanMove), typeof(bool), typeof(LayoutDocumentItem), new PropertyMetadata(true, (owner, args) => ((LayoutDocumentItem)owner).OnAdapterPropertyChanged(nameof(CanMove), args)));
    public bool CanMove
    {
        get => (bool)GetValue(CanMoveProperty);
        set => SetValue(CanMoveProperty, value);
    }
}
