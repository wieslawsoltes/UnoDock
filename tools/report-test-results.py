"""Summarize executed JUnit reports without treating captures as test results.

This is a reporting tool, not a replacement for the test executables' exit codes.
It accepts only XML documents with testsuite/testcases and never derives test counts
from source code or a configured CI matrix.
"""
from __future__ import annotations
import argparse
import json
import os
from pathlib import Path
import xml.etree.ElementTree as ET


def collect(directory: Path) -> dict:
    suites = []
    for path in sorted(directory.rglob('*.xml')):
        try:
            root = ET.parse(path).getroot()
        except ET.ParseError as error:
            print(f'Unreadable XML {path}: {error}')
            continue
        if root.tag not in ('testsuite', 'testsuites'):
            continue
        for suite in ([root] if root.tag == 'testsuite' else root.iter('testsuite')):
            cases = suite.findall('testcase')
            if not cases:
                continue
            failures = []
            skipped = 0
            for case in cases:
                if case.find('skipped') is not None:
                    skipped += 1
                errors = case.findall('failure') + case.findall('error')
                if errors:
                    failures.append({'name': case.get('name', ''), 'details': '\n'.join(
                        (error.get('message', '') + '\n' + (error.text or '')).strip()
                        for error in errors)})
            suites.append({'name': suite.get('name', path.stem),
                           'file': path.relative_to(directory).as_posix(),
                           'executedCases': len(cases), 'passed': len(cases) - skipped - len(failures),
                           'skipped': skipped, 'failed': len(failures), 'failures': failures})
    return {'schema': 1, 'revision': os.environ.get('GITHUB_SHA'), 'suites': suites,
            'executedCases': sum(s['executedCases'] for s in suites),
            'passed': sum(s['passed'] for s in suites),
            'failed': sum(s['failed'] for s in suites),
            'skipped': sum(s['skipped'] for s in suites)}


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('directory', type=Path)
    parser.add_argument('--output', type=Path)
    args = parser.parse_args()
    report = collect(args.directory)
    lines = ['## Executed test reports', '', '| Suite | Passed | Failed | Skipped |', '| --- | ---: | ---: | ---: |']
    for suite in report['suites']:
        print(f"{suite['file']}: {suite['passed']}/{suite['executedCases']} passed, {suite['failed']} failed")
        lines.append(f"| {suite['name'].replace('|', '/')} | {suite['passed']} | {suite['failed']} | {suite['skipped']} |")
        for failure in suite['failures']:
            print('FAIL ' + failure['name'] + '\n' + failure['details'])
    print(json.dumps({key: value for key, value in report.items() if key != 'suites'}, sort_keys=True))
    if args.output:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(json.dumps(report, indent=2, ensure_ascii=False) + '\n', encoding='utf-8')
    if summary := os.environ.get('GITHUB_STEP_SUMMARY'):
        with open(summary, 'a', encoding='utf-8') as stream:
            stream.write('\n'.join(lines) + '\n')
    if not report['suites']:
        print('No executed JUnit suites were found; no pass claim is made.')


if __name__ == '__main__':
    main()
