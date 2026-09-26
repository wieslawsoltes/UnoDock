using UnoDock.Layout;

namespace UnoDock.Controls;

public abstract partial class LayoutGridControl<T>
{
    private sealed record ResizeAxisState(ILayoutPositionableElement? Model, GridLength Length, double Minimum, double Pixels, long ParentVersion);
    private ResizeAxisState[] CaptureResizeAxis(bool horizontal)
    {
        var result = new ResizeAxisState[_displayed.Length];
        for (var i = 0; i < result.Length; i++)
        {
            var model = _displayed[i] as ILayoutPositionableElement;
            result[i] = new(model, horizontal ? model?.DockWidth ?? new(1, GridUnitType.Star) : model?.DockHeight ?? new(1, GridUnitType.Star), horizontal ? model?.DockMinWidth ?? 0 : model?.DockMinHeight ?? 0, horizontal ? ColumnDefinitions[i * 2].ActualWidth : RowDefinitions[i * 2].ActualHeight, (_displayed[i] as LayoutElement)?.ParentVersion ?? 0);
        }

        return result;
    }

    private bool AreResizePeersCurrent(ResizeSession session, bool checkPixels)
    {
        for (var i = 0; i < session.Axis.Length; i++)
        {
            var state = session.Axis[i];
            if (((_displayed[i] as LayoutElement)?.ParentVersion ?? 0) != state.ParentVersion)
            {
                return false;
            }

            if (state.Model is { } model)
            {
                var minimum = session.Horizontal ? model.DockMinWidth : model.DockMinHeight;
                var length = session.Horizontal ? model.DockWidth : model.DockHeight;
                // The pair is written sequentially while publishing. Its own
                // values are checked by the transaction, not as immutable peers.
                if (minimum != state.Minimum || i != session.Index && i != session.Index + 1 && length != state.Length)
                {
                    return false;
                }
            }

            if (checkPixels)
            {
                var pixels = session.Horizontal ? ColumnDefinitions[i * 2].ActualWidth : RowDefinitions[i * 2].ActualHeight;
                if (pixels != state.Pixels)
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static (GridLength Before, GridLength After) CalculateResizeLengths(ResizeSession session, double before, double after)
    {
        var a = session.BeforeLength;
        var b = session.AfterLength;
        var totalPixels = session.BeforePixels + session.AfterPixels;
        var bothStars = a.IsStar && b.IsStar;
        var weightSum = a.Value + b.Value;
        // An unconstrained pair retains the original reference-observed
        // weight-plus-displacement rule, avoiding jumps caused by pixel rounding.
        // A minimum-constrained pair no longer represents those weight ratios.
        var constrained = bothStars && (weightSum <= 0 || Math.Abs(session.BeforePixels - totalPixels * (a.Value / weightSum)) > 1.1);
        var otherStars = session.Axis.Where((state, index) => index != session.Index && index != session.Index + 1 && state.Length.IsStar && state.Pixels > 0).ToArray();
        if ((a.IsStar || b.IsStar) && otherStars.Length > 0 && (!bothStars || constrained))
        {
            // One star is not a fraction of this pair: its weight competes with
            // every other star in the grid. Calibrate against an unaffected,
            // unconstrained sibling so the requested pixels and those siblings'
            // existing weights can both remain valid after the resize.
            var reference = otherStars.Where(state => state.Length.Value > 0 && state.Pixels > state.Minimum + 1.1).OrderByDescending(state => state.Pixels).FirstOrDefault();
            var scale = reference != null ? reference.Length.Value / reference.Pixels : Math.Max(1 / totalPixels, otherStars.Max(state => state.Length.Value / state.Pixels));
            return (a.IsStar ? Star(before * scale) : new(before), b.IsStar ? Star(after * scale) : new(after));
        }

        if (constrained)
        {
            // With no free outside star, the pair owns the remaining space.
            // Rebase only this pair, including a formerly zero-weight endpoint.
            var total = weightSum > 0 ? weightSum : 1;
            return (Star(total * (before / totalPixels)), Star(total * (after / totalPixels)));
        }

        var delta = bothStars ? session.AbsolutePixels ? weightSum * (before / totalPixels) - a.Value : weightSum * (session.Displacement / totalPixels) : 0;
        return (a.IsStar ? Star(bothStars ? Math.Max(0, a.Value + delta) : before / totalPixels) : new(before), b.IsStar ? Star(bothStars ? Math.Max(0, b.Value - delta) : after / totalPixels) : new(after));
        static GridLength Star(double value)
        {
            if (!double.IsFinite(value) || value < 0)
            {
                throw new InvalidOperationException("The resize would produce an unrepresentable star weight.");
            }

            return new(value, GridUnitType.Star);
        }
    }
}
