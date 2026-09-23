using UnoDock.Internal;
using UnoDock.Layout;

namespace UnoDock.Controls;

/// <summary>Non-activating guide and preview layer. It never captures input or focus.
/// All hit rectangles are shared with the validated docking intents.</summary>
public class OverlayWindow : DockWindowControl
{
    private readonly Canvas _defaultCanvas = new();
    private Canvas _canvas;
    private readonly Border _fill = new() { Opacity = .25, IsHitTestVisible = false };
    private readonly Border _preview = new() { BorderThickness = new(1), IsHitTestVisible = false };
    private readonly Dictionary<(ILayoutGroup Target, DropTargetType Type), DockGuideVisual> _guideViews = [];
    private readonly Dictionary<ILayoutGroup, DockGuideBackplate> _plates = new(ReferenceEqualityComparer.Instance);
    private IReadOnlyList<DockGuideTarget> _guides = Array.Empty<DockGuideTarget>();
    public DockDropPlan? CurrentPlan { get; private set; }
    public IReadOnlyList<DockGuideTarget> DisplayedGuides => _guides;
    public bool IsOpen => Visibility == Visibility.Visible && (CurrentPlan?.CanExecute == true || _guides.Any(g => g.Plan.CanExecute));
    public OverlayWindow()
    {
        _canvas = _defaultCanvas;
        IsHitTestVisible = false; IsTabStop = false; Visibility = Visibility.Collapsed;
        HorizontalContentAlignment = HorizontalAlignment.Stretch; VerticalContentAlignment = VerticalAlignment.Stretch;
        _canvas.Children.Add(_fill); _canvas.Children.Add(_preview); Content = _defaultCanvas;
        Unloaded += (_, _) => Hide();
    }
    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        var canvas = GetTemplateChild("PART_DockingGuideCanvas") as Canvas ?? _defaultCanvas;
        if (ReferenceEquals(canvas, _canvas)) return;
        foreach (var child in _canvas.Children.ToArray()) { _canvas.Children.Remove(child); canvas.Children.Add(child); }
        _canvas = canvas;
    }
    public void ShowPreview(DockDropPlan? plan, Brush accent) => ShowPreview(plan, plan?.PreviewRect ?? default, accent);
    internal void ShowPreview(DockDropPlan? plan, Rect rect, Brush accent)
    {
        ArgumentNullException.ThrowIfNull(accent);
        Hide();
        if (plan?.CanExecute != true || !Valid(rect)) return;
        CurrentPlan = plan; PaintPreview(rect, accent); Visibility = Visibility.Visible;
    }
    public void ShowGuides(IReadOnlyList<DockGuideTarget> guides, DockDropPlan? selectedPlan, DockingManager manager)
        => ShowGuides(guides, selectedPlan, manager, null);
    internal void ShowGuides(IReadOnlyList<DockGuideTarget> guides, DockDropPlan? selectedPlan, DockingManager manager, Rect? projectedPreview)
    {
        ArgumentNullException.ThrowIfNull(guides); ArgumentNullException.ThrowIfNull(manager);
        var valid = guides.Where(g => g != null && g.Plan.CanExecute && ReferenceEquals(g.Plan.Target.Root?.Manager, manager)).ToArray();
        var keys = new HashSet<(ILayoutGroup, DropTargetType)>();
        foreach (var g in valid) if (!keys.Add((g.Plan.Target, g.Type))) throw new ArgumentException("Duplicate docking guide identity.", nameof(guides));
        var selected = selectedPlan?.CanExecute == true && ReferenceEquals(selectedPlan.Target.Root?.Manager, manager) ? selectedPlan : null;
        var p = DockGuidePalette.Resolve(manager, DockChrome.Palette(manager));
        var plateBounds = new Dictionary<ILayoutGroup, Rect>(ReferenceEqualityComparer.Instance);
        foreach (var group in valid.Where(g => g.Type is >= DropTargetType.DocumentPaneDockLeft and <= DropTargetType.DocumentPaneDockInside or
            >= DropTargetType.AnchorablePaneDockLeft and <= DropTargetType.AnchorablePaneDockInside).GroupBy(g => g.Plan.Target))
        {
            if (group.Count() != 5) continue;
            var center = group.Single(g => g.Plan.Position == DockPosition.Inside).DetectionRect;
            var left = group.Single(g => g.Plan.Position == DockPosition.Left).DetectionRect;
            var top = group.Single(g => g.Plan.Position == DockPosition.Top).DetectionRect;
            var right = group.Single(g => g.Plan.Position == DockPosition.Right).DetectionRect;
            var bottom = group.Single(g => g.Plan.Position == DockPosition.Bottom).DetectionRect;
            // Do not paint a connected stock cross over arbitrary custom, separated hit regions.
            if (Math.Abs(left.Right - center.Left) > .01 || Math.Abs(top.Bottom - center.Top) > .01 ||
                Math.Abs(center.Right - right.Left) > .01 || Math.Abs(center.Bottom - bottom.Top) > .01) continue;
            plateBounds.Add(group.Key, new(left.Left, top.Top, right.Right - left.Left, bottom.Bottom - top.Top));
        }
        foreach (var key in _plates.Keys.Where(k => !plateBounds.ContainsKey(k)).ToArray())
        { _canvas.Children.Remove(_plates[key]); _plates.Remove(key); }
        foreach (var pair in plateBounds)
        {
            if (!_plates.TryGetValue(pair.Key, out var plate))
            { plate = new(); _plates.Add(pair.Key, plate); _canvas.Children.Add(plate); }
            Canvas.SetLeft(plate, pair.Value.X); Canvas.SetTop(plate, pair.Value.Y); plate.Width = pair.Value.Width; plate.Height = pair.Value.Height;
            plate.Paint(p);
        }
        foreach (var key in _guideViews.Keys.Where(k => !keys.Contains(k)).ToArray())
        { _canvas.Children.Remove(_guideViews[key]); _guideViews.Remove(key); }
        foreach (var guide in valid)
        {
            var key = (guide.Plan.Target, guide.Type);
            if (!_guideViews.TryGetValue(key, out var visual))
            { visual = new DockGuideVisual(guide.Type, guide.Plan.Position); _guideViews.Add(key, visual); _canvas.Children.Add(visual); }
            var bounds = guide.DetectionRect;
            Canvas.SetLeft(visual, bounds.X); Canvas.SetTop(visual, bounds.Y);
            visual.Width = bounds.Width; visual.Height = bounds.Height;
            visual.Paint(p, selected != null && ReferenceEquals(selected.Target, guide.Plan.Target) && selected.Type == guide.Type,
                _plates.ContainsKey(guide.Plan.Target) && (guide.Type is >= DropTargetType.DocumentPaneDockLeft and <= DropTargetType.DocumentPaneDockInside or
                    >= DropTargetType.AnchorablePaneDockLeft and <= DropTargetType.AnchorablePaneDockInside));
        }
        _guides = Array.AsReadOnly(valid); CurrentPlan = selected;
        var preview = projectedPreview ?? selected?.PreviewRect ?? default;
        if (selected != null && Valid(preview)) PaintPreview(preview, p.Ink);
        else { _preview.Visibility = _fill.Visibility = Visibility.Collapsed; }
        Visibility = selected != null || valid.Length != 0 ? Visibility.Visible : Visibility.Collapsed;
    }
    private static bool Valid(Rect rect) => rect.Width > 0 && rect.Height > 0 && double.IsFinite(rect.X) && double.IsFinite(rect.Y) && double.IsFinite(rect.Right) && double.IsFinite(rect.Bottom);
    private void PaintPreview(Rect rect, Brush accent)
    {
        foreach (var visual in new[] { _fill, _preview })
        {
            Canvas.SetLeft(visual, rect.X); Canvas.SetTop(visual, rect.Y);
            visual.Width = rect.Width; visual.Height = rect.Height; visual.Visibility = Visibility.Visible;
        }
        _fill.Background = accent; _preview.BorderBrush = accent;
    }
    public void Hide()
    {
        CurrentPlan = null; _guides = Array.Empty<DockGuideTarget>(); Visibility = Visibility.Collapsed;
        foreach (var view in _guideViews.Values) _canvas.Children.Remove(view);
        _guideViews.Clear();
        foreach (var plate in _plates.Values) _canvas.Children.Remove(plate); _plates.Clear();
        _fill.Visibility = _preview.Visibility = Visibility.Collapsed;
    }
    public void Close() { var args = new CancelEventArgs(); OnClosing(args); if (!args.Cancel) Hide(); }
    protected override void OnClosing(CancelEventArgs e) => base.OnClosing(e);
}
