# Navigator property selection and explicit preview

Preview20 changes active-session `SelectedDocument` and `SelectedAnchorable` assignment
from preview-only selection to command-driven activation. Both CLR assignment and
`SetValue` go through the dependency-property metadata callback and protected virtual
hook. A derived hook may omit its base implementation to veto the default action.
Product namespaces remain `UnoDock.*`.

## Public reference, not implementation inspection

An independently authored net48 probe invokes the pinned original through public and
protected APIs only. It records actual selection properties, visibility, Closing/Closed,
active content, command callbacks and exceptions. It does not inspect original source
bodies, IL, private fields, templates or resources. The original is built in isolated
CI temporary storage and removed; no original implementation is shipped in UnoDock.

Reference revision: `2c71faba5eecc1b6ae6cd3d269408e0df37715d8`.
Probe revision: `b007cd14d9165107d37044faec1ab49b3b3e94bc`.
Run: `36032890549`. Artifact: `10823916978`.
The 28-case XML is required to be byte-identical across two probe processes. Raw XML
and its artifact/file digests are retained in `contracts/visual-fixtures`.

The observations correct an earlier description: document selection **hides** the
original navigator without Closing/Closed; tool selection **closes** it. They are not
two names for the same lifecycle transition.

| Active-session property operation | Document | Tool |
| --- | --- | --- |
| New eligible selection, command allowed | Hide, then execute | Closing, Closed, then execute |
| Command veto or throwing CanExecute | Remain visible | Remain visible |
| Allowed command that performs no activation | Hide, preserve command's result | Close, preserve command's result |
| Throwing Execute | Already hidden; propagate exception | Already closed; propagate exception |
| Same effective DP value | No new query or callback | No new query or callback |
| Clear with null | Clear that category; no activation | Clear that category; no activation |
| Closing veto | Not invoked by hide | Retain view, but still execute if ownership remains valid |

The two direct-selection DPs are independent: assigning a tool does not clear the
previous document property. Preview operations deliberately publish mutually exclusive
category selection for keyboard navigation. Public window visibility is represented
by actual membership in the managed docking surface, not a fabricated WPF Window.
A document-hidden instance can be shown again through the owning host. An instance
closed by tool selection is terminal. Detached property writes cannot authorize work
in a former session. Full WPF Window.Show/Hide inheritance is not supplied.

## Migration for preview-only callers

```csharp
navigator.PreviewDocument(documentItem);   // Highlight only; do not query the command.
navigator.PreviewAnchorable(toolItem);    // Highlight only; do not activate.

navigator.SelectedDocument = documentItem; // Query; hide and activate when allowed.
navigator.SelectedAnchorable = toolItem;   // Query; close and activate when allowed.
```

Ctrl+Tab, arrow/category navigation and Home/End retain their preview behavior.
Enter/Control release and pointer selection retain their existing explicit commit
path. Those explicit commits are distinct from property assignment: changing the
property contract does not silently change keyboard commit/veto behavior.

Existing preview tests use the new preview methods with their previous assertions
unchanged. This is an explicit test setup migration for the changed public behavior,
not removal of preview/scrolling/container/command-ownership acceptance.

## Ownership and callback boundaries

Direct selection uses the same one-use guarded activation as explicit commits. It
checks the current session, surface, root, adapter, model, parent membership, command,
selection and enabled state before and after application CanExecute. Temporary
observers detect root, command, enabled and membership changes even when reversed.
Closing/Closed/Unloaded callbacks cannot authorize a stale command or steal the focus
of a replacement navigator. Exceptions release transition guards and subscriptions.

Direct requests made during a transition are serialized with a 64-request convergence
budget. A newer effective request invalidates the older one; an overwritten queued DP
value cannot revive itself. Nulling the other category updates its list without
clearing the current preview. Ordinary model title changes remain valid.

Safety and substrate differences remain explicit. The original probe activates a
disabled target; UnoDock rejects it. The original document/query-reselect scenario
uses nested protected callbacks; UnoDock serializes those callbacks while selecting
the final requested document. The original permits document reselection from Execute
after hiding; UnoDock keeps its detached-session safety rule. These are four raw
reference cases (two disabled cases and two document reselect cases), not exact event
replay passes. All 28 observations remain intact; the other 24 are replayed against
the port with exact immediate/settled state, event order and exception type. Initial
preview is set to the observed document in the replay; this does not certify MRU
initialization order.

Arbitrary application callback side effects are not reversible. Full original event
ordering in unprobed cases, closed-window DP semantics, native non-client behavior,
external automation transport, accessibility and pixel equivalence are not claimed.

## Sample and evidence

Diagnostics -> Navigator adds `Assign document` and `Assign tool` commands using the
real public properties. `Block activation` demonstrates that a direct assignment veto
keeps the navigator and selected row visible. Existing editors, theme controls, RTL,
large-font and 3/40/200-document scenes remain.

Acceptance includes direct property/SetValue replay, no-op and exception states,
closing vetoes, terminal/reusable instances, stale command/root/parent/enabled changes,
replacement views, foreign adapters, visible LTR/RTL veto rows and existing native
keyboard/pointer suites. CI results on the exact commit establish executed counts;
this document is not a substitute for those results. Original metadata inventories,
namespace mappings, comparator and prior regression allowlist are unchanged. The API
shape count is not a feature-parity percentage, and full strict parity remains open.
