namespace UnoDock.Layout;

public class ChildrenTreeChangedEventArgs(ChildrenTreeChange change) : EventArgs
{
    public ChildrenTreeChange Change
    {
        get;
        private set;
    } = change;
}
