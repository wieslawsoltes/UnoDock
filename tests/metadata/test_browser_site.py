#!/usr/bin/env python3
"""Static site checker fixtures; these do not replace browser runtime acceptance."""
from pathlib import Path
import importlib.util
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[2]
SPEC = importlib.util.spec_from_file_location('site_check', ROOT / 'tools/check-browser-site.py')
CHECK = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(CHECK)


class BrowserSiteTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        for path in ['index.html', 'docs/index.html', 'docs/getting-started.html',
                     'docs/browser-workspaces.html', 'docs/deployment.html',
                     'playground/index.html', 'gallery/index.html']:
            target = self.root / path
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_text('<!doctype html><title>Test fixture</title><main>Fixture</main>')
        for name in ['host.mjs', 'session.mjs']:
            (self.root / 'playground' / name).write_text('// static fixture')
        framework = self.root / 'gallery/_framework'
        framework.mkdir()
        (framework / 'dotnet.native.wasm').write_bytes(b'\0asm\x01\0\0\0')
        (self.root / 'revision.txt').write_text('a' * 40 + '\n')

    def test_complete_site(self):
        result = CHECK.verify(self.root)
        self.assertEqual(6, result['documents'])

    def test_missing_runtime(self):
        (self.root / 'gallery/_framework/dotnet.native.wasm').unlink()
        with self.assertRaises(ValueError):
            CHECK.verify(self.root)

    def test_missing_guide(self):
        (self.root / 'docs/browser-workspaces.html').unlink()
        with self.assertRaises(ValueError):
            CHECK.verify(self.root)

    def test_broken_local_link(self):
        (self.root / 'index.html').write_text('<title>Fixture</title><main><a href="missing.html">Missing</a></main>')
        with self.assertRaises(ValueError):
            CHECK.verify(self.root)

    def test_relative_link_and_fragment(self):
        (self.root / 'docs/index.html').write_text('<title>Fixture</title><main><a href="../playground/">Run</a><a href="getting-started.html#setup">Setup</a></main>')
        self.assertEqual(2, CHECK.verify(self.root)['localLinks'])

    def test_revision_required(self):
        (self.root / 'revision.txt').write_text('main')
        with self.assertRaises(ValueError):
            CHECK.verify(self.root)

    def test_semantic_landmarks_required(self):
        (self.root / 'index.html').write_text('<div>Untitled</div>')
        with self.assertRaises(ValueError):
            CHECK.verify(self.root)

    def test_external_links_are_not_local_files(self):
        (self.root / 'index.html').write_text('<title>Fixture</title><main><a href="https://example.com/docs">External</a></main>')
        self.assertEqual(0, CHECK.verify(self.root)['localLinks'])

    def test_root_relative_link_rejected_for_project_site(self):
        (self.root / 'index.html').write_text('<title>Fixture</title><main><a href="/docs/index.html">Wrong project base</a></main>')
        with self.assertRaises(ValueError):
            CHECK.verify(self.root)


if __name__ == '__main__':
    unittest.main()
