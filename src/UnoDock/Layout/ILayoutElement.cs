namespace UnoDock.Layout;
public interface ILayoutElement : INotifyPropertyChanged, INotifyPropertyChanging
{
    ILayoutContainer? Parent { get; }

    ILayoutRoot? Root { get; }
}
