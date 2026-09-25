namespace UnoDock;

/// <summary>Extension point for hosts with embedded content islands or custom non-client geometry.</summary>
public interface ICrossWindowCoordinates
{
    Point Translate(FrameworkElement source, Point sourcePoint, FrameworkElement destination);
}

#if WINDOWS
#else
#endif
