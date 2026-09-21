"""Report declaration differences; never promote a name match into a behavioral parity claim."""
import argparse, hashlib, json, pathlib, re
root = pathlib.Path(__file__).resolve().parent.parent
parser = argparse.ArgumentParser()
parser.add_argument('implementation', type=pathlib.Path)
parser.add_argument('--strict', action='store_true')
args = parser.parse_args()
reference = json.loads((root / 'contracts/avalondock.json').read_text())
implementation = json.loads(args.implementation.read_text())
def canonical(value):
    value = value.replace('System.Windows.Controls.', 'Microsoft.UI.Xaml.Controls.').replace('System.Windows.Media.', 'Microsoft.UI.Xaml.Media.').replace('System.Windows.', 'Microsoft.UI.Xaml.')
    value = re.sub(r'\bContextMenu\b', 'MenuFlyout', value)
    value = re.sub(r'\bMenuItem\b', 'MenuFlyoutItem', value)
    value = value.replace('?', '')
    return re.sub(r'\s+', ' ', value).strip()
a = {canonical(line) for line in reference['declarations']}
b = {canonical(line) for line in implementation['declarations']}
report = {'schema': 1, 'comparison': 'mapped syntax declarations, not semantic or behavioral equivalence',
          'referenceSha256': reference['sha256'], 'referenceDeclarationCount': len(a), 'implementationDeclarationCount': len(b),
          'exactMappedMatches': len(a & b), 'missingOrDifferent': sorted(a-b), 'additionalOrDifferent': sorted(b-a), 'fullCompatibilityVerified': False}
path = root / 'artifacts/api-diff.json'; path.parent.mkdir(exist_ok=True)
path.write_text(json.dumps(report, indent=2)+'\n')
print(f"Reference: {len(a)}; implementation: {len(b)}; exact mapped declarations: {len(a&b)}; missing/different: {len(a-b)}")
if args.strict and a-b: raise SystemExit(1)
