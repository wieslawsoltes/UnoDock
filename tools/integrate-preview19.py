"""One-use checked source refactoring, not a generator or a parity normalization.

Only the explicitly enumerated product files are changed. Reference data and old
acceptance tests remain untouched. Run from the repository root on the pinned base.
"""
from pathlib import Path
import subprocess

hashes = {
    'src/UnoDock/Controls/NavigatorWindow.cs': '38243e59ef59aaa6b92da701937b270042d1680b',
    'src/UnoDock/Internal/DockSurface.cs': '684117b05dacdcc8b6abbd61baf8624ed01825f4',
    'samples/UnoDock.Gallery/App.xaml.cs': 'a347f5b1d74491092c1ea7324c0a7d0b7aa03035',
    'Directory.Build.props': 'f79090bb4b73f83e6c96b4b80b86189e8bd7eeeb',
    'README.md': '6ac4f0da3dca76c3bc6d6108d835af5d41a74b0b',
}
for path, expected in hashes.items():
    actual = subprocess.check_output(['git', 'hash-object', path], text=True).strip()
    if actual != expected:
        raise SystemExit('Unexpected source revision: ' + path + ': ' + actual)
changes = {}
def replace(text, old, new):
    assert text.count(old) == 1, old
    return text.replace(old, new)

path = 'src/UnoDock/Controls/NavigatorWindow.cs'
text = Path(path).read_text()
text = replace(text, '        EnsureInitialized(); EndSession();', '        EnsureInitialized(); EndSession(); _activationSession++;')
text = replace(text, '        _manager.LayoutChanged += LayoutReplaced;', '''        var subscribedSession = _sessionVersion;
        _sessionLayoutChanged = (sender, args) =>
        { if (subscribedSession == _sessionVersion) LayoutReplaced(sender, args); };
        _manager.LayoutChanged += _sessionLayoutChanged;''')
text = replace(text, '        _sessionRoot = null; _manager.LayoutChanged -= LayoutReplaced;', '''        _sessionRoot = null;
        if (_sessionLayoutChanged != null) _manager.LayoutChanged -= _sessionLayoutChanged;
        _sessionLayoutChanged = null;''')
text = replace(text, '''    internal void CommitSelection()
    {
        var selected = _selected;
        if (!Eligible(selected)) return;
        var command = selected!.ActivateCommand;
        if (command?.CanExecute(null) == true) command.Execute(null);
    }
''', '')
text = replace(text, '                Select(item); _manager.Surface?.CloseNavigator(true); e.Handled = true; return;', '''                var session = _sessionVersion;
                Select(item);
                if (session == _sessionVersion && ReferenceEquals(_selected, item) && Eligible(item))
                    CloseNavigatorForInput(true);
                e.Handled = true; return;''')
text = replace(text, '        _manager.Surface?.CloseNavigator(false); EndSession();', '''        var session = _sessionVersion;
        CloseNavigatorForInput(false);
        if (session == _sessionVersion) EndSession();''')
assert text.count('_manager.Surface?.CloseNavigator(') == 3
text = text.replace('_manager.Surface?.CloseNavigator(', 'CloseNavigatorForInput(')
changes[path] = text

path = 'src/UnoDock/Internal/DockSurface.cs'
text = Path(path).read_text()
a = '    internal void ShowNavigator(NavigatorWindow navigator)\n'
b = '    internal void BeginDrag(LayoutContent content, FrameworkElement source, PointerRoutedEventArgs args)\n'
assert text.count(a) == text.count(b) == 1
start, end = text.index(a), text.index(b)
assert start < end and '    internal void CloseNavigator(bool commit)' in text[start:end]
changes[path] = text[:start] + text[end:]

path = 'samples/UnoDock.Gallery/App.xaml.cs'
text = Path(path).read_text()
changes[path] = replace(text,
    '                        ("navigator-quality", true, () => Testing.NavigatorQualityTests.Run(gallery.Dock, output)),',
    '                        ("navigator-quality", true, () => Testing.NavigatorQualityTests.Run(gallery.Dock, output)),\n                        ("navigator-commit", true, () => Testing.NavigatorCommitTests.Run(output)),')
path = 'Directory.Build.props'
changes[path] = replace(Path(path).read_text(), '<Version>0.1.0-preview.18</Version>', '<Version>0.1.0-preview.19</Version>')
path = 'README.md'
text = replace(Path(path).read_text(), '**Version: 0.1.0-preview.18.', '**Version: 0.1.0-preview.19.')
changes[path] = replace(text, '## Preview 18\n', '''## Preview 19: navigator activation and focus ownership

Navigator commits now revalidate selection, command and layout ownership after
application CanExecute callbacks. Cancelled/stale controls cannot later activate
retained selections or dismiss replacement navigators. Reentrant opening/closing
callbacks cannot steal a newer session's focus, and initialization failures release
their own host reservations. The Navigator laboratory has compact themed buttons,
live activation status and a real command-veto policy. Existing preview semantics,
reference data and API diagnostics remain unchanged. See
[navigator activation contracts and remaining setter difference](docs/navigator-activation.md).

## Preview 18
''')
# Avoid capturing an incompletely assigned local in a sample event handler.
path = 'samples/UnoDock.Gallery/NavigatorLab.cs'
text = Path(path).read_text()
text = replace(text, '        var policy = Add("Block activation", "policy",', '        SampleButton? policy = null;\n        policy = Add("Block activation", "policy",')
text = replace(text, '        void UpdatePolicy()\n        {', '        void UpdatePolicy()\n        {\n            if (policy == null) return;')
changes[path] = text
for path, text in changes.items():
    Path(path).write_text(text)
    print('Updated ' + path)
