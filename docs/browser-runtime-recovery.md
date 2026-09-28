# Native browser runtime failure and recovery

The host distinguishes the authoritative content broker from the Uno runtime that
presents it. A successful broker transfer does not prove that native layout or
rendering completed. Unhandled errors or promise rejections in the current native
iframe now retire its ready state, keep the first error visible, disable native
transfer controls, and display a recovery action instead of stale success text.
The original errors are not swallowed, and no fake semantic controls are created.

The monitor is scoped to the iframe document generation. Navigation detaches the
old listeners; an already-captured callback from the old document cannot fail its
replacement. Ordinary resource-load events are not mistaken for unhandled runtime
exceptions. Background timer throttling alone is not treated as a crash.

**Restart native renderer** is an explicit user action. It replaces only this
iframe runtime. The session broker, other browser windows and accepted application
payloads remain. Existing host-readiness logic increments the owned content leases
and revisions before replacement editors attach. Old editor requests therefore
remain invalid. There is no automatic restart loop and no attempt to suppress the
underlying fault. Export remains available while broker content is recoverable.

Nine isolated event-lifetime tests cover observer ownership and failure retention.
The browser scenario starts actual Uno controls, injects one deliberate unhandled
error, requires the visible failure, restarts through the real button, and verifies
native controls, retained content/session, renewed leases and rejected stale writes.
This scenario tests recovery handling; it is not evidence that unrelated browser
runtime failures have been fixed. The complete browser suite remains mandatory.
