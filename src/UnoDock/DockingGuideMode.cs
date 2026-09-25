using UnoDock.Controls;
using UnoDock.Layout;

namespace UnoDock;
/// <summary>Additive input policy. The default GuidesOnly mode requires a displayed glyph
/// or a visible tab/caption insertion surface. GuidesAndEdges retains preview-8 edge zones.</summary>
public enum DockingGuideMode
{
    GuidesAndEdges,
    GuidesOnly,
    EdgesOnly
}
