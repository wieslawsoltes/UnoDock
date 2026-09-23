"""Read-only delivery verification. This file lives on a tooling branch, not in
product main. It consumes a pinned successful CI run and never publishes packages.
"""
from __future__ import annotations
import hashlib
import io
import json
import os
from pathlib import Path, PurePosixPath
import re
import subprocess
import sys
import xml.etree.ElementTree as ET
import zipfile

REVISION = '08611c5dbb199eedc6a98b0589f8e2b6d1e3a125'
RUN_ID = 35901417138
VERSION = '0.1.0-preview.13'
REPO = 'wieslawsoltes/UnoDock'
OUTPUT = Path(os.environ['RUNNER_TEMP']) / 'unodock-delivery'
OUTPUT.mkdir(parents=True, exist_ok=True)
CACHE = OUTPUT / 'input'
CACHE.mkdir(exist_ok=True)
FILES = OUTPUT / 'files'
FILES.mkdir(exist_ok=True)

def api(path: str) -> dict:
    return json.loads(subprocess.check_output(['gh', 'api', f'repos/{REPO}/{path}']))

def sha256(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()

def open_zip(data: bytes) -> zipfile.ZipFile:
    archive = zipfile.ZipFile(io.BytesIO(data))
    names = set()
    total = 0
    for item in archive.infolist():
        p = PurePosixPath(item.filename)
        assert not p.is_absolute() and '..' not in p.parts and '\\' not in item.filename, item.filename
        assert item.filename not in names, 'Duplicate ZIP entry: ' + item.filename
        names.add(item.filename)
        assert item.file_size < 100 * 1024 * 1024, item.filename
        total += item.file_size
        assert total < 200 * 1024 * 1024, 'Unexpected expanded archive size'
        assert p.suffix.lower() not in ('.ttf', '.otf', '.ttc', '.woff', '.woff2'), 'Font file excluded from delivery: ' + item.filename
    return archive

run = api(f'actions/runs/{RUN_ID}')
assert run['head_sha'] == REVISION and run['conclusion'] == 'success' and run['status'] == 'completed', 'Pinned product run is not green'
jobs = api(f'actions/runs/{RUN_ID}/jobs?per_page=100')['jobs']
assert len(jobs) == 6 and all(j['conclusion'] == 'success' for j in jobs), 'Not all six product jobs succeeded'
assert subprocess.check_output(['git', 'rev-parse', 'HEAD'], text=True).strip() == REVISION
artifacts = api(f'actions/runs/{RUN_ID}/artifacts?per_page=100')['artifacts']
requested = ('core-and-api-results', 'runtime-test-results', 'windows-runtime-test-results', 'nuget-preview')
archives: dict[str, zipfile.ZipFile] = {}
manifest: dict = {'repository': REPO, 'sourceRevision': REVISION, 'version': VERSION, 'ciRunId': RUN_ID,
                  'jobs': [{'name': j['name'], 'conclusion': j['conclusion']} for j in jobs], 'artifacts': [], 'tests': {},
                  'scope': {'runtime': ['Uno Skia X11', 'Uno Skia Win32 selected suites'],
                            'buildOnly': ['macOS desktop', 'native WinUI package target'],
                            'publishBuildOnly': ['browser gallery'], 'nugetOrgPublished': False,
                            'newPixelEquivalenceClaim': False, 'fullParityVerified': False}}
for name in requested:
    candidates = [a for a in artifacts if a['name'] == name]
    assert len(candidates) == 1, (name, len(candidates))
    artifact = candidates[0]
    assert not artifact['expired'] and artifact['workflow_run']['head_sha'] == REVISION
    assert artifact['size_in_bytes'] < 100 * 1024 * 1024
    path = CACHE / (name + '.zip')
    with path.open('wb') as dest:
        subprocess.run(['gh', 'api', f'repos/{REPO}/actions/artifacts/{artifact["id"]}/zip'], stdout=dest, check=True)
    data = path.read_bytes()
    assert len(data) == artifact['size_in_bytes']
    assert 'sha256:' + sha256(data) == artifact['digest'], name + ' artifact digest mismatch'
    archives[name] = open_zip(data)
    manifest['artifacts'].append({'id': artifact['id'], 'name': name, 'bytes': len(data), 'sha256': sha256(data)})

core = archives['core-and-api-results']
source_name = next(n for n in core.namelist() if PurePosixPath(n).name == 'UnoDock-source.zip')
source_data = core.read(source_name)
source = open_zip(source_data)
records = subprocess.check_output(['git', 'ls-tree', '-r', '-z', '--full-tree', 'HEAD']).split(b'\0')
tracked = {}
for record in records:
    if not record:
        continue
    metadata, name = record.split(b'\t', 1)
    mode, kind, digest = metadata.split()
    assert kind == b'blob', 'Submodules are not supported by delivery verification'
    tracked[name.decode('utf-8')] = (mode, digest.decode())
actual_files = {i.filename for i in source.infolist() if not i.is_dir()}
assert actual_files == set(tracked), 'Source ZIP does not match the committed file set'
for name, (mode, digest) in tracked.items():
    data = source.read(name)
    assert hashlib.sha1(b'blob ' + str(len(data)).encode() + b'\0' + data).hexdigest() == digest, name
    assert mode in (b'100644', b'100755'), 'Unexpected tracked mode: ' + name
    stored_mode = (source.getinfo(name).external_attr >> 16) & 0o777
    assert bool(stored_mode & 0o111) == (mode == b'100755'), 'Executable bit differs: ' + name
revision_file = next(n for n in core.namelist() if PurePosixPath(n).name == 'source-revision.txt')
assert core.read(revision_file).decode().strip() == REVISION
manifest['source'] = {'files': len(tracked), 'tree': subprocess.check_output(['git', 'rev-parse', 'HEAD^{tree}'], text=True).strip(),
                      'sha256': sha256(source_data), 'everyBlobAndExecutableBitVerified': True}
(FILES / f'UnoDock-{VERSION}-source.zip').write_bytes(source_data)

for label, archive in [('core', core), ('linux', archives['runtime-test-results']), ('windows', archives['windows-runtime-test-results'])]:
    suites = {}
    for name in sorted(archive.namelist()):
        if not name.endswith('.json'):
            continue
        data = json.loads(archive.read(name))
        if not isinstance(data, list) or not data or not all(isinstance(r, dict) and {'Name', 'Seconds', 'Error'} <= r.keys() for r in data):
            continue
        assert all(r['Error'] is None for r in data), label + ': ' + name
        junit = ET.fromstring(archive.read(name[:-5] + '.xml'))
        cases = junit.findall('testcase')
        assert int(junit.attrib['tests']) == len(data) == len(cases)
        assert int(junit.attrib['failures']) == 0 and all(c.find('failure') is None for c in cases)
        assert [r['Name'] for r in data] == [c.attrib['name'] for c in cases]
        suite = PurePosixPath(name).stem
        assert suite not in suites
        suites[suite] = {'passed': len(data), 'failed': 0, 'nativeXtestNames': [r['Name'] for r in data if 'XTEST' in r['Name']]}
    assert suites, 'No execution reports for ' + label
    manifest['tests'][label] = {'passed': sum(s['passed'] for s in suites.values()), 'failed': 0, 'suites': suites}
assert manifest['tests']['core']['passed'] == 120
assert manifest['tests']['linux']['suites']['dropdown-quality']['passed'] == 72
assert manifest['tests']['windows']['suites']['dropdown-quality']['passed'] == 66
python_log = next(n for n in core.namelist() if PurePosixPath(n).name == 'metadata-tests.log')
text = core.read(python_log).decode('utf-8-sig')
assert re.search(r'Ran 23 tests', text) and re.search(r'^OK\s*$', text, re.MULTILINE)
manifest['tests']['pythonComparator'] = {'passed': 23, 'failed': 0}

packages = archives['nuget-preview']
package_entries = [n for n in packages.namelist() if n.endswith(('.nupkg', '.snupkg'))]
assert len(package_entries) == 4
manifest['packages'] = []
for name in package_entries:
    data = packages.read(name)
    package = open_zip(data)
    spec = next(n for n in package.namelist() if n.endswith('.nuspec'))
    metadata = ET.fromstring(package.read(spec)).find('{*}metadata')
    assert metadata is not None
    assert metadata.findtext('{*}version') == VERSION
    repository = metadata.find('{*}repository')
    assert repository is not None and repository.attrib.get('commit') == REVISION, name
    manifest['packages'].append({'file': PurePosixPath(name).name, 'id': metadata.findtext('{*}id'), 'version': VERSION, 'commit': REVISION, 'sha256': sha256(data)})
(FILES / f'UnoDock-{VERSION}-packages.zip').write_bytes((CACHE / 'nuget-preview.zip').read_bytes())

validation_path = FILES / f'UnoDock-{VERSION}-validation.zip'
with zipfile.ZipFile(validation_path, 'w', zipfile.ZIP_DEFLATED) as output:
    for label, archive in [('core-api', core), ('linux', archives['runtime-test-results']), ('windows', archives['windows-runtime-test-results'])]:
        for name in sorted(archive.namelist()):
            if name.endswith('/') or name == source_name:
                continue
            output.writestr(label + '/' + name, archive.read(name))
    output.writestr('verification.json', json.dumps(manifest, indent=2) + '\n')
manifest_text = json.dumps(manifest, indent=2) + '\n'
(FILES / 'verification.json').write_text(manifest_text)
(FILES / 'verify-preview13-delivery.py').write_bytes(Path(__file__).read_bytes())
(FILES / 'README.txt').write_text('UnoDock preview 13\nSource revision: ' + REVISION + '\nCI run: ' + str(RUN_ID) + '\n\nThis bundle contains the complete source, NuGet libraries and symbols, and platform-separated tests/API reports.\nAll input artifact digests, committed source blobs/executable bits, package repository commits, and JSON/JUnit case records were checked.\nFull parity is not verified; no NuGet.org publication or new pixel-equivalence claim is made.\nSee verification.json and docs/dropdown-quality.md in the source.\n')
for platform, result in manifest['tests'].items():
    print(platform + ': ' + str(result['passed']) + ' passed, 0 failed')
print('Verified source files: ' + str(len(tracked)))
print('Verified package/symbol archives: ' + str(len(package_entries)))
print('All six jobs passed for ' + REVISION)
with open(os.environ['GITHUB_STEP_SUMMARY'], 'a') as summary:
    summary.write('## Verified UnoDock preview 13 delivery\n\n')
    summary.write('Source: `' + REVISION + '`; ' + str(len(tracked)) + ' files checked.\n\n')
    for platform, result in manifest['tests'].items():
        summary.write(platform + ': **' + str(result['passed']) + ' passed**, zero failures.\n\n')
