# Preview 15: classic sample property inspection

The default classic docking scene remains the independently authored arrangement described
in the public AvalonDock documentation: Properties / Documents / Alarms and Journal,
with Agenda and Contacts on the auto-hide rail. Product namespaces remain `UnoDock.*`;
original reference observations and probes deliberately retain their original names.

## Visual and interaction work

The sample-owned inspector now uses native CheckBox editors for booleans, ComboBox
editors with explicitly named enum values, color swatches, compact category bands,
collapsible categories, a description pane and a resizable property-name column.
The column divider accepts pointer drag, Left/Right and Home; double-tap resets it.
Search temporarily reveals matching fields within collapsed categories and clearing
search restores their previous expansion state. Sorting, filtering and column resizing
retain the existing field controls. Name truncation is accompanied by full help text.
The native sample theme selector is synchronized with programmatic theme changes.
Redundant trailing padding no longer shortens the visible selected combo-box values.

Additional registered properties include editor alignment, margin, padding, line wrapping
and multiline input. Thickness accepts one, two or four finite components. Negative
padding, out-of-range numbers and numeric/undefined enum casts are rejected. All fields
are explicitly registered; there is no private reflection or unrestricted property
execution in the inspector. This does not implement arbitrary custom type descriptors,
collection/object editors, multiple selection or full Xceed PropertyGrid compatibility.

Public feature guidance:
- https://xceed.com/documentation/xceed-toolkit-plus-for-wpf/AvalonDock.html
- https://xceed.com/documentation/xceed-toolkit-plus-for-wpf/PropertyGrid%20class.html

No original PropertyGrid templates, implementation bodies, artwork or font resources
were read or copied for these changes. The original isolated observer is not part of
the product. No claim of pixel equivalence is made for the inspector or native editors.

## Edit ownership and native event ordering

Each field captures a selection epoch. Before every write, the inspector checks its
attachment, current root, actual last-focused document, selected content identity and
row membership. Retained/queued native events from removed controls cannot write to an
old document after selection, content, root replacement, unloading or disposal.
After invoking an application setter, the same checks decide whether it is still safe
to publish status or refresh the row. Application side effects are not rolled back.

Reparenting can temporarily leave a document without a root. Parent notifications defer
selection reconciliation by one dispatcher turn so a completed float/dock transaction
does not spuriously replace its inspector rows. Selection epochs invalidate that work
on subsequent replacement or teardown. There is no timer or polling subscription.

Native TextChanged and focus events may arrive after the Text dependency property has
changed. Draft detection therefore reads current text against its last synchronized
value, rather than trusting a delayed TextChanged flag. Unrelated notifications preserve
a draft. A blur/Enter commit detects competing application edits and reports a conflict
instead of overwriting them. Escape reloads the actual value and discards the draft.
Programmatic TryEdit is an explicit edit of the current selected field.

Unchanged values do not invoke setters. Numbers are formatted with round-trip precision,
not a three-decimal display format. Merely focusing and blurring a fractional value
cannot round the document property. Unedited non-solid/unset brushes remain intact;
unchanged solid-color text preserves the original brush object. Read-only identity
fields never acquire write delegates.

## Acceptance

`UNODOCK_TEST_SUITE=inspector-quality` runs actual Uno controls and is included in both
the complete Linux and selected Windows acceptance suites. It covers typed editing,
validation, fractional/no-op behavior, stale native row events, application callback
replacement and exceptions, competing drafts, retained controls and document editors,
category/filter state, description/help, column resizing and theme synchronization.
Linux additionally uses XTEST for a real checkbox click and Escape cancellation.
Six screenshots cover the editor-selected classic scene, dark, RTL, filtered, invalid
and narrow presentations. These are additional evidence, not altered original baselines.

The previous sample, presentation, original-geometry and namespace invariant suites
remain enabled without relaxed assertions. CI's JSON/JUnit records and exact revision
establish what executed; configured case counts are not themselves execution evidence.

This increment changes sample behavior and presentation, not the normalized AvalonDock
API match count. Native HwndHost/Freezable contracts, inherited WPF semantics, arbitrary
templates, full accessibility, mobile input and workload-level performance parity remain
separate acceptance areas. The current original namespace migration and comparator
integrity tests are unchanged.
