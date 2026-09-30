#!/usr/bin/env python3
"""Suite-specific acceptance evidence for the consolidated desktop CI job.

Each check requires a complete, first-attempt, isolated run of its suite (JUnit
cases, the runner's execution record) plus the named cases and rendered
captures that suite is expected to produce on this platform.
"""
import json
import os
import platform
import struct
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

RUNNER_OS = os.environ.get('RUNNER_OS') or {'Darwin': 'macOS'}.get(platform.system(), platform.system())


def fluent_navigator(directory: Path) -> None:
    path = directory
    root = ET.parse(path / 'fluent-navigator.xml').getroot()
    cases = root.findall('testcase')
    names = [case.get('name', '') for case in cases]
    expected = {
        f'Fluent navigator: {theme} {name}'
        for theme in ('Light', 'Dark')
        for name in (
            'native button composition preserves ListBox contracts',
            'preview moves indicator without activating editors',
            'native Invoke commits the requested command exactly once',
            'vetoed native action cannot fall back to model activation',
            'consumer selection brushes and active hover refresh retain identity',
            'theme and density changes retain rows, sources and editors',
            'Generic round trip retains containers and legacy geometry',
            'pending native action cannot revive a closed session')
    }
    expected.update(f'Fluent navigator: {theme} tool action honors eligibility={allowed}'
                    for theme in ('Light', 'Dark') for allowed in ('False', 'True'))
    expected.update('Fluent navigator: ' + name for name in (
        'far-row reveal follows arranged native row sizes',
        'reentrant command selection cannot activate an obsolete native row',
        'corner overrides are scoped, finite and bounded'))
    for scene in ('light', 'dark', 'rtl', 'large-text', 'narrow'):
        expected.add('Fluent navigator: rendered native selection and frame: ' + scene)
        data = (path / 'visuals' / ('fluent-navigator-' + scene + '.png')).read_bytes()
        assert len(data) > 1000 and data[:8] == b'\x89PNG\r\n\x1a\n'
        assert data[12:16] == b'IHDR' and min(struct.unpack('>II', data[16:24])) > 100
    if RUNNER_OS == 'Linux':
        expected.add('XTEST Fluent navigator: a real row click commits only its target')
        expected.add('XTEST Fluent navigator: held pointer previews native pressed state before one release commit')
    assert root.tag == 'testsuite' and root.get('name') == 'fluent-navigator'
    assert len(cases) == int(root.get('tests', '-1')) and len(cases) >= len(expected)
    assert all(names) and len(names) == len(set(names)) and expected.issubset(names)
    assert not any(int(root.get(key, '0')) for key in ('failures', 'errors', 'skipped'))
    assert not any(case.find(tag) is not None for case in cases for tag in ('failure', 'error', 'skipped'))
    record = json.loads((path / 'isolated-execution.json').read_text())
    assert record['planned'] == ['fluent-navigator'] and len(record['completed']) == 1
    completed = record['completed'][0]
    assert completed['suite'] == 'fluent-navigator' and completed['error'] is None
    assert completed['executed'] == completed['passed'] == len(cases)
    print('Verified', len(cases), 'executed navigator cases and five captures.')


def fluent_presentation(directory: Path) -> None:
    p = directory
    root = ET.parse(p / 'fluent-presentation.xml').getroot()
    cases = root.findall('testcase')
    names = [case.get('name', '') for case in cases]
    assert root.tag == 'testsuite' and root.get('name') == 'fluent-presentation'
    assert len(cases) == int(root.get('tests', '-1')) and len(cases) >= (43 if RUNNER_OS == 'Linux' else 40)
    assert all(names) and len(set(names)) == len(names)
    assert not any(int(root.get(k, '0')) for k in ('failures', 'errors', 'skipped'))
    assert not any(case.find(tag) is not None for case in cases for tag in ('failure', 'error', 'skipped'))
    for scene in ('light', 'dark', 'rtl', 'large-text', 'narrow'):
        assert 'Fluent: rendered workspace retains accessible controls: ' + scene in names
        image = p / 'visuals' / ('fluent-workspace-' + scene + '.png')
        assert image.is_file() and image.stat().st_size > 1000
        assert image.read_bytes()[:8] == b'\x89PNG\r\n\x1a\n'
    assert 'Fluent: resource bindings survive repeated themes and a Generic round trip' in names
    assert 'Fluent: native Button states resolve scoped palette brushes without copying them' in names
    assert 'Fluent: native menu states retain scoped application brush identities' in names
    for kind in ('button', 'menu'):
        for state in ('PointerOver', 'Pressed'):
            for change in ('theme change', 'palette replacement'):
                assert f'Fluent resources: active {kind} {state} follows {change}' in names
    for name in (
        'construction preserves application theme dictionaries',
        'application state overrides remain authoritative in Light',
        'application state overrides remain authoritative in Dark',
        'replacing a control resource scope retains the application scope',
        'repeated Generic round trips release only the owned fallback',
        'native state callbacks may supersede palette replay',
    ):
        assert 'Fluent resources: ' + name in names
    if RUNNER_OS == 'Linux':
        assert 'XTEST Fluent: live palette repaint preserves held input and activates only on release' in names
        for mode in ('Light', 'Generic'):
            assert 'XTEST Fluent: keyboard press uses ButtonBase state and activates once in ' + mode in names
    record = json.loads((p / 'isolated-execution.json').read_text())
    assert record['planned'] == ['fluent-presentation'] and len(record['completed']) == 1
    result = record['completed'][0]
    assert result['suite'] == 'fluent-presentation' and result['error'] is None
    assert result['passed'] == result['executed'] == len(cases)
    assert sum(name.startswith('Fluent resources: Gallery frame') for name in names) == 2
    states = ET.parse(p / 'states' / 'fluent-state-resources.xml').getroot()
    state_cases = states.findall('testcase')
    state_names = [case.get('name', '') for case in state_cases]
    assert states.tag == 'testsuite' and states.get('name') == 'fluent-state-resources'
    assert len(state_cases) == int(states.get('tests', '-1')) and len(state_cases) >= 30
    assert all(state_names) and len(set(state_names)) == len(state_names)
    assert not any(int(states.get(k, '0')) for k in ('failures', 'errors', 'skipped'))
    assert not any(case.find(tag) is not None for case in state_cases for tag in ('failure', 'error', 'skipped'))
    for kind in ('button', 'menu'):
        for mode in ('Light', 'Dark'):
            for scope in ('direct', 'theme', 'merged-theme', 'shared-theme', 'default'):
                assert f'Fluent resources: {kind}/{mode}/{scope} consumer states remain authoritative' in state_names
        for contract in ('contrast dictionary and unrelated entries are not replaced',
                         'changing the resource owner retires only the old forwarding dictionary',
                         'Generic round trips remove forwarding but retain consumer dictionaries',
                         'adding and removing a consumer state is reconciled on refresh'):
            assert f'Fluent resources: {kind} {contract}' in state_names
    for mode in ('Light', 'Dark'):
        assert f'Fluent resources: button/{mode} vector glyph follows native foreground states' in state_names
    execution = json.loads((p / 'states' / 'isolated-execution.json').read_text())
    assert execution['planned'] == ['fluent-state-resources'] and len(execution['completed']) == 1
    actual = execution['completed'][0]
    assert actual['suite'] == 'fluent-state-resources' and actual['error'] is None
    assert actual['executed'] == actual['passed'] == len(state_cases)
    print('Verified', len(cases), 'Fluent presentation cases,', len(state_cases), 'resource ownership cases, and five captures.')


def docking_sizing(directory: Path) -> None:
    p = directory
    root = ET.parse(p / 'docking-sizing.xml').getroot()
    cases = root.findall('testcase')
    names = [case.get('name', '') for case in cases]
    assert root.tag == 'testsuite' and root.get('name') == 'docking-sizing'
    assert len(cases) == int(root.get('tests', '-1')) and len(cases) >= 52
    assert all(names) and len(set(names)) == len(names)
    assert not any(int(root.get(k, '0')) for k in ('failures', 'errors', 'skipped'))
    assert not any(case.find(tag) is not None for case in cases for tag in ('failure', 'error', 'skipped'))
    for horizontal in (True, False):
        for rtl in (True, False):
            assert sum(f'H={horizontal}, RTL={rtl}' in name for name in names) >= 13
    record = json.loads((p / 'isolated-execution.json').read_text())
    assert record['planned'] == ['docking-sizing'] and len(record['completed']) == 1
    result = record['completed'][0]
    assert result['suite'] == 'docking-sizing' and result['error'] is None
    assert result['passed'] == result['executed'] == len(cases)
    print('Verified', len(cases), 'actual-host multi-pane docking and sizing cases.')


def xaml_workbench(directory: Path) -> None:
    path = directory
    root = ET.parse(path / 'xaml-workbench.xml').getroot()
    cases = root.findall('testcase')
    assert root.tag == 'testsuite' and root.get('name') == 'xaml-workbench'
    assert len(cases) == int(root.get('tests', '-1')) and len(cases) >= 86
    assert len({case.get('name') for case in cases}) == len(cases)
    assert sum(case.get('name', '').startswith('XAML cleanup:') for case in cases) >= 16
    assert sum('in-flight model event' in case.get('name', '') for case in cases) == 2
    assert not any(int(root.get(key, '0')) for key in ['failures', 'errors', 'skipped'])
    assert not any(case.find(tag) is not None for case in cases for tag in ['failure', 'error', 'skipped'])
    record = json.loads((path / 'isolated-execution.json').read_text())
    assert record['planned'] == ['xaml-workbench'] and len(record['completed']) == 1
    result = record['completed'][0]
    assert result['suite'] == 'xaml-workbench' and result['error'] is None
    assert result['executed'] == result['passed'] == len(cases)
    captures = ['xaml-workbench-light', 'xaml-workbench-dark',
                'xaml-policies-light', 'xaml-policies-dark', 'xaml-workbench-narrow']
    for capture in captures:
        image = path / 'visuals' / (capture + '.png')
        assert image.is_file() and image.stat().st_size > 1000
        assert image.read_bytes()[:8] == b'\x89PNG\r\n\x1a\n'
    print('Verified', len(cases), 'real-host XAML cases, cleanup regressions and all five presentation captures.')


def xaml_workspaces(directory: Path) -> None:
    p = directory
    r = ET.parse(p / 'xaml-workspaces.xml').getroot()
    cases = r.findall('testcase')
    assert r.tag == 'testsuite' and r.get('name') == 'xaml-workspaces'
    assert len(cases) == int(r.get('tests', '-1')) and len(cases) >= 37
    assert all(c.get('name') for c in cases) and len({c.get('name') for c in cases}) == len(cases)
    assert not any(int(r.get(k, '0')) for k in ('failures', 'errors', 'skipped'))
    assert not any(c.find(tag) is not None for c in cases for tag in ('failure', 'error', 'skipped'))
    execution = json.loads((p / 'isolated-execution.json').read_text())
    assert execution['planned'] == ['xaml-workspaces'] and len(execution['completed']) == 1
    result = execution['completed'][0]
    assert result['suite'] == 'xaml-workspaces' and result['error'] is None
    assert result['executed'] == result['passed'] == len(cases)
    for kind in ('declarative', 'mvvm', 'templates', 'palette'):
        for theme in ('light', 'dark'):
            data = (p / f'xaml-{kind}-{theme}.png').read_bytes()
            assert len(data) > 1000 and data[:8] == b'\x89PNG\r\n\x1a\n'
    print('Verified', len(cases), 'actual-host consumer workspace cases and eight captures.')


CHECKS = {
    'fluent-navigator': fluent_navigator,
    'fluent-presentation': fluent_presentation,
    'docking-sizing': docking_sizing,
    'xaml-workbench': xaml_workbench,
    'xaml-workspaces': xaml_workspaces,
}


if __name__ == "__main__":
    if len(sys.argv) != 3 or sys.argv[1] not in CHECKS:
        raise SystemExit("usage: verify-suite-evidence.py {" + ",".join(CHECKS) + "} <directory>")
    CHECKS[sys.argv[1]](Path(sys.argv[2]))
