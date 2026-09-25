#!/usr/bin/env python3
"""Independent cases for the native host's fail-closed acceptance evidence gate."""
import contextlib
import importlib.util
import io
import json
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location("native_gate", ROOT / "tools/verify-native-results.py")
gate = importlib.util.module_from_spec(spec)
spec.loader.exec_module(gate)


class NativeGateTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.directory = Path(self.temp.name)
        self.addCleanup(self.temp.cleanup)

    def fixture(self, system):
        counts = {"desktop-floating": 38 if system == "Linux" else 34, "uno-theme": 10}
        if system == "Windows": counts["windows-floating-input"] = 8
        if system == "Darwin": counts["mac-native"] = 4
        for suite, count in counts.items():
            root = ET.Element("testsuite", name=suite, tests=str(count), failures="0")
            for i in range(count): ET.SubElement(root, "testcase", name=f"case-{i}", classname=suite)
            ET.ElementTree(root).write(self.directory / (suite + ".xml"))
        return sum(counts.values())

    def run_gate(self, system):
        with patch.object(gate.platform, "system", return_value=system), contextlib.redirect_stdout(io.StringIO()), contextlib.redirect_stderr(io.StringIO()):
            gate.verify(self.directory)

    def mutate(self, action):
        path = self.directory / "desktop-floating.xml"
        tree = ET.parse(path); action(tree.getroot()); tree.write(path)

    def test_linux_pass(self):
        expected = self.fixture("Linux"); self.run_gate("Linux")
        self.assertEqual(expected, json.loads((self.directory / "native-acceptance.json").read_text())["passed"])

    def test_windows_pass(self):
        expected = self.fixture("Windows"); self.run_gate("Windows")
        self.assertEqual(expected, json.loads((self.directory / "native-acceptance.json").read_text())["executed"])

    def test_mac_pass(self):
        expected = self.fixture("Darwin"); self.run_gate("Darwin")
        self.assertEqual(expected, json.loads((self.directory / "native-acceptance.json").read_text())["passed"])

    def test_assertion_failure(self):
        self.fixture("Darwin"); self.mutate(lambda root: ET.SubElement(root[0], "failure", message="failure despite process exit zero"))
        with self.assertRaises(RuntimeError): self.run_gate("Darwin")

    def test_error(self):
        self.fixture("Linux"); self.mutate(lambda root: ET.SubElement(root[0], "error"))
        with self.assertRaises(RuntimeError): self.run_gate("Linux")

    def test_skipped(self):
        self.fixture("Linux"); self.mutate(lambda root: ET.SubElement(root[0], "skipped"))
        with self.assertRaises(RuntimeError): self.run_gate("Linux")

    def test_missing_suite(self):
        self.fixture("Windows"); (self.directory / "windows-floating-input.xml").unlink()
        with self.assertRaises(RuntimeError): self.run_gate("Windows")

    def test_missing_mac_tracking_suite(self):
        self.fixture("Darwin"); (self.directory / "mac-native.xml").unlink()
        with self.assertRaises(RuntimeError): self.run_gate("Darwin")

    def test_empty_directory(self):
        with self.assertRaises(RuntimeError): self.run_gate("Linux")

    def test_partial_suite(self):
        self.fixture("Linux"); self.mutate(lambda root: root.remove(root[-1]))
        with self.assertRaises(RuntimeError): self.run_gate("Linux")

    def test_duplicate_cases(self):
        self.fixture("Linux"); self.mutate(lambda root: root[1].set("name", root[0].get("name")))
        with self.assertRaises(RuntimeError): self.run_gate("Linux")

    def test_malformed_xml(self):
        self.fixture("Linux"); (self.directory / "uno-theme.xml").write_text("<testsuite>")
        with self.assertRaises(ET.ParseError): self.run_gate("Linux")

    def test_unknown_platform(self):
        with self.assertRaises(RuntimeError): self.run_gate("Plan9")

    def test_zero_process_status_cannot_override_evidence(self):
        self.fixture("Darwin"); self.mutate(lambda root: ET.SubElement(root[0], "failure"))
        with patch.dict("os.environ", {"NATIVE_HOST_EXIT_CODE": "0"}), self.assertRaises(RuntimeError): self.run_gate("Darwin")


if __name__ == "__main__": unittest.main()
