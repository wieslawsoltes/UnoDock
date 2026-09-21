# Architecture

The core has no Uno dependency. `DockSplitSolver` handles bounded sizing and resizing;
`DockDragSession` maintains pointer identity, threshold, candidate priority, cancellation,
and commit; `UpdateBatch` coalesces nested and reentrant invalidation; `LayoutSnapshotXml`
implements a bounded, data-only XML codec.

The Uno layout model owns a strict tree. Each element has one parent; insertion validates
cycles and type constraints before ownership transfer. Owned collections update parent,
root, selection, and notification state. Root update scopes coalesce visual refresh.
All mutation is a UI-thread operation when the model is attached to an application.

`DockOperations` is the shared transition layer for commands, model methods, and input.
Floating/auto-hide nodes preserve their previous pane/index. Restore protects previous
containers from premature garbage collection. Document selection is pane-local;
activation is root-global. Close and hide have cancellable hooks.

`DockingManager` adapts observable item sources and WinUI dependency properties to the
model. Weak collection listeners and explicit disposal bound subscriptions. Layout
items own commands and retained content presenters. Source objects are not serialized;
a stable `ContentId` resolves them during XML restore.

`DockSurface` reconciles keyed model/view dictionaries. Layout grids retain child views
and dividers. Pane headers are keyed by model identity, and content presenters survive
selection changes. A separate overlay layer hosts auto-hide panes, drag previews, the
navigator, and portable floating hosts. Native floating hosts compose a WinUI `Window`
instead of imitating WPF's Window inheritance.

Serialization first parses a bounded snapshot, constructs a detached typed model,
resolves container references and content callbacks, and only then replaces the root.
The codec is explicit rather than reflection-driven, so it cannot instantiate arbitrary
CLR types from layout XML. Exceptions before replacement preserve the current workspace.

The sample is also the UI test host: tests execute on the actual Uno dispatcher with
real layout controls. Headless geometry tests are deliberately separate from UI tests.
Neither test suite should be described as original-AvalonDock behavioral equivalence.
