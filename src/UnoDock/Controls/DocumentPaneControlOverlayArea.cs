using UnoDock.Internal;
using UnoDock.Layout;

namespace UnoDock.Controls;

public class DocumentPaneControlOverlayArea : OverlayArea
{
    public DocumentPaneControlOverlayArea(Rect bounds) => SetScreenDetectionArea(bounds);
}
