"""Correct the first actual-host native inspector findings without changing legacy assertions."""
from pathlib import Path
pending = {}
def replace(path, old, new):
    text = pending.get(path, Path(path).read_text())
    if text.count(old) != 1:
        raise RuntimeError(path + ': ambiguous correction anchor: ' + old[:80])
    pending[path] = text.replace(old, new)

path = 'samples/UnoDock.Gallery/SamplePropertyInspector.Native.cs'
replace(path, '''        input.GotFocus += (_, _) =>
        {
            row.SuppressPresentationBlur = false;
            if (IsCurrent(row))
            {
                ShowDescription(row);
            }
        };''', '''        input.GotFocus += (_, _) =>
        {
            // Native focus notifications can arrive after presentation moved
            // focus elsewhere. Do not let that stale event clear the blur fence
            // or select a row which is no longer the focused native element.
            if (IsCurrent(row) && input is Control { FocusState: not FocusState.Unfocused })
            {
                row.SuppressPresentationBlur = false;
                ShowDescription(row);
            }
        };''')
replace(path, '''    private void PreserveDraftDuringPresentation()
    {
        foreach (var row in _entries)
        {
            if (row.Text is not { FocusState: not FocusState.Unfocused } text)
            {
                continue;
            }
            row.SuppressPresentationBlur = true;
            var epoch = _epoch;
            DispatcherQueue.TryEnqueue(() =>
            {
                // A stable focused editor still needs its next real user blur.
                // An editor hidden by this presentation keeps only the pending
                // blur suppression; GotFocus resets it on a later interaction.
                if (epoch == _epoch && text.FocusState != FocusState.Unfocused)
                {
                    row.SuppressPresentationBlur = false;
                }
            });
        }
    }''', '''    private void PreserveDraftDuringPresentation()
    {
        var moveFocus = false;
        foreach (var row in _entries)
        {
            if (row.Text is not { } text)
            {
                continue;
            }
            if (text.FocusState != FocusState.Unfocused || HasDraft(row))
            {
                row.SuppressPresentationBlur = true;
                moveFocus |= text.FocusState != FocusState.Unfocused;
            }
        }
        if (moveFocus)
        {
            // A dispatcher tick is not a focus-event barrier. Transfer focus to
            // the retained search control before moving/hiding any container;
            // the actual LostFocus delivery consumes the row's one-use fence.
            _synchronizing++;
            try
            {
                _search.Focus(FocusState.Programmatic);
            }
            finally
            {
                _synchronizing--;
            }
        }
    }''')
path = 'tests/UnoDock.VisualTests/NativeInspectorTests.cs'
replace(path, '''            var peer = FrameworkElementAutomationPeer.CreatePeerForElement(row);
            var selection = peer?.GetPattern(PatternInterface.SelectionItem) as ISelectionItemProvider;''', '''            // SelectionItem is exposed by the platform's data peer; the visual
            // ListViewItemAutomationPeer supplies container metadata instead.
            var parent = (ListViewAutomationPeer)FrameworkElementAutomationPeer.CreatePeerForElement(f.List);
            var peer = new ListViewItemDataAutomationPeer(row, parent);
            var selection = peer.GetPattern(PatternInterface.SelectionItem) as ISelectionItemProvider;''')
for path, text in pending.items():
    Path(path).write_text(text)
print('Corrected native data-peer selection and event-owned presentation blur cleanup.')
