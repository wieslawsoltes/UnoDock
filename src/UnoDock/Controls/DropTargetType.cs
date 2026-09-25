using UnoDock.Internal;
using UnoDock.Layout;

namespace UnoDock.Controls;

public enum DropTargetType
{
    DockingManagerDockLeft = 0,
    DockingManagerDockTop = 1,
    DockingManagerDockRight = 2,
    DockingManagerDockBottom = 3,
    DocumentPaneDockLeft = 4,
    DocumentPaneDockTop = 5,
    DocumentPaneDockRight = 6,
    DocumentPaneDockBottom = 7,
    DocumentPaneDockInside = 8,
    DocumentPaneGroupDockInside = 9,
    AnchorablePaneDockLeft = 10,
    AnchorablePaneDockTop = 11,
    AnchorablePaneDockRight = 12,
    AnchorablePaneDockBottom = 13,
    AnchorablePaneDockInside = 14,
    DocumentPaneDockAsAnchorableLeft = 15,
    DocumentPaneDockAsAnchorableTop = 16,
    DocumentPaneDockAsAnchorableRight = 17,
    DocumentPaneDockAsAnchorableBottom = 18
}
