# Native templated property inspector

The Gallery's Properties tool uses a built-in ListView with native ListViewItem
containers, category ToggleButtons, and compiled XAML templates containing
TextBox, CheckBox, ComboBox, color and read-only editors. This replaces the former
manually managed row surface and SampleButton presentation without introducing
another docking model or a reflective PropertyGrid implementation.

## Retained behavior

The registered field definitions and parsing/validation functions remain the
source of truth. Enter applies a draft; Escape discards it. A genuine editor blur
retains the existing commit/conflict behavior, while presentation-only sorting,
filtering and category changes must not implicitly commit a draft. Selection
and ownership epochs still reject callbacks from former documents or replaced
content. Read-only identities remain read-only; nonfinite dimensions and unnamed
numeric enum values remain invalid. Unchanged numbers use lossless round trips,
and unchanged brushes retain their object identity.

Category/alphabetical order, case-insensitive name/category search, remembered
collapse state, the description area, column-width validation, pointer/keyboard
column resizing, RTL direction and model subscriptions are retained. Containers
and editors are moved rather than reconstructed when order changes. Filtering
collapses both the native container and its content, avoiding empty list rows.
Native row selection updates the same description as editor focus. Category
Toggle automation follows the same Checked/Unchecked path as native interaction.

The draft fence follows actual FocusState changes. Before presentation moves or
hides an editor, the inspector transfers focus to the retained search box under
its synchronization scope. A subsequent new editor focus re-enables genuine
blur commits. A dispatcher delay alone is not used as a native focus-event barrier.

## Fluent styling without replacing native control templates

`SampleInspectorResources.xaml` contains compiled shell/row DataTemplates and
lightweight styles. The platform still owns Button, ToggleButton, TextBox,
CheckBox, ComboBox and ListView ControlTemplates, visual states, focus visuals
and automation peers. Styles override metrics, spacing and semantic brushes;
they do not copy the platform's control-template implementation. No custom
pointer-selection implementation or replacement automation peer is introduced.

Semantic ThemeResource brushes supply surfaces, primary/secondary text, strokes
and validation color. These use platform resources for light/dark and contrast
values; actual operating-system high-contrast transitions still need separate
hardware acceptance. Density and theme changes elsewhere in the dock continue
to use the established UnoDock resource and ownership conventions.

The inspector has a bounded registered field set and promises editor identity
and draft retention through sorting/filtering. Its native ListView deliberately
uses a nonvirtualizing StackPanel ItemsPanel. This is not a general high-volume
DataGrid or TableView. Large data tables should retain draft state in row models
and use an appropriate virtualizing native control; they should not copy this
bounded inspector's visual-retention policy.

## Accessibility and input boundary

The list and rows use native ListViewAutomationPeer/ListViewItemAutomationPeer
metadata. A physical XTEST row-label click is exercised on Linux; category Toggle
providers exercise the actual native control state path. The pinned Uno host's
SelectionItem-provider route is not supported by the tested ListView path, so
external UI Automation selection transport is not claimed or disguised behind
a custom peer. Native container selection and editor focus still synchronize.
Read-only text is selectable, and fields retain their automation names/help text.

## Validation and integration

`native-inspector` adds 16 common actual-host cases plus one opt-in Linux XTEST
case. They cover native containers, selection/description/focus synchronization,
category toggles, hidden-row geometry, editor/container retention, drafts,
validation, stale selection/disposal events and five rendered presentations:
light, dark, RTL, narrow and invalid input.

The pre-existing `inspector-quality` and `sample-quality` assertions are unchanged.
The dedicated three-platform workflow runs all three suites once each in fresh
processes. Its gate requires complete named native cases, matching JUnit/process
records, the exact revision and all five captures. Twelve independent Python
fixtures test the evidence gate; those synthetic fixtures are not runtime tests.

The preparation source was compiled and exercised in run 36264208501, then stored
as tree ae31be3a56b9a2286cb8292e23897f058aa339c5. Integration commit 718278627aa8ea62996d9a1d0a71376fee96240a preserves the original inspector
and reconciled XAML histories. Its focused run 36265778831 passed on all three
platforms. These are preparation/integration records, not substitutes for the
final PR head's ordinary, XAML, docking, chrome and source-quality acceptance.

This is the first built-in-control migration, not a claim that every custom dock
control has been replaced. Native floating chrome, drag state machines, navigator
ownership and the model API are unchanged by the inspector presentation diff.
No package publication, full PropertyGrid/DataGrid parity, browser runtime,
native WinUI runtime or physical AppKit pointer acceptance is claimed.
