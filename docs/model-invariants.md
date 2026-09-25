# Ownership and activation invariants

## Structural mutations

Owned collections and single-child slots prepare parent-changing notifications
while the original ownership tree is still intact. Once preparation succeeds,
collection/slot storage and child parent pointers are committed without invoking
application callbacks. Post-change callbacks therefore observe both directions
of ownership consistently. Insert, remove, replace, clear, move, transfer and
floating-window slot replacement share this boundary.

A failure before commit leaves the original structural mutation unapplied, apart
from changes explicitly made by application callbacks. A failure after commit is
reported after independent parent/root notifications, pane repair and update
leases have been attempted. A single exception retains its identity and stack;
multiple failures are retained in chronological order. Arbitrary application side
effects are not rolled back. A cross-container transfer consists of a coherent
detach followed by a coherent attach; an exception during detach may leave the
item detached rather than moving it into the requested destination.

Preparations carry parent and child-list generations. Reentrant changes which
replace a destination slot, alter a prepared collection, or transfer the incoming
child revoke the old preparation. The operation does not reclaim the child from
an application-selected container. Collection notifications retain the base
ObservableCollection reentrancy policy; clients must not assume a blanket rollback
or that every multicast subscriber runs after another subscriber throws.

## Activation

Layout-root activation serializes nested requests with last-explicit-request-wins
semantics and a 64-transition convergence bound. A pre-change request can withdraw
an uncommitted target. After commit, nested requests are processed after the
current transition's notifications; nested setters do not recursively run a second
activation transition halfway through the first one.

The active-content pointer and active flags are committed before post-change
callbacks. A deactivation callback which requests C while the outer caller requests
B cannot leave B and C active simultaneously. Independent notification failures
are collected while later queued requests still complete. Detached contents use
the same prepared flag/notification boundary, and flags owned by another root are
not cleared by an obsolete root's repair operation.

Last-focused pointers and flags are updated coherently. Root repair adopts active
content inserted from a detached tree and clears extra flags. These are model
invariants, not claims of full original AvalonDock event-order equivalence for
arbitrary throwing or reentrant application callbacks.

## Coverage

The initial new actual-host suite has 53 cases. It injects failures during parent
preparation, parent publication, collection events, owner notifications and root
updates for each structural operation. It checks ownership inside callbacks,
after the exception and after subsequent successful edits. Additional cases cover
reentrant transfers/slot replacements, retained error identity, pane selection
repair, and activation redirected from property, selection, timestamp, root and
activation-event callbacks, both with and without exceptions.

The source organization pass separately checks declaration-token preservation
under four compile configurations. Existing API inventories, generated adapters
and compatibility baselines are unchanged. Final-head CI, rather than this document,
establishes the executed platform results.
