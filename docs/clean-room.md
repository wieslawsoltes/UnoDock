# Provenance and reference process

Reference: the pinned reference revision recorded in `contracts/reference.json`
(`repository`, `revision` and project `path`). That manifest is the only place the
reference library is named; recorded contracts, tools and documentation use the neutral
term "the reference library" and the neutral root namespace token `Reference`.

Pinned revision: `2c71faba5eecc1b6ae6cd3d269408e0df37715d8`

Scope: the reference project directory recorded as `path` in `contracts/reference.json`.

## Neutral naming

`contracts/reference.json` records the original root namespace (`referenceNamespace`,
which is also the reference assembly name) and its neutral replacement
(`neutralNamespace`, `Reference`). The generators apply the substitution themselves:
`tools/ApiScan` and `tools/ApiMetadata` take `--namespace-alias <original>=<neutral>`,
and the reference workflows read both values from the manifest. ApiMetadata substitutes
every emitted string, keeps record order from the original metadata and re-sorts only
the hashed declaration list, so a regeneration reproduces the recorded
`contracts/reference-*.{json,txt}` files. The original-runner probes import
`tools/ReferenceIdentity/ReferenceIdentity.targets`, which references the pinned
assembly, generates the probe's namespace imports into the intermediate directory and
normalizes recorded type names through `ReferenceIdentity.Normalize`.

## Declaration inventory

The isolated `Reference API inventory` workflow fetches the pinned revision into a
runner temporary directory. `tools/ApiScan` emits externally visible declarations and
input hashes. Method bodies, comments, non-constant initializers, attributes, templates
and images are not emitted. Two runs must produce byte-identical JSON. The default
preprocessor profile contains 996 sorted declarations.

The property-adapter generator uses declaration names/types to create independent
Uno dependency-property plumbing. Behavior is implemented in our partial classes,
not translated from original method bodies.

## Resolved metadata inventory

`Reference metadata contract` builds the unmodified original projects in isolation
for Release and Debug, then invokes `tools/ApiMetadata`. The scanner imports symbols
from PE metadata without executing the assembly or reading IL bodies/resources.
It records resolved signatures, public/protected visibility, implicit public
constructors, generic constraints, attributes, enum values, optional parameters and
base/interface relationships. Unresolved signature types cause failure.

The verified Release profile has 105 exported types and 1,031 declared API entries;
Debug has 105 types and 1,020 entries. Each profile is scanned twice and its complete
JSON compared. Original assembly binaries, source bodies, templates and artwork are
not added to this repository or its packages.

The build-only LegacyCopy.targets workaround omits missing legacy .nlp normalization
companions from the copy-local stage. It does not alter original source, compiler
inputs or emitted APIs.

## Public-API behavior probes

`tools/ReferenceProbe` is independently authored and runs against the isolated original
assembly through its public constructors, properties, docking methods and serializer.
It captures scalar CLR defaults and dependency-property metadata for 13 types, plus
13 serialized layout scenarios. There is no non-public reflection or IL inspection.

The original serializer creates random container GUIDs. The probe canonicalizes Id
and matching PreviousContainerId values in traversal order, preserving reference
relationships. ContentId values, topology, flags, selection, timestamps, sizes and all
other behavior data are retained. Probe output is generated twice and compared.

The checked-in fixtures inform our implementation and tests. Temporary original
sources/binaries are deleted even if reference validation fails. Only declarations,
hashes, metadata and public behavior observations are exported as reference artifacts.

## Primary public documentation

* Vendor documentation for the reference library's DockingManager, layout model and
  XML layout serializer (entries under the root namespace pinned in contracts/reference.json):
  https://xceed.com/documentation/xceed-toolkit-plus-for-wpf/
* Uno windowing:
  https://platform.uno/docs/articles/features/windows-ui-xaml-window.html
* Uno SDK:
  https://platform.uno/docs/articles/uno-sdk.html
* WinUI ContentCoordinateConverter:
  https://learn.microsoft.com/en-us/windows/windows-app-sdk/api/winrt/microsoft.ui.content.contentcoordinateconverter
* NuGet trusted publishing:
  https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing

Commercial PLUS documentation may describe APIs absent from the requested repository.
The pinned public contracts define this implementation's compatibility target. This
is an engineering provenance record, not a legal opinion or a representation that a
staffed two-team clean-room process was used. See compatibility.md for open boundaries.

## Preview 2 validation tooling

The scanner enumerates inherited candidates without inspecting method bodies.
Individual type mappings and a conservative diagnostic baseline are committed. The
comparison keeps virtual flags, parameter/default values and attributes significant,
and does not translate type-looking strings inside constants. The original reference
contract remains unchanged. New behavior and diagnostic code is independently authored,
not decompiled. See parity-progress.md for evidence and gaps.

## Visual observations

Preview 7 uses independently captured public control geometry and auto-hide outputs from
the pinned original. The original program's screenshots were observed but are not bundled
as product resources. Our vectors, templates and layouts are independently authored.
See contracts/visual-fixtures/provenance.json and docs/visual-parity.md. Only application-
owned shared scene text, numeric observations and behavior XML are committed; no font
files are added.

## Navigator public observations

The independent NavigatorObservations probe renders application-owned document/tool
scenarios through public APIs. Raw measured XML from run 35817270444 is recorded with
input/probe/artifact/file hashes. The observed visibility transition is retained, not
hidden by screenshot-only assertions. Product templates and geometry are independently
authored. Uno framework source was consulted for platform API integration; the requested
original reference-library implementation bodies/templates/resources were not inspected.
