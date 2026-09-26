# Native templated property inspector

This migration replaces the sample property table's manually managed StackPanel
and custom SampleButton presentation with a built-in ListView, native ListViewItem
containers, native category ToggleButtons, and compiled XAML data templates for
TextBox, CheckBox, ComboBox, color and read-only value editors. It changes the
sample presentation; it does not introduce another docking model or reflective
PropertyGrid implementation.

## Retained behavior

The existing registered field definitions and parsing/validation functions remain
the source of truth. Enter applies a draft; Escape discards it. A genuine editor
blur retains its existing commit/conflict behavior, while reordering/filtering the
presentation must not implicitly commit a draft. Selection epochs still reject
callbacks from former documents and replaced content. Read-only identities do not
become writable, nonfinite dimensions and unnamed numeric enum values remain
invalid, and unchanged brushes retain their object identity.

Category/alphabetical order, case-insensitive name/category search, remembered
collapse state, the description area, column-width validation, pointer/keyboard
column resizing, RTL direction and model-change subscriptions are retained.
Containers and their editors are moved rather than reconstructed when order
changes. Filtering collapses the native container as well as its content, so
hidden entries do not leave blank list rows. Native row selection updates the
same description state as focusing an editor. Category Toggle automation uses
the same Checked/Unchecked path as native pointer/keyboard interaction.

## Native presentation

`SampleInspectorResources.xaml` contains the shell and row templates. The platform
owns Button/ToggleButton/TextBox/CheckBox/ComboBox/ListView control templates,
visual states, keyboard focus and automation peers. Styles only override metrics,
spacing and lightweight properties. No copied platform control template or custom
pointer-selection implementation is introduced. Semantic ThemeResource brushes
replace hard-coded sample table colors; native resources provide light/dark and
contrast-aware values, but operating-system high-contrast transitions still need
separate hardware acceptance.

The inspector registers a small bounded property set and promises editor identity
and draft retention through filtering/sorting. It deliberately uses a StackPanel
as the native ListView's ItemsPanel, avoiding cell recycling. This is not a large,
virtualized data grid. General data-heavy tables should use native virtualization
and store editing state in row models rather than retaining every visual editor.

## Validation

The new `native-inspector` suite exercises native containers and automation,
selection/description synchronization, native category toggles, hidden row geometry,
retained editors, presentation-only draft preservation, original validation,
read-only copy support, stale selection/disposal callbacks and five presentation
captures (light, dark, RTL, narrow and invalid input).

The existing inspector-quality and sample-quality assertions remain unchanged.
The dedicated preparation executes those suites, including opt-in XTEST input on
an isolated display, and requires their actual process/JUnit results before a
source tree is accepted. Final PR CI, not this document, establishes passing test
counts and platform coverage. No claim of complete PropertyGrid/DataGrid parity,
browser runtime, native WinUI runtime or physical AppKit input is made.

This change is independent of the pending consumer-workspace PR #13. Its approval
gate is not bypassed or treated as passed by testing this separate migration.
