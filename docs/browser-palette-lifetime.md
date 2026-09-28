# Browser palette lifetime

## The failure that motivated the representation change

A browser workspace has two independent responsibilities: the JavaScript broker
assigns ownership, while each Uno runtime constructs and renders its own controls.
The broker correctly returned content when a satellite closed, but the primary
Uno runtime could stop while creating the returning tabs and their context menus.
A successful broker snapshot therefore was not sufficient acceptance evidence.

The original failure was captured from the unchanged `61f913734906e8c07f983e5fbb7737f21c51794a`
application. The reproduction floats `architecture` and `notes` into separate
browser windows, moves the document into the tool window at the left edge, then
uses **Dock all & close window**. No private application methods or simulated
editors are used in this sequence.

The original native module is `dotnet.native.cpatndrtvy.wasm`, 67,659,807 bytes,
with SHA-256 `70967742716a26c81d866610008f9c9cf7b2436a94de439167856efbc2b027c0`.
Rebuilding the same input with a symbol map retained that native module identity.
The recorded stack resolves to:

```text
emscripten_memcpy_bulkmem
__memcpy
copy_object_no_checks.1
simple_nursery_serial_scan_object
simple_nursery_serial_drain_gray_stack
finish_gray_stack
collect_nursery
```

Pausing at the actual WebAssembly exception exposed a copy length of `0xfffffffc`.
Read-only inspection of the same runtime's linear memory found a cleared vtable
at the source reference. The object being scanned was identified from its class
metadata as `UnoDock.Internal.DockChromeButton`. The referring slot was 996 bytes
from the start of that object, in the region occupied by its embedded palette's
brush references; several consecutive palette references still addressed the
nursery. The invalid zero-size copy underflowed when the collector subtracted the
four-byte header word.

These observations localize the invalid retained references. They do **not**, by
themselves, establish whether a runtime write barrier, GC descriptor, compiler
lowering or another earlier operation originally produced them. The allocation
that triggers collection is not necessarily the operation that introduced the
invalid reference. Reducing allocations, increasing heap limits, suppressing GC,
or automatically restarting the runtime is not considered a correctness fix.

## Palette ownership

`DockPalette` is now an internal sealed reference record. Retained controls hold a
single reference to a separately traced palette object instead of embedding a
large reference-bearing value. The light/dark thread-static caches hold the same
reference type. `DockChromeButton` initializes its palette before setting control
properties and rejects a null palette before changing its state.

The representation retains structural record equality, shallow `with` cloning,
brush identity, light/dark values, density metrics and existing configuration
call sites. The record's bindings are immutable; the referenced Uno brushes keep
their existing mutability and ownership semantics. This is an internal change,
not a public layout API or serialization format change.

## Acceptance and recovery are separate

The full browser suite must execute without retries or skipped required cases.
Its repeated-window regression runs six cycles covering left, right, top, bottom
and tab placement. Each cycle edits an actual native satellite TextBox, transfers
the document to another satellite, returns it through the real close command,
checks the main native editor's text and rejects unhandled runtime errors.
Ownership must remain unique and the broker must finish with only the main host.

The separate [runtime recovery regression](browser-runtime-recovery.md) deliberately
raises one native-document error. It verifies that stale readiness is retired,
accepted content remains available, explicit renderer-only restart preserves the
broker/session, leases are renewed, and obsolete editor writes are rejected.
Passing recovery does not count as passing an ordinary docking operation that
crashes. The release workflow enforces both cases independently, alongside the
original native editing, drag protocol, popup blocking and journal tests.

## Retained diagnostic evidence

The following runs preserve the original failure and the symbol/object evidence.
A successful diagnostic-capture job means that a fault was captured, not that the
application passed its lifecycle test.

- [Original AOT application artifact](https://github.com/wieslawsoltes/UnoDock/actions/runs/36422498961).
- [Matching native symbol map](https://github.com/wieslawsoltes/UnoDock/actions/runs/36433030157).
- [Read-only object and referring-slot capture](https://github.com/wieslawsoltes/UnoDock/actions/runs/36453678621).
- [Native renderer recovery validation](https://github.com/wieslawsoltes/UnoDock/actions/runs/36435830734).

For the current application result, inspect the **Browser workspace and GitHub
Pages** check on the pull request's current revision. The diagnostic runs above
must not be substituted for current application acceptance.
