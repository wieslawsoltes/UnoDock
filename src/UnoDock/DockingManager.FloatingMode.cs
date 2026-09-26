namespace UnoDock;

public partial class DockingManager
{
    private bool _restoringFloatingMode;
    private void ChangeFloatingWindowMode(DependencyPropertyChangedEventArgs args)
    {
        if (_restoringFloatingMode)
            return;
        if (!Enum.IsDefined((FloatingWindowMode)args.NewValue))
        {
            _restoringFloatingMode = true;
            try
            {
                SetValue(FloatingWindowModeProperty, args.OldValue);
            }
            finally
            {
                _restoringFloatingMode = false;
            }

            throw new ArgumentOutOfRangeException(nameof(FloatingWindowMode));
        }

        InvalidateView();
    }
}
