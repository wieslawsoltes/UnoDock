namespace UnoDock.Layout;

public interface ILayoutOrientableGroup : ILayoutGroup
{
    Orientation Orientation
    {
        get; set;
    }
}
