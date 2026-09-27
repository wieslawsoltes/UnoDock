namespace UnoDock.Browser;

internal sealed record BrowserDockRequest
{
    public string Op { get; init; } = "read";
    public string? Id { get; init; }
    public long Lease { get; init; }
    public string? Title { get; init; }
    public string? Payload { get; init; }
}
