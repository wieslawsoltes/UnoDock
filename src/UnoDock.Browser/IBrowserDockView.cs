using Microsoft.UI.Xaml;

namespace UnoDock.Browser;
/// <summary>The application controls construction and payload serialization in each runtime.</summary>
public interface IBrowserDockView : IDisposable
{
    FrameworkElement View
    {
        get;
    }

    string Payload
    {
        get;
    }

    void Update(BrowserDockItem item);
}
