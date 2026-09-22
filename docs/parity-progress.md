# Preview 2: compatibility implementation and evidence

## Implemented in this continuation

The port now has public drop-area, drop-target and overlay contracts; all 19 original
`DropTargetType` values are retained. `DockDropPlan` represents validated intent and
rechecks ownership/capabilities when executed. Document targets, tool targets, empty
document groups, outer edges and docking tools beside document groups have regression
tests. Releasing a drag performs a fresh hit test. An invalid inner target cannot
silently fall through to an outer target. A pointer that does not own a drag cannot
complete it. Overlay preview and execution use the same policy.

Duplicate drop policy compares both Title and ContentId. Mixed-orientation policy,
CanDockAsTabbedDocument, CanMove, CanRepositionItems and immutable floating hosts are
checked before committing. Preview callbacks can cancel, disable capabilities, remove
targets or replace the layout without committing against the old tree. Per-content
transition guards suppress recursive operations. Close/hide callbacks revalidate root
and parent identity. LayoutChanging/LayoutChanged replacements drain to the final root;
direct dependency-property assignment cannot steal another manager's root.

The declaration-based adapter generator now emits the reference's protected virtual
property-change hooks and calls them from real dependency-property callbacks. Hooks
retain independent model reconciliation behavior. Floating controls expose dragging
state and cancellable closing hooks. Their dimensions use DIP/pixel conversion at the
native host boundary. Layout diagnostic virtual slots and original XML-ignore metadata
have been implemented. Concrete layout groups declare their XAML content property.

New menu controls include DropDownButton, DropDownControlArea, ContextMenuEx and
MenuItemEx. Shared menus resolve the target LayoutItem when opened, not during rendering.
Owned temporary DataContext values are removed on close; application values/bindings
are preserved. Item-source enumeration and container creation finish before replacing
the current menu. Icon templates must produce actual WinUI IconElement objects.

Tab strips use constrained water-filling for tool tabs and natural-width document tabs.
Header navigation handles Home/End/arrows, disabled tabs and right-to-left traversal.
Selection and invoke automation providers expose the actual model state. Retained
content is now **lazy**: unopened tabs do not create presenters; visited editors remain
cached. This is not complete header virtualization or a performance-parity assertion.
Auto-hide resizing begins from its displayed fallback dimension when the model stores
zero (unspecified), avoiding a first-drag jump to the minimum size.

Observable source reconciliation snapshots both sources before mutation and drains
reentrant changes. Identity-based sets replace repeated linear membership searches.
The gallery's **Parity lab** exposes measured areas, validated plans, execution, lazy
presenter counts and a shared data-bound document menu. Default-value controls reflect
the actual manager values rather than unrelated checkbox/slider defaults.

## Measured validation

Local validation before this commit: 45 portable core tests, 36 Uno runtime tests,
44 original-layout interoperability tests, 40 drop/menu/automation tests, and 33
lifecycle/reentrancy tests: **198 C# tests**. The resolved-API comparison has **23 Python
regression tests**. The original core random solver loops remain; the new tab-width
suite adds 5,000 randomized allocations. These are tests written and executed, not an
inference of full feature equivalence.

Runtime tests execute in a real Uno Skia/X11 host. The local isolated compiler does not
replace the official SDK XAML build; GitHub Actions validates the actual gallery,
generic Uno and native WinUI package targets. Windows/macOS jobs are build checks;
browser publication is a build check. Neither implies automated pointer delivery,
screen-reader validation or browser runtime equivalence on those platforms.

## Resolved metadata gate

`tools/check-metadata.sh` builds the actual Uno library, exports its resolved references,
scans PE metadata twice and requires identical results. It runs the comparison against
the pinned original Release contract. Individual type substitutions are recorded in
`contracts/type-mappings.json`; broad namespace rewrites are not used. Parameter names,
optional values, ref modes, generic constraints, virtual slots, visibility, enum values
and setter visibility remain significant. Quoted attribute/default strings are data
and are never type-mapped. Reference nullability annotations are kept separately;
Nullable<T> value types are not collapsed.

The scanner supplies inherited candidates with their declaring types and removes
candidates hidden by closer declarations. Constructors are never inherited. This is a
conservative structural comparison; explicit framework adapters can remain diagnostics
even when a particular source usage works. The gate additionally reports attributes
and direct base/interface shape, rather than hiding these behind a member-name score.
Compiler-generated nullable, state-machine and debugger-browsable attributes are
separated from the contract. Remaining meaningful attribute differences are reported.

Current local result: **849 / 1,031 reference entries matched**, including 767 declared
members, 24 inherited members and 58 type-shape matches. There are **182 unresolved
signature/type entries** and **22 attribute differences**. `metadata-baseline.json`
records those known diagnostics and binds them to reference/mapping hashes. CI fails
on newly unresolved entries. It is a **no-regression baseline**, not an acceptance of
full compatibility. `--strict` still fails for the outstanding diagnostics.

The older syntax report remains useful for declaration checks: **693 / 996 exact mapped
declarations**, compared with 592 in preview 1. Its 303 missing/different declarations
are not interchangeable with the resolved report's 182 entries; the reports use
different denominators, visibility resolution and inheritance treatment.

## Remaining boundaries

Full parity is not complete. Remaining work includes Microsoft.Windows.Shell's
SystemCommands/SystemParameters2/WindowChrome; WPF native-message/HwndHost contracts;
legacy protected mouse/focus/initialization extension points and other framework slot
mappings; outstanding converter/template/attribute contracts; native Skia cross-window
coordinates/docking and OS title-bar behavior; full tab virtualization, drag auto-scroll,
keyboard/touch/accessibility/localization acceptance, mobile sample heads and device
execution. Additional event-order, custom-source and legacy XML behavior cases still
need original public-API probes. See compatibility.md for migration consequences.

The independent implementation was developed from the pinned public API metadata,
public documentation and black-box fixtures; no original implementation bodies,
resources, templates or artwork were translated. No stable-release parity attestation
or NuGet.org publication is represented by this preview.
