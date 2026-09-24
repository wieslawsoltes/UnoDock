# Preview 17: callback-safe source reconciliation and compact MVVM chrome

## Source reconciliation

DocumentsSource and AnchorablesSource are still identity-based enumerable sources;
null entries and repeated references are ignored. The manager now captures both source
identities, the attached layout, insertion strategy and a notification revision for each
pass. A delivered collection notification invalidates that pass even when a callback
restores the same source identity. Checks occur across enumeration, descriptor access,
model setters, insertion hooks and batch completion. The existing 64-pass convergence
limit remains, without a dispatcher polling timer.

Both enumerable snapshots and direct-model ownership validation complete before either
source's removals. MoveNext, Current and enumerator Dispose may execute application code;
a replaced source is abandoned rather than evaluated for stale titles or placement.
Errors remain observable; previous callback side effects are not rolled back.

Source tracking is reserved before insertion callbacks. An attached model remains
tracked after a throwing AfterInsert, so later source removal can remove it. Aborted
unattached candidates are released for retry. A strategy returning true without attaching
its candidate remains a handled insertion, not an automatic infinite retry.

Removing an entry no longer grants authority to detach a model transferred into another
root, or a wrapper whose Content was repurposed by the application. Direct model sources
must be unparented or already belong to the target root; foreign ownership is rejected
before mutation. Explicit transfers should detach the model and remove its former source
entry. Same-root custom insertion placement is respected even when a strategy returns
false; a foreign custom placement is not stolen back. AfterInsert and tool selection
are suppressed when source/root/disposal ownership changes.

These are independently authored ownership safeguards, not a claim that every callback
ordering or exception in the original is identical. User collections still need a safe
threading discipline. Off-thread notifications are delivered to the UI queue; this does
not make concurrent mutation of arbitrary IEnumerable implementations thread-safe.
Closing a source model does not automatically edit application-owned collections.

## Sample presentation

The MVVM editor reuses the sample's independently authored compact button chrome. The
command bar is 31 DIP (24-DIP buttons plus margins and separator), and the status bar is
23 DIP. Hover, pressed, disabled and keyboard-focus states stay on actual native Buttons.
Bindings and editors are retained across inherited theme changes; no document-event
subscription is added for painting chrome. Generic, light, dark and RTL scenes have
explicit command clipping/height and editor-space assertions plus PNG/XML captures.
Existing XTEST Save/Revert tests remain on the real command path.

The default classic sample, original observations, generated property adapters,
UnoDock namespaces, type mappings and metadata allowlist are unchanged. These MVVM
captures establish their measured sample layout, not original pixel equivalence.

## Validation

The source-ownership and mvvm-chrome suites are registered in normal Linux and selected
Win32 acceptance as well as standalone selectors. The ordered harness registry preserves
all previous suites and their Windows selection. Counts come from executed JSON/JUnit
reports, not this document or source registration. No assertions, original fixtures,
API mappings or tolerances were weakened.

Public feature basis: https://xceed.com/documentation/xceed-toolkit-plus-for-wpf/AvalonDock.html
No original implementation, templates, assets or fonts were imported.
Full API/behavior/visual parity, arbitrary framework callbacks, mobile input, accessibility,
performance equivalence and native WinUI/browser runtime acceptance remain separate work.
