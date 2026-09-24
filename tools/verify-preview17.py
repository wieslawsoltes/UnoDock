"""Extend the pinned delivery verifier with preview17 suite retention and regression proof.

Only read-only GitHub APIs and local evidence files are used. This never builds or
publishes the product, and failing rollback reports are labeled separately from the
successful acceptance reports. Run from the evidence branch with GH_TOKEN,
ACCEPTANCE_RUN, EXPECTED_REVISION and GITHUB_REPOSITORY set.
"""
from __future__ import annotations
import hashlib
import importlib.util
import io
import json
import os
from pathlib import Path, PurePosixPath
import shutil
import subprocess
import xml.etree.ElementTree as ET
from zipfile import ZipFile

VERSION = '0.1.0-preview.17'
BASELINE = 'f3fe686c5b77f2a6e6bcba6f2f84933a1ee0f92d'
BASELINE_RUN = 35979338359
PROOF_RUN = 35989202049
PROOF_WORKFLOW_REVISION = '48cfc8fd1010138fb8d7ce9111ff753aa0caa49d'
PROOF_PRODUCT_REVISION = 'ca03af24bfe78012f43fecde87739936fdc4931a'
REQUIRED_SUITES = {'source-ownership.xml': 48, 'source-identity.xml': 12, 'mvvm-chrome.xml': 5}


def require(value: bool, message: str) -> None:
    if not value:
        raise ValueError(message)


def main() -> None:
    helper_path = Path('tools/verify-preview16.py')
    helper_bytes = helper_path.read_bytes()
    helper_sha = hashlib.sha1(b'blob ' + str(len(helper_bytes)).encode() + b'\0' + helper_bytes).hexdigest()
    require(helper_sha == 'ca5ee13a9e44b3f06599c14956c76fcf334beee6', 'The base verifier changed')
    spec = importlib.util.spec_from_file_location('pinned_delivery_verifier', helper_path)
    require(spec is not None and spec.loader is not None, 'Cannot load pinned verifier')
    helper = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(helper)
    helper.VERSION = VERSION
    helper.main()

    root, delivery = Path('evidence'), Path('delivery')
    manifest = json.loads((delivery / 'verification.json').read_text())
    revision, repo = os.environ['EXPECTED_REVISION'], os.environ['GITHUB_REPOSITORY']
    manifest['verified'] = False
    manifest['extendedVerifier'] = 'tools/verify-preview17.py'
    manifest['baselineVerifierBlob'] = helper_sha

    def api(path: str):
        return json.loads(subprocess.check_output(['gh', 'api', f'repos/{repo}/' + path], timeout=120))

    def check_run(identifier: int, expected: str):
        run = api(f'actions/runs/{identifier}')
        require(run['head_sha'] == expected and run['status'] == 'completed' and run['conclusion'] == 'success', 'Invalid evidence run')
        (root / f'additional-run-{identifier}.json').write_text(json.dumps(run, indent=2) + '\n')

    def download(identifier: int, name: str) -> dict[str, bytes]:
        listing = api(f'actions/runs/{identifier}/artifacts?per_page=100')['artifacts']
        matches = [item for item in listing if item['name'] == name and not item['expired']]
        require(len(matches) == 1, 'Ambiguous artifact: ' + name)
        item = matches[0]
        data = subprocess.check_output(['gh', 'api', f'repos/{repo}/actions/artifacts/{item["id"]}/zip'], timeout=120)
        digest = hashlib.sha256(data).hexdigest()
        require(item.get('digest') == 'sha256:' + digest, 'Artifact digest mismatch: ' + name)
        manifest['artifacts'].append({'id': item['id'], 'name': name, 'run': identifier, 'sha256': digest})
        with ZipFile(io.BytesIO(data)) as archive:
            entries = archive.infolist()
            require(len(entries) == len({e.filename for e in entries}), 'Duplicate artifact path')
            require(sum(e.file_size for e in entries) <= 300_000_000, 'Unexpected artifact size')
            result = {}
            for entry in entries:
                path = PurePosixPath(entry.filename)
                require(not path.is_absolute() and '..' not in path.parts and '\\' not in entry.filename, 'Unsafe artifact path')
                require(path.suffix.lower() not in helper.FONT_SUFFIXES, 'Font file in evidence')
                if not entry.is_dir():
                    result[entry.filename] = archive.read(entry)
            return result

    def validate_cases(xml_bytes: bytes, json_bytes: bytes, allow_failures: bool = False):
        suite = ET.fromstring(xml_bytes)
        require(suite.tag == 'testsuite', 'Expected one executed suite')
        rows, cases = suite.findall('testcase'), json.loads(json_bytes)
        require(isinstance(cases, list) and len(rows) == len(cases) == int(suite.get('tests', '-1')), 'Case count mismatch')
        names = [case['Name'] for case in cases]
        require(len(names) == len(set(names)), 'Duplicate test name')
        failures = []
        for row, case in zip(rows, cases):
            failed = row.find('failure') is not None or row.find('error') is not None
            require(row.get('name') == case['Name'] and failed == (case['Error'] is not None), 'Case result mismatch')
            require(row.find('skipped') is None, 'Skipped case')
            if failed:
                failures.append(case['Name'])
        require(len(failures) == int(suite.get('failures', '-1')), 'Failure count mismatch')
        require(allow_failures or not failures, 'Failing acceptance case')
        return names, failures

    subprocess.run(['git', 'diff', '--exit-code', BASELINE, revision, '--', 'contracts', 'tools/ReferenceProbe',
                    'tools/ReferenceVisualProbe', 'tools/ReferenceSampleProbe', 'tools/compare-metadata.py'], check=True)
    manifest['referenceInputsUnchanged'] = True
    check_run(BASELINE_RUN, BASELINE)
    roster = {}
    for platform, artifact in [('linux', 'runtime-test-results'), ('windows', 'windows-runtime-test-results')]:
        previous = download(BASELINE_RUN, artifact)
        old_suites = {}
        for name, data in previous.items():
            if '/' in name or not name.endswith('.xml') or ET.fromstring(data).tag != 'testsuite':
                continue
            old_suites[name] = validate_cases(data, previous[name[:-4] + '.json'])[0]
            destination = root / 'prior-reports' / platform
            destination.mkdir(parents=True, exist_ok=True)
            (destination / name).write_bytes(data)
            (destination / (name[:-4] + '.json')).write_bytes(previous[name[:-4] + '.json'])
        current = {suite['file']: suite for suite in manifest['suites'][platform]}
        require(set(current) == set(old_suites) | set(REQUIRED_SUITES), 'Platform suite roster changed unexpectedly')
        for name, old_names in old_suites.items():
            path = root / platform / name
            new_names, _ = validate_cases(path.read_bytes(), path.with_suffix('.json').read_bytes())
            require(old_names == new_names, 'Existing executed case roster changed: ' + platform + '/' + name)
        additions = {}
        for name, count in REQUIRED_SUITES.items():
            path = root / platform / name
            new_names, _ = validate_cases(path.read_bytes(), path.with_suffix('.json').read_bytes())
            require(len(new_names) == count == current[name]['passed'], 'Missing new regression cases')
            additions[name] = count
        for scene in ('generic', 'light', 'dark', 'rtl'):
            for extension in ('png', 'xml'):
                require((root / platform / 'visuals' / f'mvvm-compact-{scene}.{extension}').is_file(), 'Missing compact MVVM capture')
        roster[platform] = {'previousSuites': len(old_suites), 'previousCasesPreserved': sum(map(len, old_suites.values())),
                            'addedSuites': additions, 'currentTotal': manifest['totals'][platform]}
    manifest['caseRosterRetention'] = roster

    check_run(PROOF_RUN, PROOF_WORKFLOW_REVISION)
    proof_files = download(PROOF_RUN, 'source-regression-proof')
    proof = json.loads(proof_files['regression-proof.json'])
    require(proof['productRevision'] == PROOF_PRODUCT_REVISION and proof['baselineFileRevision'] == BASELINE, 'Unexpected rollback inputs')
    subprocess.run(['git', 'diff', '--exit-code', PROOF_PRODUCT_REVISION, revision, '--', 'src', 'samples', 'tests'], check=True)
    source_path = 'src/UnoDock/DockingManager.Sources.cs'
    for label, ref in [('before', BASELINE), ('after', revision)]:
        digest = hashlib.sha256(subprocess.check_output(['git', 'show', ref + ':' + source_path])).hexdigest()
        require(digest == proof['sourceSha256'][label], 'Rollback source does not match final inputs')
    for suite in proof['suites']:
        name = suite['name']
        require(name + '.xml' in REQUIRED_SUITES and name != 'mvvm-chrome', 'Unexpected rollback suite')
        before_names, failures = validate_cases(proof_files[f'before/{name}.xml'], proof_files[f'before/{name}.json'], True)
        after_names, _ = validate_cases(proof_files[f'after/{name}.xml'], proof_files[f'after/{name}.json'])
        require(before_names == after_names and len(after_names) == suite['cases'], 'Rollback tests differ')
        require(failures == suite['regressionsProven'] and len(failures) == suite['baselineFailures'] > 0 and suite['newFailures'] == 0, 'Unproven regression report')
    require({s['name'] for s in proof['suites']} == {'source-ownership', 'source-identity'}, 'Incomplete rollback proof')
    for name, data in proof_files.items():
        destination = root / 'regression-proof' / name
        destination.parent.mkdir(parents=True, exist_ok=True)
        destination.write_bytes(data)
    manifest['regressionProof'] = {'run': PROOF_RUN, 'productRevision': PROOF_PRODUCT_REVISION, 'baselineFileRevision': BASELINE,
        'cases': sum(s['cases'] for s in proof['suites']), 'baselineFailures': sum(s['baselineFailures'] for s in proof['suites']),
        'newFailures': 0, 'scope': proof['scope']}
    manifest['verified'] = True
    for path in (root / 'verification.json', delivery / 'verification.json'):
        path.write_text(json.dumps(manifest, indent=2) + '\n')
    shutil.copy2(__file__, root / 'verify-preview17.py')
    shutil.copy2('.github/workflows/verify-preview17.yml', root / 'verification-workflow.yml')
    for scene in ('light', 'dark'):
        shutil.copy2(root / 'windows/visuals' / f'mvvm-compact-{scene}.png', delivery / f'UnoDock-preview17-mvvm-{scene}.png')
    shutil.make_archive(str(delivery / f'UnoDock-{VERSION}-validation'), 'zip', root)
    (delivery / 'README.txt').write_text(f'UnoDock {VERSION}\nMerged source: {revision}\n\n'
        'Complete source, NuGet packages/symbols, actual platform reports and PNG/XML captures.\n'
        'All previous executed Linux/Win32 cases are retained. New source-ownership, source-identity and mvvm-chrome suites are included.\n'
        'regression-proof/before contains intentionally failing runs using only the old UnoDock reconciliation file; these are not final-product failures.\n'
        'Read docs/source-ownership.md and docs/source-identity.md for contracts and limits.\n'
        'Product namespaces: UnoDock.*. No full API/behavior/pixel parity is asserted.\n'
        'Packages were built, not published to NuGet.org.\n', encoding='utf-8')
    print('EXTENDED VERIFIED ' + json.dumps({k: v for k, v in manifest.items() if k != 'suites'}, sort_keys=True))
    geometry = json.loads((root / 'sample-geometry-comparison.json').read_text())
    print('CLASSIC GEOMETRY ' + json.dumps({p: {s: {k: v[k] for k in ('passed', 'comparedRectangles', 'maxCoordinateDeltaDip', 'missing', 'extra', 'toleranceDip')}
        for s, v in scenes.items()} for p, scenes in geometry.items()}, sort_keys=True))


if __name__ == '__main__':
    main()
