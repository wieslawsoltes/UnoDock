namespace UnoDock.Browser;

internal sealed record BrowserDockReport
{
    public string WindowId
    {
        get;
        init;
    } = "";
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
