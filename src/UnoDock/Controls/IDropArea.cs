using UnoDock.Internal;
using UnoDock.Layout;

namespace UnoDock.Controls;
public interface IDropArea
{
    Rect DetectionRect { get; }

    DropAreaType Type { get; }
}
