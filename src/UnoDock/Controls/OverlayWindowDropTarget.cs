using UnoDock.Internal;
using UnoDock.Layout;

namespace UnoDock.Controls;
public class OverlayWindowDropTarget
{
    public OverlayWindowDropTarget(DockDropPlan plan) => Plan = plan ?? throw new ArgumentNullException(nameof(plan));
    public DockDropPlan Plan { get; }
    public Rect DetectionRect => Plan.PreviewRect;
    public DropTargetType Type => Plan.Type;
    public bool Drop() => Plan.Execute();
}
