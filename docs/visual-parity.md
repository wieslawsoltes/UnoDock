# Compact chrome and observed visual conformance (preview 7)

The default docking chrome is independently constructed from Uno controls and small
original vector geometries. No original AvalonDock templates, resources, artwork, fonts
or implementation bodies are used. The target is the **stock theme of the pinned public
AvalonDock repository**, not the separately licensed commercial theme collection.

## Reference evidence

The independently authored `tools/ReferenceVisualProbe` and shared application-owned
`tools/VisualScene/SceneContent.cs` render the pinned original through public APIs.
Reference revision: `2c71faba5eecc1b6ae6cd3d269408e0df37715d8`.
Probe revision: `09b16fa838f0176bf26b083b47fbea8e68662b9e`.
Run: https://github.com/wieslawsoltes/UnoDock/actions/runs/35778896158.

`contracts/visual-fixtures` contains the original public geometry and per-item auto-hide
observations, with provenance and the downloaded artifact digest. Reference screenshots
were inspected separately; they are not embedded in the library. Nothing is OCR-derived.
The four reference scenes are docked, active tool, auto-hide and right-to-left, all at
1000 x 640 DIPs. Their tool-pane/editor content is our own text, not original product assets.

The new runtime suite arranges the equivalent live Uno scenes, saves PNG and geometry
XML, and checks **every visible pane/panel/editor/side-rail rectangle** against the
original observations within one DIP. This tolerance accommodates cross-framework layout
rounding. The initial Linux captures have a maximum difference of 0.08 DIP in those
rectangles. The rotated label's text-dependent length is intentionally not a fixed
geometry assertion: installed fonts, text shaping and rasterization differ between hosts.
Dark mode is independently designed and captured, not asserted as an original dark theme.

These are measured scene-specific layout assertions. They do not imply pixel-identical
rendering, exhaustive visual parity, font equivalence or coverage of every application
ControlTemplate. There is no percentage advertised as whole-product visual parity.

## Rendered controls

The default title is 18 DIPs, document tab strip 20 DIPs, tool tab strip 23 DIPs, and
side rail 26 DIPs. Document tabs stay above the editor; tool tabs are below it and a
single-tool pane hides its redundant tab strip. Thin pane separators, selection fill,
caption buttons and document overflow replace the oversized generic button chrome.

Buttons remain real Uno Buttons with automation Invoke and keyboard focus; they are not
canvas-only hit boxes. Their shared small ControlTemplate is independently authored.
Each UI thread owns its immutable default brushes and template cache. Vector close, pin
and dropdown marks do not depend on a symbol font. Live theme changes update existing
controls and retained editors rather than replacing the layout tree. Application title
and header DataTemplates remain supported. Default document icons use ImageSource when
no custom header template is supplied.

Overflow selection reveals its header via ScrollViewer without resetting user scrolling
on unrelated refreshes. Queued reveal work checks selection/generation/root attachment.
The documents menu revalidates model ownership before activation. Captions are valid
inside-drop regions when a single-tool tab strip is hidden; a caption drop appends,
whereas a visible tab strip retains boundary-based insertion. Drag auto-scroll only acts
on the actual visible strip, not the caption or a collapsed control's stale bounds.

Custom density and colors can be applied before calling Refresh:

```csharp
manager.Resources["UnoDock.TabHeight"] = 32d;
manager.Resources["UnoDock.TitleHeight"] = 28d;
manager.Resources["UnoDock.RailThickness"] = 32d;
manager.Resources["UnoDock.ForegroundBrush"] = myTextBrush;
manager.Resources["UnoDock.PaneBrush"] = myPaneBrush;
manager.Refresh();
```

Other keys: ToolTabHeight, FontSize, HeaderBrush, InactiveTabBrush, BorderBrush,
HoverBrush, PressedBrush, AccentBrush and ActiveTitleBrush. Numeric density values
are finite and clamped; invalid values use defaults. Setting RequestedTheme with no
explicit Theme follows light/dark defaults; FluentTheme supplies an explicit palette.
The sample's **Visual parity** laboratory demonstrates these operations in a nested
manager without replacing the application's main workspace.

## Behavioral corrections discovered by the original probe

The original moves **one requested anchorable** to an independent auto-hide group.
An ineligible sibling does not veto an eligible tool's transition. Two successive hides
create two groups. Pinning one of those groups appends that tool to its previous pane
and leaves the other group hidden. Four checked-in original observations are replayed
by the runtime tests. Earlier tests asserting whole-pane auto-hide were corrected to
match these observations. Arbitrary hand-authored multi-child groups, every older XML
variant and complex callback mutation sequences still need broader reference evidence.

Auto-hide flyout close now follows the model's cancellable hide/close path, rather than
merely dismissing the overlay. Escape still dismisses it. Pinning restores the retained
presenter. Flyouts start next to the actual side rails and are bounded by the remaining
client area, replacing fixed 34/32-DIP offsets.

Root RTL mirroring is included in native screen conversion. Transforming merely to
XamlRoot.Content cancels that root transform, which produces plausible but wrong native
coordinates. The adapters now transform to physical client axes and invert the entire
destination transform. Tests use independent expected mirrored coordinates and cross-
window projections rather than relying solely on a roundtrip that could hide equal bugs.

The pre-existing Linux input failure was a test-host activation issue: after closing a
temporary window, the main test window could remain inactive on WM-less Xvfb. Explicitly
reactivating it before the sequence restores real press/release delivery without changing
or skipping the release-veto assertion. Native drag tests now use the visible caption
of a single-tool pane, not the tab strip that correctly no longer exists visually.

## Validation and remaining work

The suite contains 27 new visual/behavior cases. Core plus Linux runtime execution covers
1,963 C# cases, including the existing 20 opt-in XTEST scenarios; the metadata-comparator
has 23 Python cases. CI compiles real SDK/XAML targets, executes Linux tests, runs the
Windows lifecycle/input/visual subset, builds macOS, publishes the browser gallery and
packs generic Uno/native WinUI libraries. PNG/XML artifacts are uploaded as
`visual-parity-Linux` and `visual-parity-Windows`. Inspect the exact revision's CI result.
The local iteration harness uses current C# plus existing generated gallery XAML/ICU;
it is not a substitute for the clean SDK CI builds.

Resolved API evidence remains 977/1,031 matches, 54 signature/type differences and 18
attribute differences. No API mappings, comparator rules or acceptance baseline were
relaxed for the visual work. Full strict parity remains unsatisfied.

Further visual acceptance includes floating/native non-client surfaces, full menu and
navigator styling, arbitrary templates, touch density, contrast themes, text/IME state,
multiple monitors/DPI, mobile/browser input, performance equivalence and a larger original
behavior corpus. The classic geometry is not a certification of those unrelated areas.
