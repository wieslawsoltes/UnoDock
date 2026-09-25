using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation.Provider;
using UnoDock.Internal;
using Windows.System;

namespace UnoDock.Controls;

/// <summary>Publishes the preceding logical pane's arranged size in DIPs, excluding
/// the divider. SetValue shares the keyboard resize transaction and its ownership
/// checks; pointer previews are read-only and never expose uncommitted model sizes.</summary>
public sealed class LayoutGridResizerAutomationPeer(LayoutGridResizerControl owner)
    : FrameworkElementAutomationPeer(owner), IRangeValueProvider
{
    private DockResizeRange? _last = owner.AutomationRange;
    protected override string GetClassNameCore() => nameof(LayoutGridResizerControl);
    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Thumb;
    protected override AutomationOrientation GetOrientationCore() => owner.Horizontal ? AutomationOrientation.Horizontal : AutomationOrientation.Vertical;
    protected override object? GetPatternCore(PatternInterface patternInterface) => patternInterface == PatternInterface.RangeValue ? this : base.GetPatternCore(patternInterface);
    protected override IList<AutomationPeer> GetChildrenCore() => [];
    protected override bool IsKeyboardFocusableCore() => !IsReadOnly && owner.IsTabStop;
    protected override void SetFocusCore()
    {
        if (IsReadOnly || !owner.Focus(FocusState.Keyboard)) throw new InvalidOperationException("The splitter cannot receive keyboard focus.");
    }
    public bool IsReadOnly => owner.AutomationRange.IsReadOnly;
    public double Minimum => owner.AutomationRange.Minimum;
    public double Maximum => owner.AutomationRange.Maximum;
    public double Value => owner.AutomationRange.Value;
    public double SmallChange => 10;
    public double LargeChange => 50;
    public void SetValue(double value) => owner.SetAutomationValue(value);
    internal void Synchronize()
    {
        var next = owner.AutomationRange; var old = _last; _last = next;
        if (old is not { } previous) return;
        if (previous.Minimum != next.Minimum) RaisePropertyChangedEvent(RangeValuePatternIdentifiers.MinimumProperty, previous.Minimum, next.Minimum);
        if (previous.Maximum != next.Maximum) RaisePropertyChangedEvent(RangeValuePatternIdentifiers.MaximumProperty, previous.Maximum, next.Maximum);
        if (previous.Value != next.Value) RaisePropertyChangedEvent(RangeValuePatternIdentifiers.ValueProperty, previous.Value, next.Value);
        if (previous.IsReadOnly != next.IsReadOnly) RaisePropertyChangedEvent(RangeValuePatternIdentifiers.IsReadOnlyProperty, previous.IsReadOnly, next.IsReadOnly);
    }
}
