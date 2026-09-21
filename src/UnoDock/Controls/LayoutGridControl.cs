using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Input;
using Xceed.Wpf.AvalonDock.Internal;
using Xceed.Wpf.AvalonDock.Layout;

namespace Xceed.Wpf.AvalonDock.Controls;

public abstract class LayoutGridControl<T> : Grid, ILayoutControl, IRefreshableLayoutControl where T : class, ILayoutPanelElement
{
    private readonly ILayoutOrientableGroup _group;
    private ILayoutPanelElement[] _displayed = [];
    private Orientation _lastOrientation;
    private double _lastThickness = -1;
    protected LayoutGridControl(ILayoutOrientableGroup model) => _group = model;
    public ILayoutElement Model => _group;
    public Orientation Orientation => _group.Orientation;
    protected void FixChildrenDockLengths() => OnFixChildrenDockLengths();
    protected abstract void OnFixChildrenDockLengths();
    void IRefreshableLayoutControl.Update(DockSurface surface) => Update(surface);
    internal void Update(DockSurface surface)
    {
        var models = _group.Children.OfType<ILayoutPanelElement>().Where(c => c.IsVisible).ToArray();
        var horizontal = Orientation == Orientation.Horizontal;
        var thickness = Math.Clamp(horizontal ? surface.Manager.GridSplitterWidth : surface.Manager.GridSplitterHeight, 1, 64);
        if (!_displayed.SequenceEqual(models, ReferenceEqualityComparer.Instance) || _lastOrientation != Orientation || thickness != _lastThickness)
        {
            foreach (var view in Children.ToArray()) if (view is not LayoutGridResizerControl) VisualParenting.Detach(view);
            Children.Clear(); ColumnDefinitions.Clear(); RowDefinitions.Clear();
            _displayed = models; _lastOrientation = Orientation; _lastThickness = thickness;
            for (var i = 0; i < models.Length; i++)
            {
                var view = surface.GetView(models[i]); VisualParenting.Detach(view);
                if (horizontal) ColumnDefinitions.Add(new()); else RowDefinitions.Add(new());
                SetColumn(view, horizontal ? i * 2 : 0); SetRow(view, horizontal ? 0 : i * 2); Children.Add(view);
                if (i + 1 < models.Length)
                {
                    if (horizontal) ColumnDefinitions.Add(new() { Width = new(thickness) }); else RowDefinitions.Add(new() { Height = new(thickness) });
                    var index = i;
                    var resize = new LayoutGridResizerControl { Horizontal = horizontal, Background = DockVisuals.Brush(surface.Manager, "UnoDock.HeaderBrush", "ControlFillColorSecondaryBrush") };
                    resize.ResizeBy += (_, delta) => Resize(index, delta);
                    SetColumn(resize, horizontal ? i * 2 + 1 : 0); SetRow(resize, horizontal ? 0 : i * 2 + 1); Children.Add(resize);
                }
            }
        }
        for (var i = 0; i < models.Length; i++)
        {
            var pos = models[i] as ILayoutPositionableElement;
            if (horizontal) { ColumnDefinitions[i * 2].Width = pos?.DockWidth ?? new(1, GridUnitType.Star); ColumnDefinitions[i * 2].MinWidth = pos?.DockMinWidth ?? 0; }
            else { RowDefinitions[i * 2].Height = pos?.DockHeight ?? new(1, GridUnitType.Star); RowDefinitions[i * 2].MinHeight = pos?.DockMinHeight ?? 0; }
            surface.UpdateView(models[i]);
        }
    }
    private void Resize(int index, double delta)
    {
        if (index < 0 || index + 1 >= _displayed.Length || _displayed[index] is not ILayoutPositionableElement a || _displayed[index + 1] is not ILayoutPositionableElement b) return;
        var horizontal = Orientation == Orientation.Horizontal;
        var first = horizontal ? ColumnDefinitions[index * 2].ActualWidth : RowDefinitions[index * 2].ActualHeight;
        var second = horizontal ? ColumnDefinitions[index * 2 + 2].ActualWidth : RowDefinitions[index * 2 + 2].ActualHeight;
        var pair = DockSplitSolver.ResizePair(first, second, delta, horizontal ? a.DockMinWidth : a.DockMinHeight, horizontal ? b.DockMinWidth : b.DockMinHeight);
        var lengthA = horizontal ? a.DockWidth : a.DockHeight; var lengthB = horizontal ? b.DockWidth : b.DockHeight;
        var stars = lengthA.IsStar && lengthB.IsStar; var weight = lengthA.Value + lengthB.Value;
        GridLength Length(double value) => stars ? new(weight * value / Math.Max(1, pair.Before + pair.After), GridUnitType.Star) : new(value);
        using var batch = (_group.Root as LayoutRoot)?.BeginUpdate();
        if (horizontal) { a.DockWidth = Length(pair.Before); b.DockWidth = Length(pair.After); }
        else { a.DockHeight = Length(pair.Before); b.DockHeight = Length(pair.After); }
    }
    protected void NormalizeLengths()
    {
        foreach (var child in _group.Children.OfType<ILayoutPositionableElement>())
        {
            var value = Orientation == Orientation.Horizontal ? child.DockWidth : child.DockHeight;
            if (!value.IsStar || value.Value > 0) continue;
            if (Orientation == Orientation.Horizontal) child.DockWidth = new(1, GridUnitType.Star); else child.DockHeight = new(1, GridUnitType.Star);
        }
    }
}
public class LayoutPanelControl(LayoutPanel model) : LayoutGridControl<ILayoutPanelElement>(model)
{ protected override void OnFixChildrenDockLengths() => NormalizeLengths(); }
public class LayoutDocumentPaneGroupControl(LayoutDocumentPaneGroup model) : LayoutGridControl<ILayoutDocumentPane>(model)
{ protected override void OnFixChildrenDockLengths() => NormalizeLengths(); }
public class LayoutAnchorablePaneGroupControl(LayoutAnchorablePaneGroup model) : LayoutGridControl<ILayoutAnchorablePane>(model)
{ protected override void OnFixChildrenDockLengths() => NormalizeLengths(); }

public class LayoutGridResizerControl : ContentControl
{
    internal bool Horizontal { get; set; }
    internal event EventHandler<double>? ResizeBy;
    public LayoutGridResizerControl()
    {
        IsTabStop = true; AutomationProperties.SetName(this, "Resize docked panes");
        var thumb = new Thumb { HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch };
        HorizontalContentAlignment = HorizontalAlignment.Stretch; VerticalContentAlignment = VerticalAlignment.Stretch; Content = thumb;
        thumb.DragDelta += (_, e) => ResizeBy?.Invoke(this, Horizontal ? e.HorizontalChange : e.VerticalChange);
    }
    protected override void OnKeyDown(KeyRoutedEventArgs e)
    {
        base.OnKeyDown(e);
        var delta = e.Key switch { Windows.System.VirtualKey.Left or Windows.System.VirtualKey.Up => -10, Windows.System.VirtualKey.Right or Windows.System.VirtualKey.Down => 10, _ => 0 };
        if (delta != 0) { ResizeBy?.Invoke(this, delta); e.Handled = true; }
    }
}

/// <summary>Natural-width tab panel: overflow is handled by its containing ScrollViewer.</summary>
public class DocumentPaneTabPanel : Panel
{
    protected override Size MeasureOverride(Size availableSize)
    {
        double width = 0, height = 0;
        foreach (var child in Children) { child.Measure(new(double.PositiveInfinity, availableSize.Height)); width += child.DesiredSize.Width; height = Math.Max(height, child.DesiredSize.Height); }
        return new(width, height);
    }
    protected override Size ArrangeOverride(Size finalSize)
    {
        double x = 0;
        foreach (var child in Children) { child.Arrange(new Rect(x, 0, child.DesiredSize.Width, finalSize.Height)); x += child.DesiredSize.Width; }
        return finalSize;
    }
}
public class AnchorablePaneTabPanel : DocumentPaneTabPanel { }
