"""Add validated floating-mode writes without changing native host policy."""
from pathlib import Path

path = Path('src/UnoDock/DockingManager.cs')
text = path.read_text()
old = 'new PropertyMetadata(FloatingWindowMode.Auto, (owner, _) => ((DockingManager)owner).InvalidateView())'
assert text.count(old) == 1
text = text.replace(old, 'new PropertyMetadata(FloatingWindowMode.Auto, (owner, args) => ((DockingManager)owner).ChangeFloatingWindowMode(args))')
old = '        set => SetValue(FloatingWindowModeProperty, value);'
assert text.count(old) == 1
text = text.replace(old, '''        set
        {
            if (!Enum.IsDefined(value))
                throw new ArgumentOutOfRangeException(nameof(value));
            SetValue(FloatingWindowModeProperty, value);
        }''')
path.write_text(text)
Path('src/UnoDock/DockingManager.FloatingMode.cs').write_text('''namespace UnoDock;

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
''')
