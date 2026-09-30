# Fluent Ctrl+Tab navigator

Fluent themes now give the navigator an eight-DIP rounded frame, a one-DIP
border, more compact outer spacing, semibold selection/headings, comfortably
sized rows and a retained accent selection indicator. The selection surface is
neutral; hover, pressed and focus visuals belong to native Uno/WinUI Buttons.
No platform Button ControlTemplate is copied. The new compiled row template
composes a native Button with a non-hit-testable leading-edge indicator.

## Compatibility and native composition

`NavigatorWindow` still exposes the same document/tool collections, selection
properties, preview methods and named `PART_DocumentListBox` /
`PART_AnchorableListBox` parts. `NavigatorListBox` remains a ListBox with real
ListBoxItem containers: changing its base to ListView would break consumer
XAML, container APIs and the existing selection/automation contract. A probe
of the pinned Uno host also found no DefaultListBoxStyle/DefaultListBoxItemStyle
resources. This implementation does not invent those unavailable platform styles.
Instead it uses the supported native Button template inside the existing row.

Native Button activation and legacy row taps converge on the same existing
eligibility/session/command transaction. A native action does not directly activate
its layout model or bypass a custom ActivateCommand/CanExecute veto. Preview
continues to update only the candidate, not the active editor. Retained old row
buttons cannot activate after session closure or template-family replacement.
A row retemplating releases the old button handler and its owned state resources;
container removal releases the list's row event subscription.

Buttons do not add Tab stops inside the established navigator focus model.
Their native Invoke provider remains available, and their automation names and
tooltips reflect the document/tool title. Generic/null themes retain the original
row template, 24-DIP minimum row height, square three-DIP frame and legacy palette.
The original reference fixtures and navigator regression assertions are unchanged.

## Resources, density and motion

Fluent `UnoDock.NavigatorBrush`, `NavigatorBorderBrush`,
`NavigatorSelectionBrush`, `NavigatorSelectionBorderBrush` overrides now use
owner/ancestor/application lookup just like other Fluent chrome. Legacy resource
lookup retains its manager-local contract. `UnoDock.NavigatorCornerRadius` defaults
to eight DIPs, accepts zero for square corners, clamps to 0..24, and rejects
nonfinite input by using the default. Change dictionary values and call Refresh.

Rows are at least 28 DIPs, grow with the document-tab density metric and retain
the existing font-legibility floor. Increasing density does not replace the row,
button, platform template, ItemsSource, content adapter or editor. The existing
scroll-reveal algorithm measures actual arranged rows instead of assuming a fixed
height. Consumer-provided named ListBox parts keep their templates. The selection
indicator follows logical reading direction; RTL acceptance compares physical
bounds in the unmirrored host scope, not in an already-mirrored row's coordinates.

Native brush transitions are preserved. Rendered captures wait for the selection
transition to settle rather than disable animations or mistake an in-flight fade
for the steady selected state. Pane sizing, desktop window geometry, docking
gesture ownership, layout serialization and editor models are unchanged.

## Acceptance

The new `fluent-navigator` suite has 28 common actual-host cases plus two opted-in
Linux XTEST row-click and held-pointer cases. It covers native template/state presence, preview and commit,
exactly-once command execution, veto, reentrant selection, stale buttons, resource
identity, theme/density changes, Generic round trips, far-row reveal, corner
validation and light/dark/RTL/large-text/narrow captures. The native Invoke cases
are provider-level tests, not claims of physical pointer automation. The XTEST
cases inject actual pointer input and verify the selected target, no premature
activation while pressed, and a single release commit. Existing Enter/Escape
keyboard cases still exercise the navigator-owned focus and command path.

The permanent three-platform workflow requires all named cases, matching complete
JUnit/process records and every capture. Existing ordinary navigator-quality,
commit, revocation, focus and docking suites remain independent acceptance gates.
Local full navigator attempts in the four-GiB sandbox timed out without complete
JUnit; they are not represented as passing full-suite evidence. Final-head hosted
CI is the authority for the complete regression matrix.

Windows runtime uses Uno Skia Win32; native WinUI is compiled/package validated.
Real OS high contrast, physical AppKit pointer input, pure Wayland and mixed-DPI
hardware remain separate acceptance. This is a presentation/composition increment,
not full reference-library binary/WPF/pixel equivalence or a ListBox implementation change.

The first hosted preparation passed all 276 unchanged navigator-quality, commit
and revocation cases and 29 of the 30 new cases. Its new held-Space fixture
incorrectly tried to focus an intentionally non-tab-stop inner action and failed
that setup assertion. The revised fixture uses physical pointer capture to test
native pressed/release behavior without changing the production focus contract.
That first run remains failed; only a complete later execution is acceptance.
