using Microsoft.UI.Xaml.Input;
using Windows.UI;
using Path = Microsoft.UI.Xaml.Shapes.Path;

namespace UnoDock.Internal;
// Independent compact chrome. Geometry and layout follow public visual observations,
// not the original resource dictionaries, templates or vector assets.
internal readonly record struct DockPalette(Brush Surface, Brush Header, Brush Tab, Brush Border, Brush Foreground, Brush Hover, Brush Pressed, Brush Accent, Brush ActiveTitle, double FontSize, double TitleHeight, double TabHeight, double ToolTabHeight, double RailThickness, double CornerRadius = 0, double ButtonSize = 16);
