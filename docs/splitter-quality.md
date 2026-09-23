# Transactional splitter resizing (preview 10)

## Implementation

`LayoutGridResizerControl` composes the sealed native WinUI Thumb, retaining the same
control on generic Uno. It now exposes DragStarted, DragDelta, DragCompleted,
IsDragging and CancelDrag, with explicit internal transaction stages used by the
layout grid. This is not inheritance compatibility with WPF Thumb.

A drag captures the two visible endpoints, layout/root/manager identity, orientation,
original GridLengths, realized pixel lengths and minimum constraints. Moving the
pointer updates one non-interactive preview rectangle; pane sizes and persisted
lengths are unchanged. Overshoot and reversal are solved from the original snapshot,
not from accumulated changes or asynchronously refreshed grid measurements.

On normal pointer release, the pair is committed in one root-update batch. Both-star
pairs preserve their combined star weight; the change is added to the original ratio,
rather than reconstructing it from rounded pixels (which introduced a visible initial
jump in the reference replay). Mixed pixel/star pairs retain their unit kinds and the
observed two-pane normalization. A zero-displacement completion preserves exact values
and units, including Auto. An actually resized Auto length becomes an absolute length.

The captured visible sequence, root, orientation, minimums and original lengths are
validated before preview and commit. Root replacement, endpoint removal, dimension
changes, disabled/unloaded controls, flow-direction changes, Escape and CancelDrag
abandon the preview without writing its values. An application edit is preserved.
Model callbacks may replace the root or edit an endpoint: the commit checks again
between setters, and attempts rollback only for values still owned by that operation.
Rollback attempts both endpoints even when a rollback observer throws; both errors
are retained. Arbitrary observer side effects are not made transactional or reversed.

## Actual input ordering

The pinned Uno Thumb can raise DragCompleted with Canceled=false during capture loss,
disabling or unloading, before the owner receives its corresponding lifecycle event.
A native completion therefore waits one UI-dispatch continuation. Only a matching
primary-pointer release authorizes commit. A session generation and the native child
state prevent a stale completion from acting on a new gesture. Capture loss without
release is cancellation, and the forwarded completion reports Canceled=true.

No polling loop is used. CancelDrag ends the model transaction before releasing native
capture, and completion/event forwarding happens once for each normal native session.
Native DragDelta arguments are forwarded unchanged to consumers; the docking operation
uses the actual pointer in a stationary parent's coordinate space, not assumptions
about delta-versus-total conventions across frameworks. Public completion delivery for
a native release may therefore be deferred by one dispatcher turn compared with WPF.

Keyboard resizing consumes only the divider's movement axis. Horizontal Left/Right
follow physical direction under RTL; vertical Up/Down are unaffected by RTL. Keys
move ten DIPs, and Escape cancels an active gesture. This is an independent accessible
keyboard behavior, not a claim that the original Thumb handled these keys identically.
The divider remains a real focusable control and uses a native resize cursor.

## Original public observations

`tools/ReferenceVisualProbe/SplitterObservations.cs` constructs original public models,
traverses realized public control geometry, raises public routed Thumb drag events and
records public property values. No original source body, IL, template or resource
is inspected or copied. The checked-in XML contains 32 scenarios: four length-unit
pairs, horizontal/vertical orientation, LTR/RTL and cancelled/noncancelled completion.
Reference and probe revisions, artifact hashes and method are recorded alongside it.

The observations show unchanged model lengths during the 40/15 drag-event sequence,
then commit using the last displacement (15) rather than the supplied completion total
(55). Pixel values and star normalization, plus the observed black preview brush and
0.5 opacity, are replayed for all sixteen noncancelled scenarios. The port's tests run
against real, arranged Uno controls; they do not infer visual success from model counts.

The original **ignores the synthetic DragCompletedEventArgs.Canceled flag** in this
specific public-event protocol. The port intentionally cancels safely instead. This
fixture does not establish original Escape behavior or original end-to-end pointer
acceptance. That difference remains explicit; no blanket behavioral parity is claimed.
The current reference workflow compares the entire splitter XML from two independent
probe processes as well as the existing guide screenshot/geometry reproducibility.

## Tests and sample

`SplitterQualityTests` contains 60 Linux / 53 Windows cases. Sixteen replay noncancelled
reference scenarios; seven Linux-only XTEST cases cover actual primary-pointer release,
RTL, vertical movement, Escape, disabling, unloading, and capture loss. Other cases
cover min constraints, no-op unit retention, nonfinite input, observer edits/exceptions,
reentrancy, stale layouts, keyboard axes and custom preview brushes/opacity.
Four actual PNG captures show horizontal, vertical, RTL and dark previews. Geometry
and behavior are asserted independently of screenshots. Font rasterization and all
pixel-level appearance details are not certified equivalent.

The gallery's **Splitter quality** document offers star/star and pixel/star pairs,
orientation, RTL and theme switches, cancellation and XML inspection. It uses the same
public docking control and native input path as applications.

## Boundaries

Reference replay currently covers two visible endpoints, not every nested/three-way
star-sizing combination. Native OS pointer injection is exercised on X11; Windows runs
the portable realized-control protocol tests and the existing HWND/lifecycle suite.
Native WinUI is package-built, macOS is build-only, and browser publishing is separate
from browser/touch/input acceptance. Full UI Automation range-provider semantics,
mobile devices, multiple monitor DPI transitions, WPF inheritance and general routed
input infrastructure remain separate compatibility work.
