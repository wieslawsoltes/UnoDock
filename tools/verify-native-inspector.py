#!/usr/bin/env python3
"""Require native inspector cases, unchanged behavior suites and process evidence."""
from __future__ import annotations

import argparse
import json
from pathlib import Path
import platform
import re
import struct
import xml.etree.ElementTree as ET

NATIVE_CASES = {
    'native inspector: native ListView containers and automation are used',
    'native inspector: row selection supplies the property description',
    'native inspector: editor focus and row selection remain synchronized',
    'native inspector: native category toggle updates collapse state',
    'native inspector: filtering collapses containers without blank rows',
    'native inspector: order changes preserve native containers and typed editors',
    'native inspector: presentation filtering does not implicitly commit a draft',
    'native inspector: presentation sorting cannot commit a focused draft',
    'native inspector: typed editors retain original parsing and conflict rules',
    'native inspector: retained old native editors cannot edit a replacement selection',
    'native inspector: disposing rejects retained row callbacks',
    *(f'native inspector: Fluent presentation retains editors: {mode}'
      for mode in ('light', 'dark', 'rtl', 'narrow', 'invalid')),
}
POINTER_CASE = 'XTEST: native inspector row click selects and describes the property'


def verify(directory: Path, revision: str, system: str | None = None) -> dict:
    system = platform.system() if system is None else system
    if system not in ('Linux', 'Windows', 'Darwin'):
        raise ValueError('Unsupported inspector acceptance platform: ' + system)
    if re.fullmatch(r'[0-9a-f]{40}', revision) is None:
        raise ValueError('An exact source revision is required.')
    if (directory / 'source-revision.txt').read_text().strip() != revision:
        raise ValueError('The inspector evidence belongs to a different revision.')
    minimums = {'native-inspector': 17 if system == 'Linux' else 16,
                'inspector-quality': 35 if system == 'Linux' else 33,
                'sample-quality': 29 if system == 'Linux' else 28}
    results = {}
    for suite, minimum in minimums.items():
        path = directory / suite
        root = ET.parse(path / (suite + '.xml')).getroot()
        cases = root.findall('testcase')
        names = [case.get('name', '') for case in cases]
        if (root.tag != 'testsuite' or root.get('name') != suite or
                len(cases) != int(root.get('tests', '-1')) or len(cases) < minimum or
                not all(names) or len(set(names)) != len(names)):
            raise ValueError('Missing, duplicate or incomplete cases: ' + suite)
        if (any(int(root.get(key, '0')) != 0 for key in ('failures', 'errors', 'skipped')) or
                any(case.find(tag) is not None for case in cases
                    for tag in ('failure', 'error', 'skipped'))):
            raise ValueError('A failed or skipped case is not passing acceptance: ' + suite)
        if suite == 'native-inspector':
            expected = NATIVE_CASES | ({POINTER_CASE} if system == 'Linux' else set())
            if not expected.issubset(names):
                raise ValueError('A required native behavior was not executed.')
        record = json.loads((path / 'isolated-execution.json').read_text())
        if record.get('planned') != [suite] or len(record.get('completed', [])) != 1:
            raise ValueError('Incomplete or repeated process execution: ' + suite)
        completed = record['completed'][0]
        if (completed.get('suite') != suite or completed.get('error') is not None or
                completed.get('passed') != len(cases) or completed.get('executed') != len(cases)):
            raise ValueError('Native process and JUnit records disagree: ' + suite)
        results[suite] = len(cases)
    for mode in ('light', 'dark', 'rtl', 'narrow', 'invalid'):
        data = (directory / 'native-inspector' / 'visuals' / f'native-inspector-{mode}.png').read_bytes()
        if (len(data) <= 1000 or data[:8] != b'\x89PNG\r\n\x1a\n' or data[12:16] != b'IHDR' or
                min(struct.unpack('>II', data[16:24])) <= 100):
            raise ValueError('Missing or malformed rendered capture: ' + mode)
    result = {'schema': 1, 'revision': revision, 'platform': system, 'suites': results,
              'executed': sum(results.values()), 'failed': 0, 'skipped': 0}
    (directory / 'native-inspector-acceptance.json').write_text(json.dumps(result, indent=2) + '\n')
    return result


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('directory', type=Path)
    parser.add_argument('--revision', required=True)
    arguments = parser.parse_args()
    try:
        print(json.dumps(verify(arguments.directory, arguments.revision), indent=2))
    except (OSError, ValueError, ET.ParseError, KeyError, TypeError) as error:
        parser.exit(1, f'Inspector acceptance incomplete: {error}\n')
