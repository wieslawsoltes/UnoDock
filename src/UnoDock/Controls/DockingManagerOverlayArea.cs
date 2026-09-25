using UnoDock.Internal;
using UnoDock.Layout;

namespace UnoDock.Controls;

public class DockingManagerOverlayArea : OverlayArea
{
    public DockingManagerOverlayArea(Rect bounds) => SetScreenDetectionArea(bounds);
}
