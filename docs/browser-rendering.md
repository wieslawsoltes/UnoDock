# Browser polling and rendering

The browser host polls for remote ownership/content changes, but that interval is
not a render clock. `BrowserDockingSession` compares the window's complete leased
content records, effective theme and requested active item with its last applied
projection. An unchanged poll reports the current local state without opening an
update batch, rebuilding views, or forcing `DockingManager.Refresh()`.

A changed lease, record, membership, theme or requested selection still enters
the normal model-projection path. A failed projection remains dirty for a later
attempt. In-window docking, sizing, editor and focus events retain UnoDock's own
invalidation path and are not suppressed by the broker's idle check.

The bridge report includes `projectionCount` for diagnostics. It counts completed
adapter-driven refreshes, not display frames or GPU timing. The browser regression
requires this counter to stay unchanged across several idle polls, then advance
for a real theme command while the actual native control remains accessible.

An already-captured Closed event from an obsolete model is additionally required
to match the currently registered model object before closing a broker record.
Lease checks still guard edits and transfers independently.

The earlier eight-scenario run at a46e24b failed by timeout after a successful
immutable WASM publish; it did not produce complete acceptance evidence. A V8
profile showed managed timer activity after all four records had been projected.
These observations motivated removing the unnecessary refresh, but do not prove
it is the only cause of the browser startup/input failure. Final browser execution,
including native accessibility and editing, remains required before publication.
