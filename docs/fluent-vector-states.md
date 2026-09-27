# Fluent vector states and owner-scoped frames

The continued presentation pass preserves the live-state replay and ownership
rules in `fluent-controls.md`. A vector Path does not inherit Foreground like
native text/icons. DockChromeButton now observes the stock template's
ContentPresenter.Foreground and applies that exact brush to its vector fill and
stroke. Native hover, pressed and disabled states therefore also reach the docking
glyph, including a consumer's state-specific text brush. This adds no parallel
input state machine and does not alter native IsPressed, command handling or
focus visuals. Retemplating releases the previous presenter's callback before
observing its replacement. The Generic painter and templates without that standard
presenter use their existing fallback.

The Gallery workbench dictionary now belongs to the manager's resource scope.
On an actual theme change, its palette is resolved before the Gallery renews its
own manager style. Assigning that identical style before resolution retained a
light outer-frame border in a dark workspace. Repeated same-theme selections do
not reapply it; application local Background/BorderBrush values retain precedence.
The layout model, native window, manager template and editor instances remain.
This is a sample-scoped style correction, not a general override of consumer styles.

Two added Gallery cases check exact frame brush identity over Light/Dark/Generic
round trips, repeated selections and application local overrides. Together with
the existing live-state cases, fluent-presentation has 40 common cases and three
opt-in Linux physical-keyboard cases. The independent fluent-state-resources suite
adds 30 actual-host cases for direct, named, merged, shared and Default consumer
resources, unrelated/contrast dictionary retention, Resources replacement,
Generic round trips, in-place override addition/removal and vector foregrounds.
Both suites run once each in separate native processes; dedicated CI requires
every named resource case and exact process/JUnit count agreement. Visual-state
cases are presentation tests, not evidence of physical pointer or OS contrast input.

The local combined source passed 43/43 presentation cases and 30/30 independent
resource cases on Uno/X11, including the held-Space palette-switch assertion.
Earlier failing resource and frame investigations are not substituted for that
combined-source acceptance. Final PR-head CI remains the merge requirement.
