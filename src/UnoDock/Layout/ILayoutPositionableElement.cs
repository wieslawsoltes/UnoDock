namespace UnoDock.Layout;
// Public portability contracts extend the original internal contracts without emulating WPF.
public interface ILayoutPositionableElement
{
    GridLength DockWidth { get; set; }
    GridLength DockHeight { get; set; }
    double DockMinWidth { get; set; }
    double DockMinHeight { get; set; }
    double FloatingLeft { get; set; }
    double FloatingTop { get; set; }
    double FloatingWidth { get; set; }
    double FloatingHeight { get; set; }
    bool IsMaximized { get; set; }
    bool CanRepositionItems { get; set; }
    bool AllowDuplicateContent { get; set; }
}
