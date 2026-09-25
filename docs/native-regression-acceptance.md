# Native geometry and regression acceptance

Both native stacking entry points now share the same Windows/X11/AppKit
implementation. The historical three-argument internal entry point remains
unambiguous; the variant excluding a moving source has its own method name.
The existing real-input caption tests observe the whole-window capture owner,
not the independent single-tab drag state machine. The decorative title remains
non-hit-testable while the retained caption handle is a client chrome exclusion.

Native origins are read from actual HWND rectangles, checked XCB frame geometry,
or AppKit frames. The host's notification-driven `AppWindow.Position` cache is
not the origin authority when starting a drag or persisting a cancellation.
The X11 acceptance driver independently queries Xlib screen coordinates and
checks both physical restoration and retained layout-model coordinates after
queued events. Sending the same move repeatedly while the mouse is stationary
is suppressed without stopping guide or capability updates.

The AppKit clock registers in both common modes and the concrete event-tracking
mode. It rejects construction off the main loop, rejects cross-thread disposal,
and prevents recursive callback execution. Eight AppKit-specific cases include
actual screen-axis checks, callback-mode verification, disposal inside a native
callback, stale-delivery prevention, and exceptions in both tick and diagnostic
observers. A native tracking activation stopped by an unrelated host event is
re-entered only in that same mode, under a bounded deadline.

Ordinary Linux and Windows acceptance uses `tools/run-desktop-tests.py`. It asks
the compiled application's existing suite registry for its selected suites, then
starts one fresh native process for each suite, exactly once. The optional `all`
selector in the application remains available for single-process diagnostics.
Process isolation bounds cumulative host-resource retention; it is not a claim
that an underlying framework or driver memory leak has been found and fixed.
The local validation container's all-in-one run reached its 4 GiB memory limit;
local constrained-heap runs and hosted CI are recorded separately in the PR.

Each isolated run requires an empty output directory, records the execution plan,
and writes first-attempt logs plus a durable result after every completed suite.
Every suite must supply complete, uniquely named JUnit cases with no failure,
error or skip, irrespective of native process exit status. A failed suite does
not suppress later suites, and an overall deadline cannot turn unrun work into a
pass. The previous automatic whole-run diagnostic retry is removed. The runner's
12 parser/manifest regressions complement the 14 native-evidence-gate regressions.
