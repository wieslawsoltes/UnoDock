"""Discover, pack and verify every packable library under src/.

    python3 tools/nuget-packages.py list   [-p Name=Value ...]
    python3 tools/nuget-packages.py pack   --output DIR [--version V] [-p Name=Value ...]
    python3 tools/nuget-packages.py verify --output DIR [--version V] [--commit SHA] [-p Name=Value ...]

A project is packable when `src/<Name>/<Name>.csproj` evaluates IsPackable to
anything other than false. New libraries are therefore packed and verified
without workflow edits. Uno.Sdk projects are packed with Visual Studio MSBuild on
Windows, because the native WinUI XAML compiler requires that host; elsewhere,
and for plain SDK projects, `dotnet pack` is used. `-p` properties are passed to
evaluation and packing alike, e.g. `-p UnoDockLibraryFrameworks=net10.0` for a
local build without the Windows target framework.

Verification checks, for each package: identity and version, the expected
lib/<tfm>/ folders and primary assembly, README, icon, license expression,
repository URL and commit, sibling dependency versions, content files, the absence
of unexpected entries, and a matching .snupkg whose portable PDBs carry
deterministic SourceLink mappings for the same commit.
"""
import argparse
import json
import os
import pathlib
import re
import shutil
import subprocess
import sys
import xml.etree.ElementTree as ElementTree
import zipfile

ROOT = pathlib.Path(__file__).resolve().parents[1]
REPOSITORY_URL = 'https://github.com/wieslawsoltes/UnoDock'
LICENSE = 'MIT'
SEMVER = re.compile(r'(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)(?:-[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*)?')
OPC_ENTRIES = re.compile(r'(_rels/\.rels|\[Content_Types\]\.xml|package/services/metadata/core-properties/[0-9a-f]+\.psmdcp)')
FORBIDDEN = re.compile(r'(^|/)(obj|bin)/|\.(pdb|cs|csproj|props\.user|user|nupkg|snupkg)$|(^|/)(\.DS_Store|Thumbs\.db)$', re.IGNORECASE)


def run(command, **kwargs):
    print('+ ' + ' '.join(str(part) for part in command), flush=True)
    return subprocess.run(command, check=True, **kwargs)


def property_arguments(properties):
    return [f'-p:{value}' for value in properties]


def discover(properties):
    """Return packable projects with the metadata needed to pack and verify them."""
    names = ['IsPackable', 'PackageId', 'AssemblyName', 'TargetFramework', 'TargetFrameworks', 'UsingUnoSdk']
    projects = []
    for project in sorted((ROOT / 'src').glob('*/*.csproj')):
        command = ['dotnet', 'msbuild', str(project), '-nologo', *[f'-getProperty:{name}' for name in names],
                   *property_arguments(properties)]
        output = subprocess.run(command, check=True, capture_output=True, text=True, encoding='utf-8').stdout
        values = json.loads(output)['Properties']
        if values['IsPackable'].strip().lower() == 'false':
            continue
        frameworks = values['TargetFrameworks'] or values['TargetFramework']
        projects.append({
            'project': project.relative_to(ROOT).as_posix(),
            'packageId': values['PackageId'],
            'assemblyName': values['AssemblyName'],
            'frameworks': [item.strip() for item in frameworks.split(';') if item.strip()],
            'unoSdk': values['UsingUnoSdk'].strip().lower() == 'true',
        })
    if not projects:
        raise SystemExit('No packable projects found under src/.')
    return projects


def folder_name(framework):
    """Approximate NuGet's short folder name, e.g. net10.0-windows10.0.26100.0 -> net10.0-windows10.0.26100."""
    framework = framework.lower()
    base, _, platform = framework.partition('-')
    if not platform:
        return base
    match = re.fullmatch(r'([a-z]+)([0-9.]*)', platform)
    if not match or not match[2]:
        return framework
    parts = match[2].split('.')
    while len(parts) > 2 and parts[-1] == '0':
        parts.pop()
    return f'{base}-{match[1]}{".".join(parts)}'


def pack(arguments):
    output = pathlib.Path(arguments.output).resolve()
    output.mkdir(parents=True, exist_ok=True)
    common = ['-p:ContinuousIntegrationBuild=true', *property_arguments(arguments.property)]
    if arguments.version:
        common.append(f'-p:Version={arguments.version}')
    msbuild = shutil.which('msbuild') if os.name == 'nt' else None
    for project in discover(arguments.property):
        path = str(ROOT / project['project'])
        engine = arguments.engine
        if engine == 'auto':
            engine = 'msbuild' if project['unoSdk'] and os.name == 'nt' else 'dotnet'
        if engine == 'msbuild':
            if not msbuild:
                raise SystemExit(f'{project["project"]} requires Visual Studio MSBuild on PATH (microsoft/setup-msbuild).')
            run([msbuild, path, '-restore', '-t:Pack', '-nologo', '-v:minimal', '-p:Configuration=Release',
                 f'-p:PackageOutputPath={output}{os.sep}', *common])
        else:
            run(['dotnet', 'pack', path, '-c', 'Release', '-o', str(output), '-nologo', *common])


def local_name(element):
    return element.tag.rsplit('}', 1)[-1]


def child(element, name):
    return next((item for item in element if local_name(item) == name), None)


def text(element, name):
    item = child(element, name)
    return (item.text or '').strip() if item is not None else ''


def read_nuspec(archive):
    names = [name for name in archive.namelist() if '/' not in name and name.endswith('.nuspec')]
    if len(names) != 1:
        raise ValueError(f'expected one root .nuspec, found {names}')
    return names[0], child(ElementTree.fromstring(archive.read(names[0])), 'metadata')


def sourcelink(pdb):
    match = re.search(rb'\{\s*"documents"\s*:\s*\{.*?\}\s*\}', pdb, re.DOTALL)
    return json.loads(match[0]) if match else None


def verify_symbols(path, identity, frameworks, assembly, commit, errors):
    if not path.exists():
        errors.append(f'{identity}: missing symbol package {path.name}')
        return
    with zipfile.ZipFile(path) as archive:
        entries = [name for name in archive.namelist() if not name.endswith('/')]
        expected = {f'lib/{framework}/{assembly}.pdb' for framework in frameworks}
        for name in entries:
            if OPC_ENTRIES.fullmatch(name) or ('/' not in name and name.endswith('.nuspec')):
                continue
            if name not in expected:
                errors.append(f'{identity}: unexpected symbol package entry {name}')
        for name in sorted(expected):
            if name not in entries:
                errors.append(f'{identity}: symbol package lacks {name}')
                continue
            pdb = archive.read(name)
            if not pdb.startswith(b'BSJB'):
                errors.append(f'{identity}: {name} is not a portable PDB')
                continue
            mapping = sourcelink(pdb)
            prefix = 'https://raw.githubusercontent.com/' + REPOSITORY_URL.split('github.com/', 1)[1] + '/'
            targets = list((mapping or {}).get('documents', {}).items())
            if not targets:
                errors.append(f'{identity}: {name} has no SourceLink mapping')
            elif not all(key.startswith('/_/') for key, _ in targets):
                errors.append(f'{identity}: {name} is not a deterministic CI build (source paths are not mapped to /_/)')
            elif not all(value.startswith(prefix + commit + '/') for _, value in targets):
                errors.append(f'{identity}: {name} SourceLink does not point at {prefix}{commit}/')


def verify(arguments):
    directory = pathlib.Path(arguments.output)
    projects = {project['packageId']: project for project in discover(arguments.property)}
    packages = sorted(directory.glob('*.nupkg'))
    errors, rows, seen = [], [], set()
    if arguments.version and not SEMVER.fullmatch(arguments.version):
        errors.append(f'invalid semantic version {arguments.version}')
    versions = set()
    for package in packages:
        with zipfile.ZipFile(package) as archive:
            try:
                nuspec_name, metadata = read_nuspec(archive)
            except ValueError as error:
                errors.append(f'{package.name}: {error}')
                continue
            identity, version = text(metadata, 'id'), text(metadata, 'version')
            label = f'{identity} {version}'
            versions.add(version)
            if identity not in projects:
                errors.append(f'{package.name}: package {identity} does not correspond to a packable project')
                continue
            if identity in seen:
                errors.append(f'{package.name}: duplicate package {identity}')
            seen.add(identity)
            project = projects[identity]
            if package.name != f'{identity}.{version}.nupkg':
                errors.append(f'{package.name}: file name does not match {identity}.{version}.nupkg')
            if arguments.version and version != arguments.version:
                errors.append(f'{label}: expected version {arguments.version}')
            for name in ('authors', 'description', 'projectUrl', 'tags', 'copyright', 'releaseNotes'):
                if not text(metadata, name):
                    errors.append(f'{label}: missing <{name}>')
            if text(metadata, 'projectUrl') != REPOSITORY_URL:
                errors.append(f'{label}: projectUrl is not {REPOSITORY_URL}')
            license_element = child(metadata, 'license')
            if license_element is None or license_element.get('type') != 'expression' or (license_element.text or '').strip() != LICENSE:
                errors.append(f'{label}: license must be the SPDX expression {LICENSE}')
            entries = [name for name in archive.namelist() if not name.endswith('/')]
            readme, icon = text(metadata, 'readme'), text(metadata, 'icon')
            if not readme or readme not in entries or not archive.read(readme).strip():
                errors.append(f'{label}: README is not declared or not packed')
            if not icon or icon not in entries or not archive.read(icon).startswith(b'\x89PNG\r\n\x1a\n'):
                errors.append(f'{label}: PNG icon is not declared or not packed')
            repository = child(metadata, 'repository')
            commit = repository.get('commit', '') if repository is not None else ''
            if repository is None or repository.get('type') != 'git' or repository.get('url') != REPOSITORY_URL:
                errors.append(f'{label}: repository metadata must be git {REPOSITORY_URL}')
            if not re.fullmatch(r'[0-9a-f]{40}', commit):
                errors.append(f'{label}: repository commit is missing')
            elif arguments.commit and commit != arguments.commit:
                errors.append(f'{label}: repository commit {commit} is not {arguments.commit}')

            expected = {folder_name(framework) for framework in project['frameworks']}
            libraries = {name.split('/')[1].lower() for name in entries if name.startswith('lib/') and name.count('/') >= 2}
            if libraries != expected:
                errors.append(f'{label}: lib folders {sorted(libraries)} differ from target frameworks {sorted(expected)}')
            groups = child(metadata, 'dependencies')
            groups = [] if groups is None else [group for group in groups if local_name(group) == 'group']
            declared = {(group.get('targetFramework') or '').lower() for group in groups}
            if declared != expected:
                errors.append(f'{label}: dependency groups {sorted(declared)} differ from target frameworks {sorted(expected)}')
            for group in groups:
                for dependency in group:
                    if dependency.get('id') in projects and dependency.get('version') != version:
                        errors.append(f'{label}: dependency {dependency.get("id")} {dependency.get("version")} does not match {version}')
            content = child(metadata, 'contentFiles')
            patterns = [] if content is None else ['contentFiles/' + (item.get('include') or '') for item in content]
            assembly = project['assemblyName']
            for name in entries:
                parts = name.split('/')
                if FORBIDDEN.search(name):
                    errors.append(f'{label}: forbidden entry {name}')
                elif OPC_ENTRIES.fullmatch(name) or name in (nuspec_name, readme, icon):
                    continue
                elif parts[0] == 'lib' and len(parts) >= 3:
                    if len(parts) == 3 and parts[2].lower().endswith(('.dll', '.exe')) and parts[2] != assembly + '.dll':
                        errors.append(f'{label}: unexpected assembly {name}')
                elif parts[0] == 'contentFiles':
                    if name not in patterns:
                        errors.append(f'{label}: content file {name} is not declared in the nuspec')
                elif parts[0] in ('build', 'buildTransitive') and name.endswith(('.props', '.targets')):
                    continue
                else:
                    errors.append(f'{label}: unexpected entry {name}')
            lowered = {entry.lower() for entry in entries}
            for framework in sorted(libraries):
                if f'lib/{framework}/{assembly}.dll'.lower() not in lowered:
                    errors.append(f'{label}: lib/{framework}/ lacks {assembly}.dll')
            framework_folders = sorted(libraries)
        verify_symbols(package.with_suffix('.snupkg'), label, framework_folders, assembly, commit, errors)
        rows.append((identity, version, ', '.join(framework_folders), commit[:12]))
    for identity in sorted(set(projects) - seen):
        errors.append(f'{identity}: no package produced for {projects[identity]["project"]}')
    if len(versions) > 1:
        errors.append(f'packages disagree on version: {sorted(versions)}')
    for symbols in sorted(directory.glob('*.snupkg')):
        if not symbols.with_suffix('.nupkg').exists():
            errors.append(f'{symbols.name}: symbol package without a matching .nupkg')

    report = ['| Package | Version | Frameworks | Commit |', '|---|---|---|---|']
    report += [f'| {identity} | {version} | {frameworks} | {commit} |' for identity, version, frameworks, commit in rows]
    print('\n'.join(report), flush=True)
    if os.environ.get('GITHUB_STEP_SUMMARY'):
        with open(os.environ['GITHUB_STEP_SUMMARY'], 'a', encoding='utf-8') as summary:
            summary.write('### NuGet packages\n\n' + '\n'.join(report) + '\n\n')
    if errors:
        print('\nPackage verification failed:', file=sys.stderr)
        for error in errors:
            print('  - ' + error, file=sys.stderr)
        raise SystemExit(1)
    print(f'\nVerified {len(rows)} packages and symbol packages.')


def main():
    if hasattr(sys.stdout, 'reconfigure'):
        sys.stdout.reconfigure(encoding='utf-8')
        sys.stderr.reconfigure(encoding='utf-8')
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument('command', choices=['list', 'pack', 'verify'])
    parser.add_argument('--output', default='artifacts/packages', help='package directory')
    parser.add_argument('--version', help='package version (defaults to Directory.Build.props)')
    parser.add_argument('--commit', help='commit that repository metadata and SourceLink must reference')
    parser.add_argument('--engine', choices=['auto', 'dotnet', 'msbuild'], default='auto')
    parser.add_argument('-p', '--property', action='append', default=[], metavar='NAME=VALUE',
                        help='MSBuild global property for evaluation and packing')
    arguments = parser.parse_args()
    if arguments.command == 'list':
        print(json.dumps(discover(arguments.property), indent=2))
    elif arguments.command == 'pack':
        pack(arguments)
    else:
        verify(arguments)


if __name__ == '__main__':
    main()
