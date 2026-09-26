#!/usr/bin/env python3
"""Require the pointer-independent initial geometry case in every chrome contract."""
import importlib.util
from pathlib import Path
import subprocess
import sys
import unittest

ROOT = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location('chrome_gate', ROOT / 'tools/verify-floating-chrome.py')
gate = importlib.util.module_from_spec(spec)
spec.loader.exec_module(gate)


class InitialGeometryContractTests(unittest.TestCase):
    def test_initial_geometry_is_required_on_all_hosts(self):
        for platform in ('Linux', 'Windows', 'Darwin'):
            with self.subTest(platform=platform):
                contracts = gate.contracts(platform)
                for kind, suite in (('document', 'floating-chrome-documents'), ('tools', 'floating-chrome-tools')):
                    _, required = contracts[suite]
                    self.assertIn(f'chrome/{kind}: initial client geometry settles without pointer input', required)
                    self.assertIn(f'chrome/{kind}: native frame is replaced and caption controls remain real', required)

    def test_existing_physical_resize_contract_remains(self):
        for platform in ('Linux', 'Windows'):
            contracts = gate.contracts(platform)
            for kind, suite in (('document', 'floating-chrome-documents'), ('tools', 'floating-chrome-tools')):
                _, required = contracts[suite]
                for edge in gate.EDGES:
                    self.assertIn(f'chrome/{kind}: physical pointer resizes {edge}', required)


if __name__ == '__main__':
    subprocess.run([sys.executable, str(ROOT / 'tools/test-floating-chrome-gate.py')], check=True, cwd=ROOT)
    unittest.main()
