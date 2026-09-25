using UnoDock.Internal;
using UnoDock.Layout;

namespace UnoDock.Controls;

internal interface IModelDropArea : IDropArea
{
    ILayoutElement? Model
    {
        get;
    }

    DockingManager? Manager
    {
        get;
    }
}
