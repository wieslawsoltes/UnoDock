# Automation providers and keyboard resizing (preview 18)

This increment is an independently authored accessibility/keyboard improvement,
not a claim that the original docking control exposes identical automation peers.
It does not change original inventories, type mappings or the parity baseline.
Product namespaces remain `UnoDock.*`. The classic and MVVM samples are retained.

## Splitter range contract

`LayoutGridResizerAutomationPeer` implements WinUI `IRangeValueProvider` and reports
`AutomationControlType.Thumb`. Value is the preceding **logical** pane's currently
arranged width or height in DIPs. The range excludes the divider itself. Minimum and
maximum follow the two endpoint minima and the available pair extent; impossible
minima produce a finite read-only interval, never a reversed range. Fractional layout
rounding can place the current arranged value just outside the model's minimum;
the reported interval encloses that value without altering the model constraints.

In RTL, the preceding logical pane is visually on the right. Numeric increases and
Home/End still refer to that pane's size. Arrow keys retain physical Left/Right
semantics. Orientation describes the movement axis: horizontal for side-by-side
panes and vertical for stacked panes. SmallChange is 10 DIP; LargeChange is 50 DIP.
Page Up/Down shrink/grow by LargeChange. Home/End use the current minimum/maximum.

SetValue rejects NaN, infinity and out-of-range inputs before modifying the model.
Unchanged values do not invoke setters or convert Auto/star units. Disabled,
unloaded, replaced-root, changed-orientation and changed-endpoint providers reject
writes. An in-progress pointer preview remains read-only to automation; it continues
to report committed geometry, never the moving ghost's proposed size. Providers
require the owning UI thread; the framework transport is responsible for marshalling
external requests. Direct invalid operations currently throw InvalidOperationException,
not a complete mapping of native UIA HRESULTs.

Numeric operations use the same guarded resize transaction as keyboard operations.
Absolute numeric requests compute the target star ratio from requested pixels, rather
than adding displacement to a previously rounded arrangement. Pointer and ordinary
arrow operations retain their original-observed delta/weight rule.
Star totals, mixed units, cancellation and callback-safe rollback are not reimplemented
in a second code path. A callback may veto a commit by replacing the root or editing
an endpoint; only values still owned by the transaction are restored. Arbitrary
application side effects are not reversible. Range values reflect arranged sizes,
so observers may query the old geometry until the next layout completes.

## Tab selection and metadata

The tab peer exposes SelectionItem and Invoke and the pane exposes Selection.
Select/Invoke consult the current LayoutItem.ActivateCommand, call CanExecute, and
revalidate the root, model, adapter, enabled state and command identity before Execute.
A command callback cannot authorize a stale operation on a transferred/removed model
or disposed/replaced workspace. Custom commands remain authoritative: the provider
does not force model activation after a custom command chooses another behavior.

AddToSelection rejects a second item in a single-selection pane instead of silently
replacing the selection. Select is the replacement operation. Removing the required
selected item is rejected. Explicit AutomationProperties names, IDs and help text
take precedence; otherwise title, ContentId and document description are used.
SetFocus routes to the real tab label without selecting the document. Closed/removed
models and old panes no longer report current selection. Legacy manually constructed
model-backed tab peers remain usable before first realization; previously presented
views cannot activate while unloaded.

Range notifications follow endpoint size/lifecycle changes. Tab notifications are
coalesced on the owning UI dispatcher so intermediate model flag updates are not
presented as final pane selection. Existing peers receive property/event requests;
ordinary rendering does not create peers merely to emit them. This does not certify
that every Uno platform forwards every event through an external assistive-technology
transport. Cross-process UIA, Narrator/NVDA and other screen-reader acceptance remain
separate, as do touch, mobile, IME and arbitrary application peers/templates.

## Visual and sample behavior

The divider has an accent-color keyboard focus rectangle inside its existing bounds.
The feedback is non-hit-testable, changes no measurements and is hidden after focus
moves away. A dependency-property observer handles native Tab traversal and
same-element input-source changes where GotFocus alone can miss the final state.
Light/dark and horizontal/vertical/RTL screenshots exercise that feedback.
The inner composed Thumb is raw in the accessibility tree; the public control supplies
the numeric provider. Existing pointer ghost behavior is unchanged.

Diagnostics -> Splitters opens the real sample with numeric range inspection,
minimum/maximum/application-value commands and a Focus divider action. The commands
query the actual provider of the current realized divider instead of retaining a
stale provider after sample reset. The toolbar uses independent compact sample chrome.

## Acceptance and sources

The new `accessibility-quality` suite is part of both full Linux and selected Windows
acceptance. It checks realized ranges, unit preservation, lifecycle invalidation,
reentrant and throwing setters, command callbacks, names, IDs, descriptions, selection,
focus and screenshots. Three opt-in XTEST cases exercise Tab traversal and native
Home/Page/arrow input (including RTL). Tests make direct provider calls on real Uno
hosts; they are not an external screen reader or original-product pixel comparison.
The registry retains all previously configured suites. Inspect actual JSON/JUnit for
the exact revision; a test matrix is not an execution result.

Public primary API guidance:
- https://learn.microsoft.com/en-us/windows/windows-app-sdk/api/winrt/microsoft.ui.xaml.automation.provider.irangevalueprovider
- https://learn.microsoft.com/en-us/windows/windows-app-sdk/api/winrt/microsoft.ui.xaml.automation.provider.iselectionitemprovider
- https://learn.microsoft.com/en-us/windows/apps/design/accessibility/custom-automation-peers

No original implementation bodies, templates, artwork or font files were imported.
No full-compatibility attestation or NuGet.org publication is implied by this preview.
