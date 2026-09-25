using Microsoft.UI.Xaml.Input;
using UnoDock.Compatibility;
using UnoDock.Internal;
using UnoDock.Layout;

namespace UnoDock.Controls;
public class LayoutAnchorGroupControl : ContentControl, ILayoutControl
{
    private readonly LayoutAnchorGroup _model;
    private readonly StackPanel _panel = new() { Spacing = 2 };
    public LayoutAnchorGroupControl(LayoutAnchorGroup model) { _model = model; Content = _panel; }
    public ILayoutElement Model => _model;
    public ObservableCollection<LayoutAnchorControl> Children { get; } = [];
    internal void Update(DockingManager manager)
    {
        var models = _model.Children.ToArray();
        foreach (var stale in Children.Where(c => !models.Contains(c.Model)).ToArray()) Children.Remove(stale);
        for (var i = 0; i < models.Length; i++)
        {
            var child = Children.FirstOrDefault(c => ReferenceEquals(c.Model, models[i]));
            if (child == null) Children.Insert(Math.Min(i, Children.Count), child = new(models[i]));
            else if (Children.IndexOf(child) != i) Children.Move(Children.IndexOf(child), i);
            child.Update(manager);
        }
        _panel.Orientation = _model.GetSide() is AnchorSide.Left or AnchorSide.Right ? Orientation.Vertical : Orientation.Horizontal;
        VisualParenting.ReconcilePanel(_panel, Children.Cast<UIElement>().ToArray());
        if (manager.AnchorGroupTemplate != null) Template = manager.AnchorGroupTemplate;
    }
}
