namespace UnoDock.Browser;

/// <summary>A portable application payload; it never contains a serialized UIElement.</summary>
public sealed record BrowserDockItem
{
    public string Id { get; init; } = "";
    public string Kind { get; init; } = "document";
    public string Type { get; init; } = "text";
    public string Title { get; init; } = "";
    public string Payload { get; init; } = "";
    public string Owner { get; init; } = "main";
    public string Zone { get; init; } = "center";
    public long Lease { get; init; }
    public long Revision { get; init; }
}
