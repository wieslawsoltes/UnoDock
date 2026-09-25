namespace UnoDock.Core;
/// <summary>Pure geometry shared by native client-coordinate translation and tab dragging.</summary>
public static class DockInteractionGeometry
{
    /// <param name = "clientOffsetPixels">Source client origin relative to the destination client origin, in physical pixels.</param>
    public static DockPoint TranslateClientPoint(DockPoint point, DockPoint clientOffsetPixels, double sourceScale, double destinationScale)
    {
        if (!double.IsFinite(point.X) || !double.IsFinite(point.Y))
            throw new ArgumentOutOfRangeException(nameof(point));
        if (!double.IsFinite(clientOffsetPixels.X) || !double.IsFinite(clientOffsetPixels.Y))
            throw new ArgumentOutOfRangeException(nameof(clientOffsetPixels));
        if (!double.IsFinite(sourceScale) || sourceScale <= 0)
            throw new ArgumentOutOfRangeException(nameof(sourceScale));
        if (!double.IsFinite(destinationScale) || destinationScale <= 0)
            throw new ArgumentOutOfRangeException(nameof(destinationScale));
        var result = new DockPoint((point.X * sourceScale + clientOffsetPixels.X) / destinationScale, (point.Y * sourceScale + clientOffsetPixels.Y) / destinationScale);
        if (!double.IsFinite(result.X) || !double.IsFinite(result.Y))
            throw new ArgumentOutOfRangeException(nameof(point), "Coordinate transform overflow.");
        return result;
    }

    /// <summary>Time-based, edge-accelerated scrolling. Returns a bounded offset delta, not a velocity.
        /// A stalled frame is capped to 50 ms; RTL reverses the logical scroll direction.</summary>
        public static double AutoScrollDelta(double pointerX, double viewportWidth, double offset, double maximumOffset, double elapsedSeconds, bool rightToLeft = false, double edgeWidth = 32, double maximumSpeed = 900)
    {
        if (!double.IsFinite(pointerX))
            throw new ArgumentOutOfRangeException(nameof(pointerX));
        if (!double.IsFinite(viewportWidth) || viewportWidth < 0)
            throw new ArgumentOutOfRangeException(nameof(viewportWidth));
        if (!double.IsFinite(offset) || offset < 0)
            throw new ArgumentOutOfRangeException(nameof(offset));
        if (!double.IsFinite(maximumOffset) || maximumOffset < 0)
            throw new ArgumentOutOfRangeException(nameof(maximumOffset));
        if (!double.IsFinite(elapsedSeconds) || elapsedSeconds < 0)
            throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
        if (!double.IsFinite(edgeWidth) || edgeWidth <= 0)
            throw new ArgumentOutOfRangeException(nameof(edgeWidth));
        if (!double.IsFinite(maximumSpeed) || maximumSpeed < 0)
            throw new ArgumentOutOfRangeException(nameof(maximumSpeed));
        if (viewportWidth == 0 || maximumOffset == 0 || pointerX < 0 || pointerX > viewportWidth)
            return 0;
        var edge = Math.Min(edgeWidth, viewportWidth / 2);
        var intensity = pointerX < edge ? -(1 - pointerX / edge) : pointerX > viewportWidth - edge ? 1 - (viewportWidth - pointerX) / edge : 0;
        var velocity = Math.CopySign(intensity * intensity, intensity) * maximumSpeed * (rightToLeft ? -1 : 1);
        var start = Math.Min(offset, maximumOffset);
        return Math.Clamp(start + velocity * Math.Min(elapsedSeconds, .05), 0, maximumOffset) - start;
    }
}
