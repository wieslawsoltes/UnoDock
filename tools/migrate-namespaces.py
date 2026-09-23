"""One-time, deterministic preview-14 namespace migration.

Original reference inventories/fixtures/probes remain byte-identical. The diagnostic
allowlist is unchanged; only its explicit mapping digest is rebound to the new names.
"""
from pathlib import Path
import hashlib
import json

ROOT = Path(__file__).resolve().parent.parent
OLD, NEW = 'Xceed.Wpf.AvalonDock', 'UnoDock'

def migrate():
    protected = [p for folder in ('contracts', 'tools/ReferenceProbe', 'tools/ReferenceVisualProbe')
                 for p in (ROOT / folder).rglob('*') if p.is_file()
                 and p.name not in ('type-mappings.json', 'metadata-baseline.json')]
    before = {p: hashlib.sha256(p.read_bytes()).hexdigest() for p in protected}
    for folder in ('src', 'samples', 'tests'):
        for p in (ROOT / folder).rglob('*'):
            if p.suffix not in ('.cs', '.xaml', '.csproj') or any(x in p.parts for x in ('bin', 'obj')):
                continue
            text = p.read_text(encoding='utf-8-sig')
            if OLD in text:
                p.write_text(text.replace(OLD, NEW), encoding='utf-8')
    p = ROOT / 'tools/generate-property-adapters.py'
    text = p.read_text()
    text = text.replace("'using " + OLD, "'using " + NEW)
    text = text.replace("f'namespace {ns};'", "f'namespace {ns.replace(namespace, \"UnoDock\", 1)};'")
    p.write_text(text)
    p = ROOT / 'tools/audit-api.py'
    text = p.read_text()
    marker = 'def canonical(value):\n'
    insertion = "    value = re.sub(r'\\bXceed\\.Wpf\\.AvalonDock(?=\\.|\\s|$)', 'UnoDock', value)\n"
    if insertion not in text:
        assert text.count(marker) == 1
        text = text.replace(marker, marker + insertion)
    p.write_text(text)
    p = ROOT / 'contracts/type-mappings.json'
    data = json.loads(p.read_text())
    for item in data['types']:
        item['target'] = item['target'].replace(OLD, NEW)
    existing = {v['source'] for v in data['types']}
    reference = json.loads((ROOT / 'contracts/avalondock-metadata-release.json').read_text())
    names = {t['name'] for t in reference['types'] if t['name'].startswith(OLD + '.')}
    names.update(n.split('`')[0] for n in tuple(names) if '`' in n)
    for name in sorted(names - existing):
        data['types'].append({'source': name, 'target': NEW + name[len(OLD):],
                              'kind': 'namespace-migration',
                              'note': 'Preview 14 breaking namespace rename; no signature or behavior normalization.'})
    p.write_text(json.dumps(data, indent=2, ensure_ascii=False) + '\n')
    baseline_path = ROOT / 'contracts/metadata-baseline.json'
    baseline = json.loads(baseline_path.read_text())
    diagnostics = baseline['diagnostics'][:]
    baseline['mappingSha256'] = hashlib.sha256(p.read_bytes()).hexdigest()
    baseline_path.write_text(json.dumps(baseline, indent=2, ensure_ascii=False) + '\n')
    assert json.loads(baseline_path.read_text())['diagnostics'] == diagnostics
    for p, digest in before.items():
        assert hashlib.sha256(p.read_bytes()).hexdigest() == digest, str(p)

if __name__ == '__main__':
    migrate()
