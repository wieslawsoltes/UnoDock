#!/usr/bin/env python3
"""Mutation tests for the independent native chrome acceptance gate."""
import contextlib
import importlib.util
import io
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch
import xml.etree.ElementTree as ET

SPEC = importlib.util.spec_from_file_location("chrome_gate", Path(__file__).with_name("verify-floating-chrome.py"))
GATE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(GATE)


class ChromeGateTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.directory = Path(self.temporary.name)

    def fixture(self, system="Linux"):
        completed = []
        for suite, (minimum, required) in GATE.contracts(system).items():
            names = sorted(required)
            names += [f"{suite}: additional existing case {i}" for i in range(minimum - len(names))]
            root = ET.Element("testsuite", name=suite, tests=str(len(names)), failures="0", errors="0", skipped="0")
            for name in names:
                ET.SubElement(root, "testcase", name=name)
            ET.ElementTree(root).write(self.directory / (suite + ".xml"), encoding="utf-8")
            completed.append({"suite": suite, "executed": len(names), "passed": len(names), "error": None})
        self.execution = {"schema": 1, "planned": [result["suite"] for result in completed], "completed": completed}
        self.save_execution()
        (self.directory / "source-revision.txt").write_text("a" * 40 + "\n")

    def save_execution(self):
        (self.directory / "isolated-execution.json").write_text(json.dumps(self.execution), encoding="utf-8")

    def verify(self, system="Linux"):
        with patch.object(GATE.platform, "system", return_value=system), contextlib.redirect_stdout(io.StringIO()):
            GATE.verify(self.directory)
        return json.loads((self.directory / "chrome-acceptance.json").read_text())

    def reject(self, system="Linux"):
        with self.assertRaises((ValueError, KeyError, TypeError, OSError, ET.ParseError)):
            self.verify(system)
        self.assertFalse((self.directory / "chrome-acceptance.json").exists())

    def mutate_xml(self, mutate, suite="floating-resize-policy-documents"):
        path = self.directory / (suite + ".xml")
        root = ET.parse(path).getroot()
        mutate(root)
        ET.ElementTree(root).write(path, encoding="utf-8")

    def test_full_matrix_on_each_platform(self):
        for system, count in (("Linux", 121), ("Windows", 121), ("Darwin", 79)):
            with self.subTest(system=system):
                self.fixture(system)
                self.assertEqual(count, self.verify(system)["passed"])

    def test_old_two_suite_matrix_is_not_enough(self):
        self.fixture()
        for path in self.directory.glob("floating-resize-policy-*.xml"):
            path.unlink()
        self.reject()

    def test_every_required_case_is_individually_required(self):
        for system in ("Linux", "Windows", "Darwin"):
            for suite, (_, required) in GATE.contracts(system).items():
                for name in required:
                    with self.subTest(system=system, suite=suite, case=name):
                        self.fixture(system)
                        self.mutate_xml(lambda root: next(case for case in root if case.get("name") == name).set("name", "unrelated replacement"), suite)
                        self.reject(system)

    def test_aggregate_and_case_failures_errors_skips_are_rejected(self):
        for tag, total in (("failure", "failures"), ("error", "errors"), ("skipped", "skipped")):
            for aggregate in (False, True):
                with self.subTest(tag=tag, aggregate=aggregate):
                    self.fixture()
                    self.mutate_xml(lambda root: root.set(total, "1") if aggregate else ET.SubElement(root[0], tag))
                    self.reject()

    def test_missing_empty_duplicate_and_wrong_suite_identities(self):
        for mutation in (lambda root: root.remove(root[0]), lambda root: root[0].set("name", ""),
                         lambda root: root[1].set("name", root[0].get("name")), lambda root: root.set("name", "other"),
                         lambda root: root.set("tests", "999")):
            self.fixture()
            self.mutate_xml(mutation)
            self.reject()

    def test_duplicate_or_missing_planned_and_completed_suites(self):
        for field in ("planned", "completed"):
            for mutation in (lambda values: values.append(values[0]), lambda values: values.pop(),
                             lambda values: values.__setitem__(1, values[0])):
                self.fixture()
                mutation(self.execution[field])
                self.save_execution()
                self.reject()

    def test_process_failure_or_disagreeing_counts_are_rejected(self):
        for field, value in (("error", "native host exited"), ("error", False), ("executed", 1), ("passed", 0), ("executed", True)):
            self.fixture()
            self.execution["completed"][0][field] = value
            self.save_execution()
            self.reject()

    def test_malformed_or_missing_revision(self):
        for revision in ("", "main", "a" * 39, "a" * 41, "z" * 40):
            self.fixture()
            (self.directory / "source-revision.txt").write_text(revision)
            self.reject()
        (self.directory / "source-revision.txt").unlink()
        self.reject()

    def test_failed_reverification_removes_prior_success_summary(self):
        self.fixture()
        self.verify()
        self.mutate_xml(lambda root: ET.SubElement(root[0], "failure"))
        self.reject()

    def test_unsupported_platform(self):
        self.fixture()
        self.reject("unknown")


if __name__ == "__main__":
    unittest.main(verbosity=2)
