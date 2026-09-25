namespace UnoDock.Layout;

public interface ILayoutContentSelector
{
    LayoutContent? SelectedContent
    {
        get;
    }

    int SelectedContentIndex
    {
        get;
        set;
    }

    int IndexOf(LayoutContent content);
}
