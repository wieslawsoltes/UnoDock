#!/usr/bin/env python3
"""Test the real evidence gate; synthetic fixtures are not runtime acceptance."""
import importlib.util
import json
from pathlib import Path
import struct
import tempfile
import unittest
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[2]
SPEC = importlib.util.spec_from_file_location('inspector_gate', ROOT / 'tools/verify-native-inspector.py')
GATE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(GATE)
REVISION = '1' * 40


class NativeInspectorGateTests(unittest.TestCase):
    def setUp(self):
        temporary = tempfile.TemporaryDirectory()
        self.addCleanup(temporary.cleanup)
        self.directory = Path(temporary.name)

    def fixture(self, system='Linux'):
        (self.directory / 'source-revision.txt').write_text(REVISION)
        counts = {'native-inspector': 17 if system == 'Linux' else 16,
                  'inspector-quality': 35 if system == 'Linux' else 33,
                  'sample-quality': 29 if system == 'Linux' else 28}
        for suite, count in counts.items():
            directory = self.directory / suite
            directory.mkdir()
            root = ET.Element('testsuite', name=suite, tests=str(count), failures='0', skipped='0')
            names = sorted(GATE.NATIVE_CASES | ({GATE.POINTER_CASE} if system == 'Linux' else set())) if suite == 'native-inspector' else [f'case-{i}' for i in range(count)]
            for name in names:
                ET.SubElement(root, 'testcase', name=name)
            ET.ElementTree(root).write(directory / (suite + '.xml'))
            record = {'planned': [suite], 'completed': [{'suite': suite, 'error': None, 'passed': count, 'executed': count}]}
            (directory / 'isolated-execution.json').write_text(json.dumps(record))
        visuals = self.directory / 'native-inspector/visuals'
        visuals.mkdir()
        for mode in ('light', 'dark', 'rtl', 'narrow', 'invalid'):
            # Header fixture only: the verifier checks presence/size/basis, not pixels.
            data = b'\x89PNG\r\n\x1a\n' + struct.pack('>I', 13) + b'IHDR' + struct.pack('>II', 500, 500) + b'\x00' * 1500
            (visuals / f'native-inspector-{mode}.png').write_bytes(data)

    def mutate(self, action):
        path = self.directory / 'native-inspector/native-inspector.xml'
        tree = ET.parse(path)
        action(tree.getroot())
        tree.write(path)

    def test_all_platforms(self):
        for system in ('Linux', 'Windows', 'Darwin'):
            with self.subTest(system=system), tempfile.TemporaryDirectory() as path:
                self.directory = Path(path)
                self.fixture(system)
                result = GATE.verify(self.directory, REVISION, system)
                self.assertEqual(81 if system == 'Linux' else 77, result['executed'])

    def test_failed_assertion(self):
        self.fixture()
        self.mutate(lambda root: ET.SubElement(root[0], 'failure'))
        with self.assertRaises(ValueError):
            GATE.verify(self.directory, REVISION, 'Linux')

    def test_skipped_case(self):
        self.fixture()
        self.mutate(lambda root: ET.SubElement(root[0], 'skipped'))
        with self.assertRaises(ValueError):
            GATE.verify(self.directory, REVISION, 'Linux')

    def test_missing_original_behavior_suite(self):
        self.fixture()
        (self.directory / 'inspector-quality/inspector-quality.xml').unlink()
        with self.assertRaises(OSError):
            GATE.verify(self.directory, REVISION, 'Linux')

    def test_missing_named_behavior_despite_equal_count(self):
        self.fixture()
        self.mutate(lambda root: root[0].set('name', 'Unrelated passing case'))
        with self.assertRaises(ValueError):
            GATE.verify(self.directory, REVISION, 'Linux')

    def test_duplicate_case(self):
        self.fixture()
        self.mutate(lambda root: root[1].set('name', root[0].get('name')))
        with self.assertRaises(ValueError):
            GATE.verify(self.directory, REVISION, 'Linux')

    def test_partial_junit(self):
        self.fixture()
        self.mutate(lambda root: root.remove(root[0]))
        with self.assertRaises(ValueError):
            GATE.verify(self.directory, REVISION, 'Linux')

    def test_failed_process_despite_passing_assertions(self):
        self.fixture()
        path = self.directory / 'native-inspector/isolated-execution.json'
        record = json.loads(path.read_text())
        record['completed'][0]['error'] = 'Native process exited with 1'
        path.write_text(json.dumps(record))
        with self.assertRaises(ValueError):
            GATE.verify(self.directory, REVISION, 'Linux')

    def test_stale_revision(self):
        self.fixture()
        with self.assertRaises(ValueError):
            GATE.verify(self.directory, '2' * 40, 'Linux')

    def test_missing_capture(self):
        self.fixture()
        (self.directory / 'native-inspector/visuals/native-inspector-rtl.png').unlink()
        with self.assertRaises(OSError):
            GATE.verify(self.directory, REVISION, 'Linux')

    def test_malformed_capture(self):
        self.fixture()
        (self.directory / 'native-inspector/visuals/native-inspector-dark.png').write_bytes(b'not a screenshot')
        with self.assertRaises(ValueError):
            GATE.verify(self.directory, REVISION, 'Linux')

    def test_unknown_platform(self):
        with self.assertRaises(ValueError):
            GATE.verify(self.directory, REVISION, 'Unknown')


if __name__ == '__main__':
    unittest.main()
