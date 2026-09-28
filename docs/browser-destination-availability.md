# Destination availability and atomic dock-back

A registered browser window can outlive its native Uno renderer. A renderer error
must therefore affect transfer eligibility in every source window, not only the
failed window's own toolbar. The broker now tracks an `accepting` flag separately
from the historical readiness marker used to rotate content leases on reload.

## Failure, reload and recovery

The shell reports the native document's `starting`, `ready` and `failed` states to
its already-authenticated broker connection. Starting or failed hosts disappear
from other windows' destination selectors. `Move`, drag-ticket consumption and
`Dock all` validate the destination in the broker, so a stale selector or delayed
request cannot bypass the presentation check.

A top-level satellite reload withdraws its destination eligibility immediately
after reconnecting, before its replacement Uno frame loads. An explicit native
readiness handshake makes it eligible again. The subsequent native report and
repeated status notifications are idempotent: they do not churn content leases or
write the journal. Suspending transfer eligibility does not reset the old
readiness marker. Replacement editors still receive incremented leases/revisions,
and old editors remain unable to write with a prior lease.

This is an availability check, not a guarantee that a renderer cannot fail after
a successful transfer. The separate native-error monitor and recovery controls
remain responsible for faults that happen during or after content projection.
Background throttling alone does not mark a renderer failed. The checks are not
a sandbox against hostile same-origin scripts.

## Dock all is one broker operation

`WorkspaceState.transferAll` validates the source, destination and every open
record's placement before changing ownership. It then updates all owners, leases
and revisions and invokes persistence once. A journal callback cannot observe a
partially returned group. Original payloads, titles, content types and preferred
docking zones are retained. Closed content is not reopened by dock-back, and an
empty or same-host return does not create a new commit.

When the primary renderer is unavailable, **Dock all & close window** is disabled
with restart guidance. The broker independently rejects the same request without
moving any record or closing the satellite. Once the primary renderer recovers,
the original command works without a new browser session.

Browser-owned window close is different from voluntary dock-back. Closing a
satellite still reassigns its content to the primary broker even when the primary
renderer is unavailable; otherwise the last copy of that content could become
unreachable. The journal/export and explicit primary recovery preserve access.
As before, a storage failure leaves complete in-memory ownership and a visible
storage error, not a false durability guarantee.

## Verification

The new protocol tests cover failed destinations, reload lease rotation, repeated
status reports, preflight rejection, atomic persistence observation, storage
failure, closed content, no-op returns and browser-close recovery.

Three additional real-browser scenarios raise deliberate errors only after real
Uno controls are present: a failed satellite cannot receive content until recovery;
a failed primary blocks a multi-record dock-back without partial movement; and a
satellite whose new native navigation is held cannot receive content until the
replacement runtime is ready. They retain the existing renderer, native controls,
lease model and original 13 browser scenarios. There is no auto-retry, simulated
native editor or substitution of broker success for rendered application state.

See [browser workspaces](browser-workspaces.md) and
[native renderer recovery](browser-runtime-recovery.md).
