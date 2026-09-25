using System.Runtime.InteropServices;

namespace UnoDock;
/// <summary>Optional physical-screen coordinate contract for desktop hosts. Screen points
/// use the host's global coordinate space: physical pixels on Windows/X11 and
/// AppKit screen points on macOS. Visual points are device-independent local units.</summary>
public interface IScreenWindowCoordinates : ICrossWindowCoordinates
{
    Point ToScreen(FrameworkElement source, Point point);
    Point FromScreen(Point screenPoint, FrameworkElement destination);
}
#if !WINDOWS
#endif
#if WINDOWS
#else
#endif
#if WINDOWS
#else
#endif
#if WINDOWS
#else
#endif
#if !WINDOWS
#endif
#if !WINDOWS
#endif
#if !WINDOWS
#endif
#if !WINDOWS
#endif
