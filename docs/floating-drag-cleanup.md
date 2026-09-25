# Floating-drag terminal callback failures

The final preview21 review identified a teardown ordering defect: hiding docking
guides invokes application Visibility/Unloaded observers before releasing caption
capture. An observer exception could therefore leave IsDragging, the caption
session, or its native clock alive after the surface had already dropped ownership.
The same ordering existed on both cancellation and release.

## Contract

The surface revokes the old gesture and stops header scrolling first. Old tab
input/unload handlers are removed before invoking application cleanup observers.
Guide cleanup, caption-state/capture cleanup, and source capture release are then
attempted independently. One callback failure retains its exception identity and
original stack; multiple failures are reported in occurrence order through an
AggregateException. None is silently treated as a successful drop.

A failed release teardown never executes its captured docking plan. A retained
late release is inert; a later independently initiated gesture remains usable.
The source's existing models and editor content are not rolled back or replaced.
Cancellation still restores geometry only while the original workspace owns it.
A successor that reacquires a source does not lose capture to the old teardown.

Guide clearing snapshots the native floating hosts instead of enumerating a
collection which an application callback can change. It stops clearing after a
successor changes the gesture generation. Each overlay separately revokes its old
plan and snapshots/removes only its old guide visuals. An application callback
that opens a replacement preview owns that replacement; an older Hide or
ShowPreview cannot hide its fill/border afterwards.

This is callback fault containment, not a promise to undo arbitrary application
side effects, force a permanently throwing application observer to cooperate,
or provide a transactional rollback of user content.

## Acceptance

`floating-drag-cleanup` adds 27 actual-host cases to ordinary and native desktop
acceptance: 24 document/tool, native/in-surface, cancel/release combinations with
throwing guide/caption/both callbacks, plus three overlay cleanup/reentrancy cases.
The matrix checks all failure identities, every terminal state, stopped clocks,
retained editor instances, no accidental drop, inert stale release, and a usable
next gesture. Native evidence verification requires the suite and its complete
case count; two Python regressions check missing and partial cleanup evidence.

These are deterministic callback injections into actual Uno hosts. The existing
XTEST and SendInput suites separately exercise physical pointer gestures. No
original behavioral fixture, pixel baseline, API mapping, or comparator threshold
is changed. Final executed counts are recorded in PR #9 after CI, not assumed here.
