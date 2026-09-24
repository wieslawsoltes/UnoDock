# Preview 16: source-backed workspace and guarded restoration

Product namespaces remain `UnoDock.*`. This continuation keeps the default classic
Properties/Documents/Alarms scene, its independent property inspector, and all previous
visual observations. The **MVVM binding** selector now opens a second, functional
workspace rather than two isolated demonstration notes.

## Sample architecture

`MvvmWorkspace` owns a catalogue and an observable collection of open
`WorkspaceDocument` view models. `DocumentsSource` and `AnchorablesSource` contain
models, not UI elements. Closed buffers stay in the catalogue and can be reopened by
double-clicking a Workspace row or pressing Enter. A document has stable identity,
observable text/name/read-only state, exact dirty tracking, and save/revert/close
commands. Dirty titles propagate through explicit public `LayoutItem.SetBinding`
bindings to the actual model adapters and rendered document headers.

The sample uses ordinary compiled Uno data templates. It does not depend on arbitrary
binding-valued `Setter.Value` compilation, which remains a separate platform boundary.
The pre-existing Note template/style examples are preserved. Default visual rows are
not claimed to be identical to original WPF item containers.

The workspace has a file catalogue on the left, documents in the centre, document
properties on the right, and status output below. Compact editor toolbars expose real
save, revert, and close commands. The properties tool controls read-only state and
unsaved-buffer close protection, reports line/character counts, and captures/restores
layout snapshots. The outer classic menu and sample/theme selectors remain available.

The app-owned editor instance is retained through ordinary tab activation and
float/dock transitions. Replacing a whole layout can rebuild template containers;
the view models and buffers retain their identities, and sample-owned tool views are
transferred into their new presenter. This is not a promise to retain every native
editor's selection/undo stack through an XML restore.

## Save and close semantics

Save snapshots the requested buffer before asynchronous IO. The successful write
updates the saved baseline to that snapshot, not to any newer text entered while IO
was pending. Such newer text remains dirty. A failed write leaves the baseline and
buffer unchanged. Concurrent saves to the same document are rejected, and a saving
document cannot be closed. Dirty-close protection is enabled by default and may be
explicitly disabled. Closing always leaves the buffer in the session catalogue.

Sample documents are written under application-local `mvvm-documents`, using validated
IDs rather than user-controlled display names as file paths. A temporary file is
written before replacement of the named file. This is not a general-purpose file
picker/editor, a crash journal, or an across-process atomicity guarantee. The catalogue
is session-owned; automatically recovering it on application restart is not implemented.
Revert is an explicit destructive action that restores the last successfully saved
buffer (or its initial contents). Layout XML never stores editor text.

Late async completions cannot update a replaced or disposed workspace. The sample
layout-save path serializes writes and uses a temporary file. Layout restore captures
sample/root/request identity before asynchronous reads and abandons stale requests.
This does not cancel an OS write that has already begun or reverse arbitrary user
callback side effects.

## Library restoration ownership

`LayoutSerializer` acquires a manager-wide weakly associated lease before invoking a
caller-owned reader. Separate serializer instances cannot bypass the same manager's
reentry guard; unrelated managers remain independent. The original layout and its
previous content/icon/tooltip values are captured once. A dependency-property
observation detects root replacement, including replace-then-restore of the same
root identity. Disposal also invalidates the operation.

Each resolver callback is followed by ownership validation. Nodes moved by a callback
out of the detached candidate are not subsequently overwritten. A callback may omit
a node with Cancel. Before and after publishing the candidate, ownership is checked;
a competing application root stays in place and the outer operation reports failure.

The lease remains active through source-reconciliation cleanup. It is released even
when cleanup throws. If both the operation and cleanup fail, both exceptions are
retained, rather than masking the original failure. Normal parsing still creates a
detached model and never incrementally edits the attached layout. Callback-created
application side effects are not rolled back.

The MVVM sample resolves content IDs against its existing catalogue, then reconciles
its open source collection while the manager is suspended. Saved closed tabs reopen
without replacing buffers; unknown IDs are omitted rather than materialized as fake
editors. This explicitly separates layout persistence from application content storage.

## Provenance and acceptance

The sample was independently authored against public Uno controls, public AvalonDock
concepts and the public feature description at:
https://xceed.com/documentation/xceed-toolkit-plus-for-wpf/AvalonDock.html

No original implementation, sample source bodies, templates, artwork or fonts are
imported. The original deterministic inventories, fixtures, type mappings, comparator
and diagnostic allowlist are unchanged. Namespace migration is not counted as a parity
gain, and sample-only additions do not improve the normalized reference API count.

`restore-ownership` executes manager/serializer callback, reader, source and exception
regressions. `mvvm-workspace` executes actual Uno templates, editor/model bindings,
rendered dirty titles, retained editors, close/reopen, save races, failure/retry, source
membership, XML identity and screenshots. Linux additionally exercises native Save and
Revert buttons with XTEST. The sample tests use deterministic injected storage; the
interactive sample uses application-local storage. These are distinct validation scopes.

Both suites are included in Linux and selected Windows acceptance, not just special
selectors. Five additional PNG/XML captures cover light, dark, RTL, dirty and restored
MVVM workspaces. They are evidence, not an original pixel-comparison baseline. Existing
classic geometry assertions and namespace invariants remain enabled without loosened
limits. Exact workflow reports establish which cases executed successfully.

Full WPF inheritance/event-routing semantics, arbitrary original templates, all
binding-valued style setters, arbitrary file workflows, mobile input, native WinUI
runtime coverage and workload performance equivalence remain separate acceptance areas.
