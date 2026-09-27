using UnoDock.Browser;

namespace UnoDock.Gallery;

internal sealed class BrowserTextViewFactory : IBrowserDockViewFactory
{
    public IBrowserDockView Create(BrowserDockItem item, Action<string, string> commit)
    {
        if (item.Type != "text")
            throw new NotSupportedException("Unregistered browser content type: " + item.Type);
        return new BrowserTextView(item, commit);
    }
}
