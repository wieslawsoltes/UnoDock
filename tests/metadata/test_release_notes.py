import importlib.util
from pathlib import Path
import subprocess
import sys
import unittest

ROOT = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location('release_notes', ROOT / 'tools/release-notes.py')
notes = importlib.util.module_from_spec(spec)
spec.loader.exec_module(notes)

CHANGELOG = '# Changelog\n\nIntro.\n\n## 0.2.0\n\n- Second.\n\n## 0.1.0-preview.1\n\n- First.\n\n### Known limitations\n\n- One.\n'


class ReleaseNotesTests(unittest.TestCase):
    def test_section_stops_at_next_version(self):
        self.assertEqual('- Second.\n', notes.section(CHANGELOG, '0.2.0'))

    def test_last_section_keeps_subsections(self):
        self.assertEqual('- First.\n\n### Known limitations\n\n- One.\n', notes.section(CHANGELOG, '0.1.0-preview.1'))

    def test_missing_or_empty_section_fails(self):
        with self.assertRaises(ValueError):
            notes.section(CHANGELOG, '9.9.9')
        with self.assertRaises(ValueError):
            notes.section('## 1.0.0\n\n## 0.9.0\n- x\n', '1.0.0')

    def test_repository_changelog_has_current_version_notes(self):
        props = (ROOT / 'Directory.Build.props').read_text(encoding='utf-8')
        version = props.split('<Version>', 1)[1].split('</Version>', 1)[0]
        result = subprocess.run([sys.executable, str(ROOT / 'tools/release-notes.py'), 'v' + version], capture_output=True, text=True)
        self.assertEqual(0, result.returncode, result.stderr)
        self.assertIn('### Known limitations', result.stdout)


if __name__ == '__main__':
    unittest.main(verbosity=2)
