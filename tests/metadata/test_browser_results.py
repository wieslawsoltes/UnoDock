#!/usr/bin/env python3
"""Evidence parser tests. These fixtures are not real browser acceptance."""
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[2]
SPEC = importlib.util.spec_from_file_location('browser_results', ROOT / 'tools/verify-browser-results.py')
GATE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(GATE)


class BrowserResultsTests(unittest.TestCase):
    def setUp(self):
        temporary = tempfile.TemporaryDirectory()
        self.addCleanup(temporary.cleanup)
        self.directory = Path(temporary.name)
        self.names = sorted(GATE.REQUIRED)
        specs = [{'title': name, 'ok': True, 'tests': [{'results': [{'status': 'passed', 'retry': 0}]}]} for name in self.names]
        self.report = {'stats': {'expected': len(specs), 'unexpected': 0, 'skipped': 0, 'flaky': 0},
                       'errors': [], 'suites': [{'suites': [{'specs': specs}]}]}
        self.root = ET.Element('testsuite')
        for name in self.names:
            ET.SubElement(self.root, 'testcase', name=name)

    def verify(self):
        (self.directory / 'results.json').write_text(json.dumps(self.report), encoding='utf-8')
        ET.ElementTree(self.root).write(self.directory / 'results.xml')
        return GATE.verify(self.directory)

    def test_complete(self):
        self.assertEqual(len(self.names), self.verify()['passed'])

    def test_missing_required_native_scenario(self):
        self.root[0].set('name', 'Unrelated passing scenario')
        self.report['suites'][0]['suites'][0]['specs'][0]['title'] = 'Unrelated passing scenario'
        with self.assertRaises(ValueError):
            self.verify()

    def test_duplicate_identity(self):
        self.root[0].set('name', self.root[1].get('name'))
        with self.assertRaises(ValueError):
            self.verify()

    def test_case_failure_despite_green_summary(self):
        ET.SubElement(self.root[0], 'failure')
        with self.assertRaises(ValueError):
            self.verify()

    def test_partial_process_report(self):
        self.report['suites'][0]['suites'][0]['specs'][0]['tests'][0]['results'] = []
        with self.assertRaises(ValueError):
            self.verify()

    def test_successful_retry_is_not_first_attempt_evidence(self):
        self.report['suites'][0]['suites'][0]['specs'][0]['tests'][0]['results'][0]['retry'] = 1
        with self.assertRaises(ValueError):
            self.verify()

    def test_browser_failure_despite_junit(self):
        self.report['errors'] = [{'message': 'Native browser teardown timed out'}]
        with self.assertRaises(ValueError):
            self.verify()

    def test_disagreeing_json_identities(self):
        self.report['suites'][0]['suites'][0]['specs'][0]['title'] = 'Different scenario'
        with self.assertRaises(ValueError):
            self.verify()

    def test_skipped_summary(self):
        self.report['stats']['skipped'] = 1
        with self.assertRaises(ValueError):
            self.verify()


if __name__ == '__main__':
    unittest.main()
