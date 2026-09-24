using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation.Provider;
using UnoDock.Internal;
using Windows.System;

namespace UnoDock.Controls;

internal readonly record struct DockResizeRange(double Minimum, double Maximum, double Value, bool IsReadOnly)
{
    internal static DockResizeRange Unavailable => new(0, 0, 0, true);
}

public partial class LayoutGridResizerControl
{
    private readonly Border _keyboardFocus = new()
    {
        Name = "PART_SplitterKeyboardFocus", IsHitTestVisible = false,
        Visibility = Visibility.Collapsed, BorderThickness = new(1), Margin = new(1)
    };
    internal Func<DockResizeRange>? ReadAutomationRange { get; set; }
    internal Action<double>? WriteAutomationValue { get; set; }

    private void InitializeAutomation(Grid chrome)
    {
        chrome.Children.Add(_keyboardFocus);
        AutomationProperties.SetAccessibilityView(_thumb, AccessibilityView.Raw);
        // Tab traversal and a same-element pointer/keyboard transition can update
        // FocusState after GotFocus, or without another GotFocus event.
        RegisterPropertyChangedCallback(FocusStateProperty, (_, _) => PaintKeyboardFocus());
        GotFocus += (_, _) => PaintKeyboardFocus();
        LostFocus += (_, _) => PaintKeyboardFocus();
        SizeChanged += (_, _) => RefreshAutomation();
        Loaded += (_, _) => RefreshAutomation();
        Unloaded += (_, _) => { PaintKeyboardFocus(); RefreshAutomation(); };
        IsEnabledChanged += (_, _) => { PaintKeyboardFocus(); RefreshAutomation(); };
    }
    internal void ConfigureAutomation(DockPalette palette)
    {
        _keyboardFocus.BorderBrush = palette.Accent;
        PaintKeyboardFocus(); RefreshAutomation();
    }
    private void PaintKeyboardFocus() => _keyboardFocus.Visibility = IsLoaded && IsEnabled && FocusState == FocusState.Keyboard
        ? Visibility.Visible : Visibility.Collapsed;
    internal DockResizeRange AutomationRange
    {
        get
        {
            if (!DispatcherQueue.HasThreadAccess) throw new InvalidOperationException("Automation must run on the owning UI thread.");
            var range = ReadAutomationRange?.Invoke() ?? DockResizeRange.Unavailable;
            return range with { IsReadOnly = range.IsReadOnly || !IsEnabled || !IsLoaded || _dragging || _ending || _pendingCompletion != null };
        }
    }
    internal void SetAutomationValue(double value)
    {
        if (!double.IsFinite(value)) throw new ArgumentOutOfRangeException(nameof(value), "A finite pane size is required.");
        var range = AutomationRange;
        if (range.IsReadOnly || WriteAutomationValue == null) throw new InvalidOperationException("The splitter is not currently resizable.");
        if (value < range.Minimum || value > range.Maximum) throw new ArgumentOutOfRangeException(nameof(value), "The pane size is outside the current resize range.");
        if (value == range.Value) return;
        WriteAutomationValue(value);
        RefreshAutomation();
    }
    private bool ResizeBoundaryFromKey(VirtualKey key)
    {
        if (key is not (VirtualKey.Home or VirtualKey.End or VirtualKey.PageUp or VirtualKey.PageDown)) return false;
        var range = AutomationRange;
        if (range.IsReadOnly) return false;
        var value = key switch
        {
            VirtualKey.Home => range.Minimum,
            VirtualKey.End => range.Maximum,
            VirtualKey.PageUp => Math.Max(range.Minimum, range.Value - 50),
            _ => Math.Min(range.Maximum, range.Value + 50)
        };
        SetAutomationValue(value); return true;
    }
    internal void RefreshAutomation()
    {
        // No peer creation, global subscriptions or model enumeration when unused.
        if (FrameworkElementAutomationPeer.FromElement(this) is LayoutGridResizerAutomationPeer peer)
            peer.Synchronize();
    }
    protected override AutomationPeer OnCreateAutomationPeer() => new LayoutGridResizerAutomationPeer(this);
}

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
