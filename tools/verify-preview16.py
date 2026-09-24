"""Verify a pinned successful CI run, then assemble a source/package/evidence delivery.

This script does not build, execute product tests, publish NuGet packages, or substitute
configured counts for executed reports. It refuses ambiguous artifacts and wrong commits.
"""
from __future__ import annotations
import hashlib
import io
import json
import os
from pathlib import Path, PurePosixPath
import runpy
import shutil
import subprocess
import xml.etree.ElementTree as ET
from zipfile import ZipFile

VERSION = '0.1.0-preview.16'
FONT_SUFFIXES = {'.ttf', '.otf', '.ttc', '.woff', '.woff2'}


def main() -> None:
    repo = os.environ['GITHUB_REPOSITORY']
    run_id = int(os.environ['ACCEPTANCE_RUN'])
    reference_id = int(os.environ.get('REFERENCE_RUN', '35914057506'))
    revision = os.environ['EXPECTED_REVISION']
    expected_reference = '4bd5d97e3dc284ba74eff6dc3692ada571f44399'
    root = Path('evidence'); root.mkdir(exist_ok=True)
    manifest: dict = {'schema': 1, 'version': VERSION, 'sourceRevision': revision,
                      'acceptanceRun': run_id, 'referenceRun': reference_id,
                      'artifacts': [], 'suites': {}, 'publishedToNuGet': False}

    def api(path: str):
        return json.loads(subprocess.check_output(['gh', 'api', f'repos/{repo}/' + path]))

    run = api(f'actions/runs/{run_id}')
    reference = api(f'actions/runs/{reference_id}')
    if run['head_sha'] != revision or reference['head_sha'] != expected_reference:
        raise ValueError('Run/source or reference identity mismatch')
    jobs = api(f'actions/runs/{run_id}/jobs?per_page=100')['jobs']
    manifest['jobs'] = [{k: j[k] for k in ('name', 'status', 'conclusion')} for j in jobs]
    if run['status'] != 'completed' or run['conclusion'] != 'success' or len(jobs) != 6 or any(j['conclusion'] != 'success' for j in jobs):
        raise ValueError('Pinned product run is not completely successful')
    if reference['status'] != 'completed' or reference['conclusion'] != 'success':
        raise ValueError('Reference observation did not complete successfully')
    (root / 'acceptance-run.json').write_text(json.dumps(run, indent=2) + '\n')
    (root / 'reference-run.json').write_text(json.dumps(reference, indent=2) + '\n')

    def download(identifier: int, name: str, directory: Path):
        entries = api(f'actions/runs/{identifier}/artifacts?per_page=100')['artifacts']
        matches = [a for a in entries if a['name'] == name and not a['expired']]
        if len(matches) != 1:
            raise ValueError('Missing or ambiguous artifact: ' + name)
        item = matches[0]
        data = subprocess.check_output(['gh', 'api', f'repos/{repo}/actions/artifacts/{item["id"]}/zip'])
        digest = hashlib.sha256(data).hexdigest()
        if item.get('digest') != 'sha256:' + digest:
            raise ValueError('Artifact digest mismatch: ' + name)
        manifest['artifacts'].append({'id': item['id'], 'name': name, 'run': identifier, 'sha256': digest})
        directory.mkdir(parents=True, exist_ok=True)
        with ZipFile(io.BytesIO(data)) as archive:
            names = archive.namelist()
            if len(names) != len(set(names)):
                raise ValueError('Duplicate archive entry')
            if sum(i.file_size for i in archive.infolist()) > 300_000_000:
                raise ValueError('Unexpectedly large evidence artifact')
            for entry in archive.infolist():
                path = PurePosixPath(entry.filename)
                if path.is_absolute() or '..' in path.parts or '\\' in entry.filename:
                    raise ValueError('Unsafe artifact path')
                if entry.is_dir():
                    continue
                if path.suffix.lower() in FONT_SUFFIXES:
                    raise ValueError('Font files must not be included in delivery')
                destination = directory / path
                destination.parent.mkdir(parents=True, exist_ok=True)
                destination.write_bytes(archive.read(entry))

    for name, folder in [('core-and-api-results', 'core'), ('runtime-test-results', 'linux'),
                         ('windows-runtime-test-results', 'windows'), ('nuget-preview', 'packages')]:
        download(run_id, name, root / folder)
    download(reference_id, 'public-sample-reference', root / 'reference')
    if (root / 'core/source-revision.txt').read_text().strip() != revision:
        raise ValueError('Source export identifies another revision')
    subprocess.run(['git', 'fetch', 'origin', revision], check=True)
    records = subprocess.check_output(['git', 'ls-tree', '-r', '-z', revision]).split(b'\0')
    tracked = {}
    for record in records:
        if not record:
            continue
        metadata, name = record.split(b'\t', 1)
        mode, kind, sha = metadata.decode().split()
        if kind != 'blob' or mode not in ('100644', '100755'):
            raise ValueError('Unexpected source entry kind')
        tracked[name.decode()] = (mode, sha)
    source_path = root / 'core/UnoDock-source.zip'
    with ZipFile(source_path) as source:
        files = [entry for entry in source.infolist() if not entry.is_dir()]
        if len(files) != len(tracked) or {e.filename for e in files} != set(tracked):
            raise ValueError('Exported source file set differs from Git')
        for entry in files:
            data = source.read(entry); mode, expected = tracked[entry.filename]
            actual = hashlib.sha1(b'blob ' + str(len(data)).encode() + b'\0' + data).hexdigest()
            if actual != expected:
                raise ValueError('Source blob mismatch: ' + entry.filename)
            executable = bool((entry.external_attr >> 16) & 0o111)
            if executable != (mode == '100755'):
                raise ValueError('Source executable-mode mismatch: ' + entry.filename)
            if PurePosixPath(entry.filename).suffix.lower() in FONT_SUFFIXES:
                raise ValueError('Font file in source archive')
    manifest['sourceFiles'] = len(tracked)
    manifest['sourceTree'] = subprocess.check_output(['git', 'rev-parse', revision + '^{tree}'], text=True).strip()
    manifest['sourceSha256'] = hashlib.sha256(source_path.read_bytes()).hexdigest()
    # Reusing the unchanged public observation is only valid while its inputs stay
    # byte-identical. The observer is not silently rerun or rebound to this product.
    subprocess.run(['git', 'diff', '--exit-code', expected_reference, revision, '--',
                    'tools/ReferenceSampleProbe', 'contracts/reference.json'], check=True)

    package_files = sorted((root / 'packages').glob('*.*pkg'))
    if len(package_files) != 4:
        raise ValueError('Expected two packages and two symbol packages')
    manifest['packages'] = []
    for path in package_files:
        with ZipFile(path) as package:
            if any(PurePosixPath(n).suffix.lower() in FONT_SUFFIXES for n in package.namelist()):
                raise ValueError('Font file in package')
            nuspecs = [n for n in package.namelist() if n.endswith('.nuspec')]
            if len(nuspecs) != 1:
                raise ValueError('Missing or ambiguous package metadata')
            metadata = ET.fromstring(package.read(nuspecs[0])).find('{*}metadata')
            if metadata is None or metadata.findtext('{*}version') != VERSION:
                raise ValueError('Package version mismatch')
            repository = metadata.find('{*}repository')
            if repository is None or repository.get('commit') != revision:
                raise ValueError('Package/source commit mismatch')
            manifest['packages'].append({'file': path.name, 'id': metadata.findtext('{*}id'),
                                         'sha256': hashlib.sha256(path.read_bytes()).hexdigest()})

    for platform, directory in [('core', root / 'core/test-results'), ('linux', root / 'linux'), ('windows', root / 'windows')]:
        suites = []
        for path in sorted(directory.glob('*.xml')):
            document = ET.parse(path).getroot()
            if document.tag != 'testsuite':
                continue
            cases = document.findall('testcase')
            recorded = json.loads(path.with_suffix('.json').read_text())
            if not isinstance(recorded, list) or len(recorded) != len(cases):
                raise ValueError('Case count mismatch: ' + str(path))
            for case, result in zip(cases, recorded):
                failure = case.find('failure') is not None or case.find('error') is not None
                if case.get('name') != result['Name'] or failure != (result['Error'] is not None):
                    raise ValueError('JSON/JUnit case mismatch: ' + str(path))
                if case.find('skipped') is not None:
                    raise ValueError('Skipped cases are not accepted as passed')
            failed = sum(result['Error'] is not None for result in recorded)
            if int(document.get('tests', '-1')) != len(cases) or int(document.get('failures', '-1')) != failed:
                raise ValueError('JUnit aggregate mismatch')
            if failed:
                raise ValueError('Executed failing tests: ' + str(path))
            suites.append({'name': document.get('name'), 'file': path.name, 'passed': len(cases), 'failed': failed})
        if not suites:
            raise ValueError('No executed suites: ' + platform)
        if platform != 'core':
            names = {suite['file'] for suite in suites}
            if not {'restore-ownership.xml', 'mvvm-workspace.xml'}.issubset(names):
                raise ValueError('New suites did not execute: ' + platform)
            for scene in ('light', 'dark', 'rtl', 'dirty', 'restored'):
                for suffix in ('png', 'xml'):
                    if not (directory / 'visuals' / f'mvvm-workspace-{scene}.{suffix}').is_file():
                        raise ValueError('Missing actual MVVM capture')
        manifest['suites'][platform] = suites
        print(platform + ': ' + json.dumps(suites, sort_keys=True))
    manifest['totals'] = {platform: sum(s['passed'] for s in suites) for platform, suites in manifest['suites'].items()}
    metadata = json.loads((root / 'core/metadata-diff.json').read_text())
    manifest['api'] = {k: metadata[k] for k in ('referenceEntryCount', 'mappedSignatureMatches', 'unresolvedSignatureCount', 'attributeDifferenceCount', 'counts')}
    comparer = runpy.run_path('tools/compare-sample-geometry.py')['compare']
    comparisons = {platform: {scene: comparer(root / 'reference' / f'sample-reference-{scene}.xml',
                    root / platform / 'visuals' / f'sample-surface-{scene}.xml')
                    for scene in ('classic', 'selected-editor', 'rtl')} for platform in ('linux', 'windows')}
    (root / 'sample-geometry-comparison.json').write_text(json.dumps(comparisons, indent=2) + '\n')
    if not all(result['passed'] for scenes in comparisons.values() for result in scenes.values()):
        raise ValueError('Classic sample geometry regressed')
    manifest['classicGeometryPassed'] = True
    manifest['originalMvvmPixelEquivalenceAsserted'] = False
    manifest['runtimeScope'] = 'Uno Skia X11 and selected Win32; macOS build, browser publish, native WinUI package build only'
    manifest['verified'] = True
    (root / 'verification.json').write_text(json.dumps(manifest, indent=2) + '\n')
    print('VERIFIED ' + json.dumps({k: v for k, v in manifest.items() if k != 'suites'}, sort_keys=True))
    delivery = Path('delivery'); delivery.mkdir(exist_ok=True)
    shutil.copy2(source_path, delivery / f'UnoDock-{VERSION}-source.zip')
    shutil.make_archive(str(delivery / f'UnoDock-{VERSION}-packages'), 'zip', root / 'packages')
    source_path.unlink(); shutil.rmtree(root / 'packages')
    shutil.copy2(__file__, root / 'verify-preview16.py')
    shutil.copy2('tools/compare-sample-geometry.py', root / 'compare-sample-geometry.py')
    shutil.make_archive(str(delivery / f'UnoDock-{VERSION}-validation'), 'zip', root)
    shutil.copy2(root / 'verification.json', delivery / 'verification.json')
    (delivery / 'README.txt').write_text(f'UnoDock {VERSION}\nSource commit: {revision}\n\nComplete source, packages and symbols, actual platform tests and PNG/XML captures.\nProduct namespaces: UnoDock.*. Packages were built, not published to NuGet.org.\nRead docs/mvvm-workspace.md for implementation, persistence, validation and compatibility boundaries.\nThe classic sample geometry remains compared with unchanged original public observations.\nThe new MVVM captures are not a pixel-equivalence claim.\n', encoding='utf-8')


if __name__ == '__main__':
    main()
