# Preview 7 validation checkpoint

Implementation commit: `2d3933d3d7e2120ff6bc23199ccdb157b40875fa`.
GitHub Actions: https://github.com/wieslawsoltes/UnoDock/actions/runs/35788310906.
The run completed successfully on 2026-09-22; all six jobs passed. This record's
cleanup commit removes the one-use integration workflow and adds documentation only.
Inspect the workflow for the exact revision being consumed.

## Executed regression cases

| Suite | Linux passes | Windows passes |
| --- | ---: | ---: |
| Portable core | 97 | Not run |
| Runtime/control | 36 | Not run |
| Original-layout interoperability | 44 | Not run |
| Drop/menu/automation | 40 | Not run |
| Lifecycle/source handling | 33 | Not run |
| Interaction | 25 | Not run |
| Converter replay/binding | 1,517 | Not run |
| Window coordinates | 16 | Not run |
| Shell/chrome | 36 | Not run |
| Window lifecycle/navigation | 49 | 47 |
| Input extensions | 43 | 35 |
| Visual parity | 27 | 27 |
| **C# totals per platform** | **1,963** | **109** |

There were zero failures. Twenty Linux XTEST scenarios execute with native pointer/key
input; Windows does not count those Linux-only cases as executed. The Windows suite
includes its two actual HWND filter cases. Platform totals overlap and must not be
summed as distinct test cases. The Python metadata comparator has 23 passing cases.
The complete SDK/XAML desktop application built on Linux, Windows and macOS. Browser
publishing and generic Uno/native WinUI package production also passed.

## Measured visual evidence

The new runtime suite captures five real rendered scenes through RenderTargetBitmap
and BitmapEncoder, producing PNG and arranged-geometry XML on Linux and Windows.
Four scenes replay the original stock-layout observations: docked, active tool,
auto-hide and right-to-left. The fifth is an independently designed dark palette.

For every checked visible pane, panel, editor and side-rail rectangle, the maximum
absolute coordinate/size difference was 0.08 DIP on each platform in all four scenes.
The committed test tolerance is one DIP. This compares scene-specific arranged geometry,
not pixel identity or every visual state. Text-dependent label lengths, fonts and glyph
rasterization are not represented as equivalent. Screenshots were inspected separately.

Four additional original public-API observations verify per-item auto-hide transitions,
independent groups, pin restoration order and an ineligible sibling. The tests also
exercise live themes, caption capabilities, cancellable hide/close, retained presenters,
custom templates, icons, overflow scrolling, density overrides and native RTL projection.

Reference source revision: `2c71faba5eecc1b6ae6cd3d269408e0df37715d8`.
Reference probe revision: `09b16fa838f0176bf26b083b47fbea8e68662b9e`.
Reference capture run: https://github.com/wieslawsoltes/UnoDock/actions/runs/35778896158.
The public observation XML preserves its original bytes, including Windows CRLF; the
scoped .gitattributes entry declares that convention without suppressing trailing spaces.
No original templates, resources, implementation bodies, artwork or fonts are packaged.

## API comparison and limits

The resolved comparison remains **977/1,031** matched entries: 881 declared members,
24 inherited counterparts and 72 type shapes. **54 signature/type entries** remain
unresolved (9 missing members, 12 signature differences, 33 type-shape differences),
plus 18 separately reported attribute differences. Two inventories of the same built
input are byte-identical. No comparator rules, explicit mappings or regression baseline
were relaxed. The no-regression gate passes; the full strict parity gate does not.

Native runtime evidence here applies to Uno's Skia X11 and Win32 hosts. The native WinUI
package is build-validated, not comprehensively runtime-validated. macOS is build-only;
browser publishing is not browser input/runtime acceptance. Multi-DPI/monitor behavior,
mobile/touch, screen-reader traversal, arbitrary templates, floating/native chrome,
complete menu/navigator styling and performance equivalence need broader acceptance.
Existing library warnings about inherited Dispose members and Uno ListBox APIs remain;
this is not a warning-free or complete-compatibility release.

## Artifacts

The run publishes exact source/revision/checksum and API/test reports in
`core-and-api-results`, platform-separated runtime results, `visual-parity-Linux` and
`visual-parity-Windows` PNG/XML captures, desktop/browser builds and NuGet packages.
The source ZIP was verified by reconstructing every Git blob and tree and comparing
its root to committed tree `32a85c659dad5f40127dafaee565f434420b39af`.
Downloaded source and runtime artifact digests were verified.

The packages are version `0.1.0-preview.7`. They were produced, **not published to
NuGet.org**. No full-compatibility attestation is supplied. See [visual implementation](visual-parity.md)
and [compatibility boundaries](compatibility.md).
