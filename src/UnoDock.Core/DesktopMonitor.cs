namespace UnoDock.Core;
/// <summary>A monitor in physical (device pixel) desktop coordinates, its work
/// area in the same space and its own display scale (1 = 96 DPI).</summary>
public readonly record struct DesktopMonitor(DockRect Bounds, DockRect WorkArea, double Scale);
