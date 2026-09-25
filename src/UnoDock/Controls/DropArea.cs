using UnoDock.Internal;
using UnoDock.Layout;

namespace UnoDock.Controls;
/// <summary>A measured area in the coordinate space of the supplied reference visual.
/// Refresh after arrange or DPI changes. Detached or collapsed visuals have empty bounds.</summary>
public class DropArea<T> : IDropArea, IModelDropArea where T : FrameworkElement
{
    private readonly FrameworkElement _relativeTo;
    public DropArea(T areaElement, DropAreaType type) : this(areaElement, type, areaElement?.FindVisualTreeRoot() as FrameworkElement ?? areaElement!)
    {
    }

    public DropArea(T areaElement, DropAreaType type, FrameworkElement relativeTo)
    {
        ArgumentNullException.ThrowIfNull(areaElement);
        ArgumentNullException.ThrowIfNull(relativeTo);
        if (!Enum.IsDefined(type))
            throw new ArgumentOutOfRangeException(nameof(type));
        AreaElement = areaElement;
        Type = type;
        _relativeTo = relativeTo;
        Refresh();
    }

    public T AreaElement
    {
        get;
    }
    public Rect DetectionRect
    {
        get;
        private set;
    }
    public DropAreaType Type
    {
        get;
    }

    ILayoutElement? IModelDropArea.Model => AreaElement is DockingManager manager ? manager.Layout.RootPanel : (AreaElement as ILayoutControl)?.Model;

    DockingManager? IModelDropArea.Manager => AreaElement as DockingManager ?? (AreaElement as ILayoutControl)?.Model?.Root?.Manager;

    public void Refresh()
    {
        DetectionRect = default;
        if (AreaElement.XamlRoot == null || _relativeTo.XamlRoot == null || AreaElement.Visibility != Visibility.Visible || AreaElement.ActualWidth <= 0 || AreaElement.ActualHeight <= 0)
            return;
        var converter = ((IModelDropArea)this).Manager?.CrossWindowCoordinates;
        try
        {
            DetectionRect = DockCoordinates.Bounds(AreaElement, new Rect(0, 0, AreaElement.ActualWidth, AreaElement.ActualHeight), _relativeTo, converter);
        }
        catch (Exception e) when (DockCoordinates.IsUnavailable(e))
        {
            DetectionRect = default;
        }
    }
}
