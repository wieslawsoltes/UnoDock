namespace UnoDock;
public partial class DockingManager
{
    private bool _revertingFloatingTitleBar;
    public static readonly DependencyProperty FloatingWindowTitleBarModeProperty = DependencyProperty.Register(nameof(FloatingWindowTitleBarMode), typeof(FloatingWindowTitleBarMode), typeof(DockingManager), new PropertyMetadata(FloatingWindowTitleBarMode.Custom, (d, e) => ((DockingManager)d).FloatingTitleBarChanged(e)));
    /// <summary>Custom replaces OS decorations with the retained, theme-aware UnoDock
    /// caption. System opts back into the desktop host's original decorations.</summary>
    public FloatingWindowTitleBarMode FloatingWindowTitleBarMode
    {
        get => (FloatingWindowTitleBarMode)GetValue(FloatingWindowTitleBarModeProperty);
        set
        {
            if (!Enum.IsDefined(value))
                throw new ArgumentOutOfRangeException(nameof(value));
            SetValue(FloatingWindowTitleBarModeProperty, value);
        }
    }

    private void FloatingTitleBarChanged(DependencyPropertyChangedEventArgs e)
    {
        if (_revertingFloatingTitleBar)
            return;
        if (!Enum.IsDefined((FloatingWindowTitleBarMode)e.NewValue))
        {
            _revertingFloatingTitleBar = true;
            try
            {
                SetValue(FloatingWindowTitleBarModeProperty, e.OldValue);
            }
            finally
            {
                _revertingFloatingTitleBar = false;
            }

            throw new ArgumentOutOfRangeException(nameof(FloatingWindowTitleBarMode));
        }

        Surface?.CancelDrag();
        foreach (var window in FloatingWindows.ToArray())
            window.RefreshNativeChrome();
    }
}
