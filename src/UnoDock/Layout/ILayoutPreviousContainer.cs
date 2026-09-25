namespace UnoDock.Layout;

public interface ILayoutPreviousContainer
{
    ILayoutContainer? PreviousContainer
    {
        get;
    }

    string? PreviousContainerId
    {
        get;
    }

    int PreviousContainerIndex
    {
        get; set;
    }
}
