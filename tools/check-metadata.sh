#!/usr/bin/env bash
# Inspect real built Uno metadata and gate new unresolved diagnostics.
set -euo pipefail
cd "$(dirname "$0")/.."
refs=$(mktemp -d)
trap 'rm -rf "$refs"' EXIT
mkdir -p artifacts
python3 tests/metadata/test_metadata.py 2>&1 | tee artifacts/metadata-tests.log
dotnet build src/UnoDock -c Release -f net10.0 -p:UnoDockLibraryFrameworks=net10.0
dotnet msbuild src/UnoDock/UnoDock.csproj -t:ExportApiReferences \
  -p:Configuration=Release -p:TargetFramework=net10.0 \
  -p:UnoDockLibraryFrameworks=net10.0 -p:ApiReferenceOutput="$refs"
assembly=src/UnoDock/bin/Release/net10.0/UnoDock.dll
dotnet run --project tools/ApiMetadata -c Release -- "$assembly" artifacts/unodock-metadata \
  --ref-dir "$refs" --profile net10.0-release
dotnet run --no-build --project tools/ApiMetadata -c Release -- "$assembly" "$refs/repeat" \
  --ref-dir "$refs" --profile net10.0-release
cmp artifacts/unodock-metadata.json "$refs/repeat.json"
python3 tools/compare-metadata.py artifacts/unodock-metadata.json --baseline contracts/metadata-baseline.json
