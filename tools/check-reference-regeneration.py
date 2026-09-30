"""Check that regenerated reference inventories and fixtures match the recorded contracts.

Declarations, records, digests and ordering must match exactly (line endings aside).
The only tolerated difference is the hash of each input assembly that the workflow
compiles from the pinned sources: those bytes depend on the runner's build paths and
compiler, so the recorded values stay as frozen provenance. The set of inputs and the
hashes of the pinned framework reference assemblies must still match.
"""
import json
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parent.parent


def read(path: Path) -> bytes:
    return path.read_bytes().replace(b'\r\n', b'\n')


def main(generated: Path, built: set[str]) -> int:
    failures = []
    for name in ('reference-metadata-release', 'reference-metadata-debug'):
        actual_txt, expected_txt = read(generated / f'{name}.txt'), read(ROOT / f'contracts/{name}.txt')
        if actual_txt != expected_txt:
            failures.append(f'{name}.txt')
        actual, expected = json.loads(read(generated / f'{name}.json')), json.loads(read(ROOT / f'contracts/{name}.json'))
        actual_hashes, expected_hashes = actual.pop('inputHashes'), expected.pop('inputHashes')
        if list(actual_hashes) != list(expected_hashes):
            failures.append(f'{name}.json inputs')
        for key in actual_hashes.keys() - built:
            if actual_hashes[key] != expected_hashes.get(key):
                failures.append(f'{name}.json input {key}')
        if actual != expected:
            failures.append(f'{name}.json')
    fixtures = generated / 'reference-fixtures'
    if fixtures.is_dir():
        recorded = sorted(p.name for p in (ROOT / 'contracts/reference-fixtures').glob('*.xml'))
        if sorted(p.name for p in fixtures.glob('*.xml')) != recorded:
            failures.append('reference-fixtures set')
        failures += [f'reference-fixtures/{name}' for name in recorded
                     if (fixtures / name).exists() and read(fixtures / name) != read(ROOT / 'contracts/reference-fixtures' / name)]
    for failure in failures:
        print(f'::error::Regenerated {failure} differs from contracts/', file=sys.stderr)
    print('Regenerated reference inventories and fixtures match contracts/' if not failures else f'{len(failures)} difference(s)')
    return 1 if failures else 0


if __name__ == '__main__':
    if len(sys.argv) < 2:
        raise SystemExit('Usage: check-reference-regeneration.py <generated-directory> [built-assembly.dll]...')
    sys.exit(main(Path(sys.argv[1]), set(sys.argv[2:])))
