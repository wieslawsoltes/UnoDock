using UnoDock.Internal;
using UnoDock.Layout;

namespace UnoDock.Controls;

public abstract class OverlayArea
{
    public Rect ScreenDetectionArea
    {
        get; private set;
    }

    protected void SetScreenDetectionArea(Rect rect)
    {
        if (!double.IsFinite(rect.X + rect.Y + rect.Width + rect.Height) || rect.Width < 0 || rect.Height < 0)
            throw new ArgumentOutOfRangeException(nameof(rect));
        ScreenDetectionArea = rect;
    }

    public bool HitTest(Point point) => ScreenDetectionArea.Width > 0 && ScreenDetectionArea.Height > 0 && ScreenDetectionArea.Contains(point);
}
