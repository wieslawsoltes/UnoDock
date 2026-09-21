namespace Xceed.Wpf.AvalonDock;

/// <summary>Extension point for hosts with embedded content islands or custom non-client geometry.</summary>
public interface ICrossWindowCoordinates
{
    Point Translate(FrameworkElement source, Point sourcePoint, FrameworkElement destination);
}
/// <summary>Uses the framework's screen conversion API, including per-window rasterization scales.</summary>
public sealed class ContentIslandCoordinates : ICrossWindowCoordinates
{
    public Point Translate(FrameworkElement source, Point sourcePoint, FrameworkElement destination)
    {
        ArgumentNullException.ThrowIfNull(source); ArgumentNullException.ThrowIfNull(destination);
        if (source.XamlRoot == null || destination.XamlRoot == null) throw new InvalidOperationException("Both visual roots must be loaded.");
        if (ReferenceEquals(source.XamlRoot, destination.XamlRoot)) return source.TransformToVisual(destination).TransformPoint(sourcePoint);
#if WINDOWS
        var from = Microsoft.UI.Content.ContentCoordinateConverter.CreateForWindowId(source.XamlRoot.ContentIslandEnvironment.AppWindowId);
        var to = Microsoft.UI.Content.ContentCoordinateConverter.CreateForWindowId(destination.XamlRoot.ContentIslandEnvironment.AppWindowId);
        var rootPoint = source.TransformToVisual(source.XamlRoot.Content).TransformPoint(sourcePoint);
        var screen = from.ConvertLocalToScreen(rootPoint);
        var targetRootPoint = to.ConvertScreenToLocal(screen);
        return destination.XamlRoot.Content.TransformToVisual(destination).TransformPoint(targetRootPoint);
#else
        throw new PlatformNotSupportedException("This host requires an ICrossWindowCoordinates implementation. ContentCoordinateConverter is unavailable on Uno Skia.");
#endif
    }
}
