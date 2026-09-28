namespace UnoDock.Internal;
// Keep brush references in a separately traced immutable object, not in a large
// reference-bearing value embedded in retained controls. The browser fault
// capture found cleared nursery objects through the embedded palette's slots.
// Record equality and with-expressions retain the existing palette contract.
internal sealed record DockPalette(Brush Surface, Brush Header, Brush Tab, Brush Border, Brush Foreground, Brush Hover, Brush Pressed, Brush Accent, Brush ActiveTitle, double FontSize, double TitleHeight, double TabHeight, double ToolTabHeight, double RailThickness, double ButtonCornerRadius = 0, double ChromeButtonSize = 16, double ActiveTabIndicatorThickness = 0, bool UsesFluentControls = false, Brush? SecondaryForeground = null, Brush? DisabledForeground = null, double TabCornerRadius = 0, double TabHorizontalPadding = 0, double PaneCornerRadius = 0);
