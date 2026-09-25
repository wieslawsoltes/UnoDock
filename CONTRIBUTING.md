# Contributing to UnoDock

## Source layout

Keep one handwritten top-level type identity per C# file. Use the type name for
its primary file and `Type.Feature.cs` for partial implementations. Nested types
remain nested: lifting one out of its declaring type changes its identity and is
not a cosmetic refactor. Preserve public names, accessibility, XAML contracts,
conditional-compilation branches, and linked-source project includes.

The root `.editorconfig` defines four-space C# indentation, Allman braces,
expanded statement/block layouts, UTF-8 and LF endings. The checked-in property
adapters are generated deterministically; change their generator rather than
hand-formatting generated output. Original reference inventories, mappings and
comparator baselines are not formatting inputs.

Run from the repository root with the repository's .NET 10 SDK:

```sh
dotnet run --project tools/SourceMaintenance -c Release -- --check .
dotnet format whitespace . --folder --include src samples tests tools \
  --exclude tools/ReferenceProbe --verify-no-changes
python3 tests/metadata/test_source_maintenance.py
python3 tools/generate-property-adapters.py
git diff --exit-code -- '*Properties.g.cs'
```

Omit `--verify-no-changes` to apply whitespace formatting. `SourceMaintenance
--apply` is a mechanical organizer, not a semantic refactoring engine. It checks
retained declaration tokens under normal/Windows and Release/Debug parsing,
updates explicit Compile/Link entries, rejects file-local types requiring manual
work, and refuses to overwrite existing files. Review its diff before committing.
Its artifact report records file moves; build all affected target frameworks.

## Validation

```sh
dotnet run --project tests/UnoDock.Core.Tests -c Release -- artifacts/core-tests
dotnet build samples/UnoDock.Gallery -c Release -f net10.0-desktop \
  -p:UnoDockTargetFrameworks=net10.0-desktop \
  -p:UnoDockLibraryFrameworks=net10.0 -warnaserror
```

The `layout-mutation-invariants` suite exercises parent/collection/slot consistency
and reentrant activation on the actual Uno UI thread. It joins ordinary Linux and
selected Windows acceptance. Use `tools/run-desktop-tests.py` with a fresh output
directory and its selector to run it separately. Native-input tests require an
explicit opt-in and a dedicated display; do not inject test input into an ordinary
interactive desktop.

Required CI includes the complete ordinary regression suite, browser publish
build, native WinUI/package builds, native desktop docking, custom floating chrome,
and source-quality checks. A successful build is not a substitute for runtime
acceptance. Missing, failed, skipped or stale test evidence must not be relabeled
as passing. Platform-specific validation boundaries remain documented in the
native chrome and docking contracts.
