#!/usr/bin/env python3
"""Require actual native/browser scenarios and complete first-attempt reports."""
from collections import Counter
import argparse
import json
from pathlib import Path
import xml.etree.ElementTree as ET

REQUIRED = {
    'acknowledged native typing preserves caret order without docking refreshes',
    'idle remote polling does not force repeated native layout projections',
    'real Uno runtime boots and renders the four model-backed editors',
    'native Uno text edits survive popup transfer and popup close',
    'two native browser windows exchange tools and documents without duplicate ownership',
    'lease guards reject stale writes after a content transfer',
    'DOM drag protocol docks into a requested edge and consumes its ticket once',
    'blocked popups retain source ownership and show recovery guidance',
    'reloading a satellite preserves its leased content',
    'journal recovery returns popup content to the reloaded primary',
    'an unhandled native error retires stale readiness and recovery preserves content with renewed leases',
    'repeated native window returns retain edited payloads without runtime faults',
    'failed destination is excluded and rejects transfer until native recovery',
    'failed primary blocks dock-all without partial return and resumes after recovery',
    'reloading destination is unavailable before native readiness and renews its leases',
}


def specifications(suites):
    for suite in suites:
        yield from suite.get('specs', [])
        yield from specifications(suite.get('suites', []))


def verify(directory: Path) -> dict:
    report = json.loads((directory / 'results.json').read_text(encoding='utf-8'))
    stats = report['stats']
    cases = list(ET.parse(directory / 'results.xml').getroot().iter('testcase'))
    names = [case.get('name', '') for case in cases]
    if not REQUIRED.issubset(names) or not all(names) or len(names) != len(set(names)):
        raise ValueError('Missing or duplicate required browser scenarios.')
    if stats['expected'] != len(cases) or any(stats[key] for key in ('unexpected', 'skipped', 'flaky')):
        raise ValueError('Browser reports contain failed, skipped, flaky or incomplete execution.')
    if report.get('errors') or any(case.find(tag) is not None for case in cases for tag in ('failure', 'error', 'skipped')):
        raise ValueError('A browser error cannot be overridden by the summary counts.')
    specs = list(specifications(report.get('suites', [])))
    if Counter(spec['title'] for spec in specs) != Counter(names):
        raise ValueError('JSON scenario identities do not match JUnit.')
    for spec in specs:
        tests = spec.get('tests', [])
        if not spec.get('ok') or len(tests) != 1:
            raise ValueError('A scenario did not complete one selected browser execution.')
        results = tests[0].get('results', [])
        if (len(results) != 1 or results[0].get('status') != 'passed' or
                results[0].get('retry', 0) != 0 or results[0].get('error') or results[0].get('errors')):
            raise ValueError('A scenario was retried, failed, or has no completed native execution.')
    return {'executed': len(cases), 'passed': len(cases), 'failed': 0, 'skipped': 0, 'retried': 0}


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('directory', type=Path)
    args = parser.parse_args()
    try:
        print(json.dumps(verify(args.directory), indent=2))
    except (OSError, ValueError, KeyError, TypeError, ET.ParseError) as error:
        parser.exit(1, f'Browser acceptance incomplete: {error}\n')
