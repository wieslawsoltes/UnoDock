using System.ComponentModel;
using System.Collections.Specialized;
using Microsoft.UI.Xaml.Data;
using UnoDock.Compatibility;
using UnoDock.Controls;

namespace UnoDock.Gallery;

internal sealed class InputLabManager(InputLabState state, Action<string> record) : DockingManager
{
    internal LayoutDocumentPane? BoundPane
    {
        get;
        set;
    }

    protected override LayoutDocumentPaneControl CreateDocumentPaneControl(LayoutDocumentPane model) => new InputLabPane(model, state, record, ReferenceEquals(model, BoundPane));
    protected override bool OnReceiveWeakEvent(Type managerType, object sender, EventArgs e)
    {
        record($"Source hook: {(e as NotifyCollectionChangedEventArgs)?.Action}; UI thread={DispatcherQueue.HasThreadAccess}");
        return base.OnReceiveWeakEvent(managerType, sender, e);
    }
}
