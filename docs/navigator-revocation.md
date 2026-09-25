# Preview20 continuation: revocation and sample reachability

The continuation starts from PR #8 head
`3efaf3e7590b5a5ef07bd7d2217212fe45df74a6`. Ordinary CI run
`36041183648` found four navigator-commit failures on both platforms, a native
sample-policy click failure on Linux, and an incomplete Windows runtime run at
its three-minute process deadline. The actual Linux report contained 2,693
executed cases, of which five failed. The Windows report contained 844 completed
cases, of which four failed; that is not a complete Windows acceptance result.

## Activation revocation

Explicit keyboard/host commits still detach before querying the activation
command. The command, model, manager and navigator observers now span detachment
itself, so a changed-and-restored value from an `Unloaded` callback cannot escape
validation merely because its final value matches the capture.

A different preview request on a detached/closed navigator revokes its pending
activation without changing its published selection, showing the view, or starting
a new session. This also rejects change-then-restore (ABA) requests. An identical
preview remains a no-op. Effective direct-property changes have an independent
request generation which is checked by explicit as well as property-driven
activation. Direct assignments still use the preview20 document-hide/tool-close
contract and preserve the original exact-replay fixtures.

Detachment is idempotent and guaranteed even when no command can be captured or
application code throws. Temporary observers are released in `finally`. Command
vetoes, harmless title changes, same-value selection and one-use authorization
retain their previous behavior. No callback side effects are rolled back.

`navigator-revocation` joins both full Linux and selected Windows acceptance. Its
real-host cases cover document/tool queries, actual Unloaded callbacks, effective
and no-op requests, ABA changes, parent/root ownership, and tool Closing/Closed.
The prior four failing assertions remain unchanged in `NavigatorCommitTests`.

## Reachable native sample controls

The navigator laboratory uses an independently authored wrapping panel instead of
a horizontally clipped scroll toolbar. All eleven existing native buttons retain
their order, instances, automation peers and commands. The two property-assignment
actions remain ordinary buttons; resizing does not rebuild the docking model or
editor content. Real-host checks cover 360, 540 and 780 DIP sample widths, positive
bounds, non-overlap, command identity and retained active content, with screenshots.

The native sample-input test still uses XTEST, not an Invoke substitute or click
retry. It waits for pointer entry to be processed and then verifies exactly one
actual Button.Click before asserting the command-policy result. The separate
native automation-provider scenario is retained.

## Validation budget and scope

Windows now has the same bounded five-minute runtime process budget as Linux,
inside the unchanged thirty-minute job deadline. Every selected suite still runs;
a timeout is a failure. No assertions, reference fixtures, API mappings, comparator
thresholds or diagnostic allowlist entries were relaxed.

Product namespaces remain `UnoDock.*`; the clean-room boundary is unchanged. This
increment does not claim full AvalonDock API/behavior/pixel parity, native WinUI
runtime coverage, external UIA transport, or NuGet publication. Exact committed
CI results, rather than this document, establish executed acceptance counts.
