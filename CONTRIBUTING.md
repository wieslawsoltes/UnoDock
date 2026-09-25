# Contributing to UnoDock

## Source layout and formatting

Keep one handwritten top-level type identity per C# file. Use the type name for
its primary file and `Type.Feature.cs` for partial implementations. Keep nested
types nested: moving one to namespace scope changes its public identity. Preserve
namespaces, accessibility, XAML contracts, conditional branches and linked includes.

The root `.editorconfig` documents four-space C# indentation, Allman braces,
expanded statements, UTF-8 and LF. The Roslyn source tool enforces a matching fixed
canonical layout; it does not dynamically read arbitrary EditorConfig options.
Change the tool and editor conventions together when changing repository style.

```sh
dotnet run --project tools/SourceMaintenance -c Release -- --check .
dotnet run --project tools/SourceMaintenance -c Release -- --check-format .
python3 tests/metadata/test_source_maintenance.py
python3 tests/metadata/test_source_formatting.py
python3 tools/generate-property-adapters.py
git diff --exit-code -- '*Properties.g.cs'
```

Use `--format` to expand and format handwritten code. It requires a compact-input
sentinel to expand correctly, verifies idempotence, and compares executable tokens
before and after under normal/Windows and Release/Debug parsing. The entire plan
is validated before writing. Literal contents must not change. Generated adapters
and pinned reference probes are excluded; change their generator, not its output.

`--apply` mechanically splits multiple top-level type identities into files. It
preserves nested declarations, compares declaration tokens in four configurations,
updates explicit Compile/Link entries, rejects file-local types requiring manual
work, and refuses to overwrite existing files. Review its move report and diff.
Original reference inventories, API mappings and comparator baselines are excluded.

## Validation

```sh
dotnet run --project tests/UnoDock.Core.Tests -c Release -- artifacts/core-tests
dotnet build samples/UnoDock.Gallery -c Release -f net10.0-desktop \
  -p:UnoDockTargetFrameworks=net10.0-desktop \
  -p:UnoDockLibraryFrameworks=net10.0 -warnaserror
```

The `layout-mutation-invariants` suite tests coherent ownership and reentrant
activation on the actual Uno UI thread. It participates in ordinary Linux and
selected Windows acceptance. Use the existing isolated runner and a fresh output
directory for focused tests. Physical-input tests need an explicit opt-in and a
dedicated display, not a normal interactive desktop.

Required CI includes ordinary regression, browser publish build, native WinUI and
package builds, native docking, custom chrome and source-quality checks. A passing
build or passing assertions alone do not prove successful native process completion.
Missing, failed, skipped or stale evidence cannot count as passing. Native platform
input-validation boundaries remain documented in the chrome and docking contracts.
