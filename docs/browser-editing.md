# Native editing and broker acknowledgements

The owning Uno runtime edits its retained native view. On a change, the browser
broker validates the content ID and lease, commits the new title/payload and
returns a detached copy of the canonical record. That acknowledgement includes
the actual revision; callers must not guess it from a local counter.

`BrowserDockingSession` validates the acknowledged record against the current
identity, owner, kind, type, placement and lease, and rejects a backwards revision
or an unchanged revision for a changed payload. It advances its projection record
without assigning the text back to the view. Thus the next ownership poll sees an
acknowledged local edit, not a remote payload to replay into a live text selection.
Payload-only updates also no longer force `DockingManager.Refresh()`. Membership,
lease/type replacement, requested activation and theme changes retain the normal
refresh path. Failed projections remain dirty.

A successful acknowledgement means the authoritative live model accepted the
edit. It is not a claim of durable browser storage: storage failures remain visible
in the snapshot and the UI, and the current journal can still be exported.

Browser tests use keyboard Select All and character events through Uno's real
semantic text box. Playwright `fill()` changes selection on the DOM proxy only;
Uno Skia maintains its own managed selection. The regression types successive
chunks across polling intervals, requires exact content and revision convergence,
and checks that acknowledged typing does not increment the docking refresh count.
It does not invoke the broker's update callback as a substitute for native input.

The earlier Chromium/Mono renderer crashes are a separate unresolved validation
boundary. Passing the protocol-level acknowledgement tests alone does not qualify
a build for publication; every required native scenario must still pass.
