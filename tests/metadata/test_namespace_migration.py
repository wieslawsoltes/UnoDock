"""Keep the intentional namespace break distinct from claimed compatibility gains."""
import hashlib
import json
from pathlib import Path
import re
import unittest

ROOT = Path(__file__).resolve().parents[2]
POLICY = json.loads((ROOT / 'contracts/namespace-migration.json').read_text())

def read(name):
    return json.loads((ROOT / name).read_text())

class NamespaceMigrationTests(unittest.TestCase):
    def test_original_inventory_and_comparator_bytes(self):
        for name, digest in POLICY['originalFiles'].items():
            with self.subTest(file=name):
                self.assertEqual(digest, hashlib.sha256((ROOT / name).read_bytes()).hexdigest())

    def test_diagnostic_allowlist_unchanged(self):
        diagnostics = read('contracts/metadata-baseline.json')['diagnostics']
        digest = hashlib.sha256(json.dumps(diagnostics,ensure_ascii=False,separators=(',',':')).encode()).hexdigest()
        self.assertEqual(POLICY['diagnosticsSha256'], digest)

    def test_mapping_digest_bound_to_baseline(self):
        self.assertEqual(read('contracts/metadata-baseline.json')['mappingSha256'],
                         hashlib.sha256((ROOT / 'contracts/type-mappings.json').read_bytes()).hexdigest())

    def test_explicit_exported_type_mapping(self):
        mappings = {m['source']: m['target'] for m in read('contracts/type-mappings.json')['types']}
        for record in read('contracts/avalondock-metadata-release.json')['types']:
            name = record['name']
            if name.startswith(POLICY['from'] + '.'):
                self.assertEqual(POLICY['to'] + name[len(POLICY['from']):], mappings[name])

    def test_unique_mapping_no_wildcards(self):
        entries = read('contracts/type-mappings.json')['types']
        self.assertEqual(len(entries),len({m['source'] for m in entries}))
        for m in entries:
            self.assertNotIn('*',m['source'])
            self.assertFalse(m['target'].startswith(POLICY['from']))

    def test_product_and_sample_code_have_no_legacy_namespace(self):
        for directory in ('src','samples'):
            for p in (ROOT / directory).rglob('*'):
                if p.suffix in ('.cs','.xaml','.csproj') and not {'bin','obj'}.intersection(p.parts):
                    with self.subTest(file=str(p.relative_to(ROOT))):
                        self.assertNotIn(POLICY['from'],p.read_text(encoding='utf-8-sig'))

    def test_reference_probes_still_use_original_public_api(self):
        for directory in ('tools/ReferenceProbe','tools/ReferenceVisualProbe','tools/ReferenceSampleProbe'):
            source = '\n'.join(p.read_text(encoding='utf-8-sig') for p in (ROOT/directory).glob('*.cs'))
            self.assertIn('Xceed.Wpf.AvalonDock',source)

    def test_declared_product_namespace_not_confused_with_test_namespace(self):
        namespaces = set()
        for p in (ROOT/'src/UnoDock').rglob('*.cs'):
            if not {'bin','obj'}.intersection(p.parts):
                namespaces.update(re.findall(r'^namespace ([A-Za-z0-9_.]+)',p.read_text(),re.M))
        self.assertIn('UnoDock',namespaces)
        self.assertIn('UnoDock.Layout',namespaces)
        self.assertIn('UnoDock.Controls',namespaces)
        self.assertTrue(all(n.startswith('UnoDock') or n == 'Microsoft.Windows.Shell' for n in namespaces))

if __name__ == '__main__':
    unittest.main(verbosity=2)
