"""Correct actual-host native inspector findings without changing legacy assertions."""
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
        };''', '''        if (input is Control focusControl)
        {
            // FocusState is authoritative even when a rapid Focus/blur sequence
            // coalesces the later routed GotFocus/LostFocus notifications.
            var focusToken = focusControl.RegisterPropertyChangedCallback(Control.FocusStateProperty, (sender, _) =>
            {
                if (sender is Control { FocusState: not FocusState.Unfocused })
                {
                    row.SuppressPresentationBlur = false;
                }
            });
            _tokens.Add((focusControl, Control.FocusStateProperty, focusToken));
        }
        input.GotFocus += (_, _) =>
        {
            if (IsCurrent(row) && input is Control { FocusState: not FocusState.Unfocused })
            {
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
            // LostFocus consumes the fence, and a new FocusState clears it.
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
            var selection = peer?.GetPattern(PatternInterface.SelectionItem) as ISelectionItemProvider;
            Check.True(selection != null);
            selection!.Select();''', '''            var peer = FrameworkElementAutomationPeer.CreatePeerForElement(row);
            Check.True(peer is ListViewItemAutomationPeer);
            Check.Equal(AutomationControlType.ListItem, peer!.GetAutomationControlType());
            // Uno's data-selection peer rejects this native ListView path. Do
            // not substitute a custom peer or claim unsupported UIA transport;
            // select through the native container, with physical input below.
            row.IsSelected = true;''')
replace(path, '''            await Wait(() => ReferenceEquals(f.List.SelectedItem, f.Row("FontSize")));
            Check.Equal("FontSize", f.Inspector.SelectedPropertyName);''', '''            await Wait(() => ReferenceEquals(f.List.SelectedItem, f.Row("FontSize")));
            await f.Settle();
            Check.Same(f.Field<TextBox>("FontSize"), Microsoft.UI.Xaml.Input.FocusManager.GetFocusedElement(f.Inspector.XamlRoot!));
            Check.Equal("FontSize", f.Inspector.SelectedPropertyName);''')
replace(path, '''        return tests.Run(output, "native-inspector");''', '''        if (OperatingSystem.IsLinux() && Environment.GetEnvironmentVariable("UNODOCK_NATIVE_INPUT_TESTS") == "1")
        {
            Add("XTEST: native inspector row click selects and describes the property", async f =>
            {
                f.Inspector.Filter("FontSize");
                await f.Settle();
                var row = f.Row("FontSize");
                var label = ((Grid)row.Content).FindVisualChildren<TextBlock>().Single(text => text.Name == "PropertyName");
                using var input = new X11TestInput();
                await input.Click(label);
                await Wait(() => ReferenceEquals(f.List.SelectedItem, row));
                Check.Equal("FontSize", f.Inspector.SelectedPropertyName);
            });
        }
        return tests.Run(output, "native-inspector");''')
for path, text in pending.items():
    Path(path).write_text(text)
print('Corrected native container selection, physical click coverage and event-owned draft focus state.')
