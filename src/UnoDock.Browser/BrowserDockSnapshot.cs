namespace UnoDock.Browser;

public sealed record BrowserDockSnapshot
{
    public int Schema { get; init; }
    public string WindowId { get; init; } = "";
    public string Session { get; init; } = "";
    public string Theme { get; init; } = "light";
    public string Active { get; init; } = "";
    public long Sequence { get; init; }
    public BrowserDockItem[] Items { get; init; } = [];
}
