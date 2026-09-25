namespace UnoDock.Core;

public readonly record struct DockRect(double X, double Y, double Width, double Height)
{
    public double Right => X + Width;
    public double Bottom => Y + Height;

    public bool Contains(DockPoint p) => Width >= 0 && Height >= 0 && p.X >= X && p.X <= Right && p.Y >= Y && p.Y <= Bottom;
    public DockRect ClampTo(DockRect workArea, double minimumVisible = 32)
    {
        if (!double.IsFinite(X + Y + Width + Height + workArea.X + workArea.Y + workArea.Width + workArea.Height + minimumVisible) || workArea.Width < 0 || workArea.Height < 0 || minimumVisible < 0)
            throw new ArgumentOutOfRangeException(nameof(workArea));
        var w = Math.Clamp(Width, Math.Min(96, workArea.Width), Math.Max(96, workArea.Width));
        var h = Math.Clamp(Height, Math.Min(64, workArea.Height), Math.Max(64, workArea.Height));
        return new(Math.Clamp(X, workArea.X - w + minimumVisible, Math.Max(workArea.X - w + minimumVisible, workArea.Right - minimumVisible)), Math.Clamp(Y, workArea.Y, Math.Max(workArea.Y, workArea.Bottom - minimumVisible)), w, h);
    }
}
