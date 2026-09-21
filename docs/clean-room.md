# Provenance and reference process

Reference repository: https://github.com/xceedsoftware/wpftoolkit

Pinned revision: `2c71faba5eecc1b6ae6cd3d269408e0df37715d8`

Scope: `ExtendedWPFToolkitSolution/Src/Xceed.Wpf.AvalonDock`.

The isolated `Reference API inventory` workflow fetches the pinned revision into the
runner temporary directory. `tools/ApiScan` parses syntax with Roslyn and emits only
externally visible declarations and input file hashes. Method bodies, comments,
non-constant initializers, attributes, templates, and images are not emitted. Two
independent runs must produce byte-identical JSON. Temporary reference source is
deleted; it is never part of the implementation repository or package.

The inventory contains 996 sorted declarations in the default preprocessor profile.
This is not a semantic API compatibility certificate. Scanner limitations and strict
comparison behavior are recorded in compatibility.md. The property adapter generator
uses names/types to produce new dependency-property plumbing; implementation behavior
is in the independently authored partial classes.

Primary public behavior/platform references consulted:

* Xceed AvalonDock DockingManager and layout-model documentation:
  https://xceed.com/documentation/xceed-toolkit-plus-for-wpf/Xceed.Wpf.AvalonDock~Xceed.Wpf.AvalonDock.DockingManager.html
* Xceed XML layout serialization documentation:
  https://xceed.com/documentation/xceed-toolkit-plus-for-wpf/Xceed.Wpf.AvalonDock~Xceed.Wpf.AvalonDock.Layout.Serialization.XmlLayoutSerializer.html
* Uno windowing documentation:
  https://platform.uno/docs/articles/features/windows-ui-xaml-window.html
* Uno SDK and class-library templates:
  https://platform.uno/docs/articles/uno-sdk.html
* WinUI ContentCoordinateConverter API:
  https://learn.microsoft.com/en-us/windows/windows-app-sdk/api/winrt/microsoft.ui.content.contentcoordinateconverter
* NuGet trusted publishing:
  https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing

Public PLUS documentation can contain APIs absent from the pinned open repository.
The pinned inventory, rather than commercial documentation alone, identifies the
requested source surface. This process description is an engineering provenance
record, not a legal opinion or a representation of a staffed two-team clean room.
