#!/usr/bin/env python3
"""Evidence parsing regressions for isolated, actual-host acceptance."""
import importlib.util
import io
import os
import sys
import json
import tempfile
import unittest
from unittest.mock import patch
from pathlib import Path
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location("desktop_runner", ROOT / "tools/run-desktop-tests.py")
runner = importlib.util.module_from_spec(spec)
spec.loader.exec_module(runner)


class DesktopRunnerTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(); self.addCleanup(self.temp.cleanup)
        self.directory = Path(self.temp.name)
        self.path = self.directory / "suite.xml"

    def suite(self, **attributes):
        root = ET.Element("testsuite", {"name": "suite", "tests": "2", "failures": "0", **attributes})
        ET.SubElement(root, "testcase", name="first"); ET.SubElement(root, "testcase", name="second")
        return root

    def verify(self, root):
        ET.ElementTree(root).write(self.path)
        return runner.verify_suite(self.path, "suite")

    def test_complete_results(self):
        self.assertEqual({"executed": 2, "passed": 2}, self.verify(self.suite()))

    def test_wrong_suite(self):
        with self.assertRaises(ValueError): self.verify(self.suite(name="different"))

    def test_partial_cases(self):
        root = self.suite(); root.remove(root[0])
        with self.assertRaises(ValueError): self.verify(root)

    def test_empty_suite(self):
        root = ET.Element("testsuite", name="suite", tests="0")
        with self.assertRaises(ValueError): self.verify(root)

    def test_duplicate_case(self):
        root = self.suite(); root[1].set("name", "first")
        with self.assertRaises(ValueError): self.verify(root)

    def test_failure_despite_zero_summary(self):
        root = self.suite(); ET.SubElement(root[0], "failure")
        with self.assertRaises(ValueError): self.verify(root)

    def test_error(self):
        root = self.suite(); ET.SubElement(root[0], "error")
        with self.assertRaises(ValueError): self.verify(root)

    def test_skip(self):
        root = self.suite(); ET.SubElement(root[0], "skipped")
        with self.assertRaises(ValueError): self.verify(root)

    def test_nonzero_summary(self):
        with self.assertRaises(ValueError): self.verify(self.suite(failures="1"))

    def test_manifest(self):
        path = self.directory / "manifest.json"; path.write_text('["suite", "another-suite"]')
        self.assertEqual(["suite", "another-suite"], runner.read_manifest(path))

    def test_bad_manifest(self):
        for value in [[], {}, ["../suite"], ["Suite"], [None], ["suite", "suite"]]:
            with self.subTest(value=value), self.assertRaises(ValueError):
                path = self.directory / "manifest.json"; path.write_text(json.dumps(value))
                runner.read_manifest(path)

    def test_stale_results_rejected_before_launch(self):
        self.path.write_text("old evidence")
        with self.assertRaises(ValueError): runner.run(self.path, self.directory, "all", "not-executed", 1, 1)

    def test_legacy_console_escapes_only_unrepresentable_characters(self):
        for encoding in ["cp1252", "ascii"]:
            for error in [False, True]:
                with self.subTest(encoding=encoding, error=error):
                    data = io.BytesIO()
                    with io.TextIOWrapper(data, encoding=encoding, newline="\n") as stream:
                        with patch.object(runner.sys, "stderr" if error else "stdout", stream):
                            runner.write_console("PASS infinity=\u221e arrow=\u2192", error=error)
                        self.assertEqual(b"PASS infinity=\\u221e arrow=\\u2192\n", data.getvalue())

    def test_utf8_console_preserves_unicode(self):
        data = io.BytesIO()
        with io.TextIOWrapper(data, encoding="utf-8", newline="\n") as stream:
            with patch.object(runner.sys, "stdout", stream):
                runner.write_console("PASS infinity=\u221e arrow=\u2192")
            self.assertEqual("PASS infinity=\u221e arrow=\u2192\n".encode("utf-8"), data.getvalue())

    def test_log_projection_preserves_raw_bytes_and_child_exit_status(self):
        raw = "infinity=\u221e arrow=\u2192\n".encode("utf-8")
        for code in [0, 7]:
            with self.subTest(code=code):
                data = io.BytesIO(); log = self.directory / f"child-{code}.log"
                with io.TextIOWrapper(data, encoding="cp1252", newline="\n") as stream:
                    with patch.object(runner.sys, "stdout", stream):
                        result = runner.execute([sys.executable, "-c",
                            f"import sys; sys.stdout.buffer.write({raw!r}); sys.exit({code})"],
                            os.environ.copy(), log, 10)
                    self.assertIn(b"infinity=\\u221e", data.getvalue())
                self.assertEqual(code, result)
                self.assertEqual(raw, log.read_bytes())


if __name__ == "__main__": unittest.main()
