namespace UnoDock.Browser;

internal sealed record BrowserDockReport
{
    public string WindowId
    {
        get;
        init;
    } = "";
    public long ProjectionCount
    {
        get;
        init;
    }

    public string Active
    {
        get;
        init;
    } = "";
    public BrowserDockItem[] Items
    {
        get;
        init;
    } = [];
}
