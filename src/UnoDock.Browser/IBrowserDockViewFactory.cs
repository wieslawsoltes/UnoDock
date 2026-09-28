namespace UnoDock.Browser;

public interface IBrowserDockViewFactory
{
    /// <summary>Commit publishes title and payload with the current ownership lease.</summary>
    IBrowserDockView Create(BrowserDockItem item, Action<string, string> commit);
}
