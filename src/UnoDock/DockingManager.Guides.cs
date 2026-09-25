using UnoDock.Controls;
using UnoDock.Layout;

namespace UnoDock;

public partial class DockingManager
{
    public static readonly DependencyProperty DockingGuideModeProperty = DependencyProperty.Register(
        nameof(DockingGuideMode), typeof(DockingGuideMode), typeof(DockingManager),
        new PropertyMetadata(DockingGuideMode.GuidesOnly, OnDockingGuideModeChanged));
    private static void OnDockingGuideModeChanged(DependencyObject owner, DependencyPropertyChangedEventArgs args)
    {
        var manager = (DockingManager)owner;
        if (args.NewValue is not DockingGuideMode value || !Enum.IsDefined(value))
        {
            manager.SetValue(DockingGuideModeProperty, args.OldValue);
            throw new ArgumentOutOfRangeException(nameof(DockingGuideMode));
        }
        manager._surface?.CancelDrag();
    }
    public DockingGuideMode DockingGuideMode
    {
        get => (DockingGuideMode)GetValue(DockingGuideModeProperty);
        set { if (!Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(nameof(value)); SetValue(DockingGuideModeProperty, value); }
    }
    /// <summary>Additive extended targets, off in the observed stock profile. Enables
    /// four extra tool-as-tool intents around a document compass.</summary>
    public static readonly DependencyProperty ShowDocumentPaneToolGuidesProperty = DependencyProperty.Register(
        nameof(ShowDocumentPaneToolGuides), typeof(bool), typeof(DockingManager),
        new PropertyMetadata(false, (d, _) => ((DockingManager)d)._surface?.CancelDrag()));
    public bool ShowDocumentPaneToolGuides
    {
        get => (bool)GetValue(ShowDocumentPaneToolGuidesProperty);
        set => SetValue(ShowDocumentPaneToolGuidesProperty, value);
    }
    /// <summary>Live validated glyphs in the same surface coordinate space as GetDropPlan.
    /// A snapshot is for immediate use; Plan.CanExecute rechecks ownership and policy.</summary>
    public IReadOnlyList<DockGuideTarget> GetDockingGuides(LayoutContent content, Point surfacePoint)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (!double.IsFinite(surfacePoint.X) || !double.IsFinite(surfacePoint.Y)) throw new ArgumentOutOfRangeException(nameof(surfacePoint));
        return _surface?.GetDockingGuides(content, surfacePoint) ?? [];
    }
}
