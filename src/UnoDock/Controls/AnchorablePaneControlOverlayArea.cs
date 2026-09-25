using UnoDock.Internal;
using UnoDock.Layout;

namespace UnoDock.Controls;
public class AnchorablePaneControlOverlayArea : OverlayArea
{
    public AnchorablePaneControlOverlayArea(Rect bounds) => SetScreenDetectionArea(bounds);
}
