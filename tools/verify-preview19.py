"""Run the pinned delivery verifier with preview19-specific acceptance requirements.

The complete expanded verifier is included in the validation ZIP. This driver does
not change product sources, CI results, original contracts, or geometry tolerances.
"""
from pathlib import Path
import hashlib
import runpy

source = Path('tools/verify-preview16.py').read_bytes()
assert hashlib.sha1(b'blob ' + str(len(source)).encode() + b'\0' + source).hexdigest() == 'ca5ee13a9e44b3f06599c14956c76fcf334beee6'
text = source.decode()
def replace_once(old, new):
    global text
    assert text.count(old) == 1, old
    text = text.replace(old, new)
replace_once("VERSION = '0.1.0-preview.16'", "VERSION = '0.1.0-preview.19'")
replace_once("{'restore-ownership.xml', 'mvvm-workspace.xml'}", "{'restore-ownership.xml', 'mvvm-workspace.xml', 'navigator-commit.xml', 'navigator-sample.xml', 'focus-ownership.xml'}")
replace_once("root / 'verify-preview16.py'", "root / 'verify-preview19.py'")
replace_once("    manifest['verified'] = True", """    # No reference/mapping or pre-existing acceptance assertion is relaxed.
    previous = 'ec84d7d5ba6ac4be1829cc8c1c6d5369d7569521'
    subprocess.run(['git', 'diff', '--exit-code', previous, revision, '--',
                    'contracts', 'tools/ReferenceProbe', 'tools/ReferenceVisualProbe',
                    'tools/ReferenceSampleProbe', 'tests/metadata', 'tools/compare-sample-geometry.py'], check=True)
    old_tests = subprocess.check_output(['git', 'ls-tree', '-r', '--name-only', previous, '--', 'tests'], text=True).splitlines()
    if not old_tests:
        raise ValueError('Previous acceptance file inventory is empty')
    subprocess.run(['git', 'diff', '--exit-code', previous, revision, '--', *old_tests], check=True)
    for platform in ('linux', 'windows'):
        if not (root / platform / 'visuals/navigator-activation-sample.png').is_file():
            raise ValueError('Missing realized navigator sample capture: ' + platform)
    if not (root / 'linux/visuals/navigator-activation-sample-dark.png').is_file():
        raise ValueError('Native policy/theme sample scenario did not capture its final state')
    manifest['previousAcceptanceFilesUnchanged'] = len(old_tests)
    manifest['referenceContractsUnchanged'] = True
    manifest['originalNavigatorPixelEquivalenceAsserted'] = False
    manifest['localExecutionPerformed'] = False
    manifest['verified'] = True""")
text = text.replace('docs/mvvm-workspace.md for implementation', 'docs/navigator-activation.md for implementation')
text = text.replace('The new MVVM captures are not a pixel-equivalence claim.', 'The navigator sample captures are not a pixel-equivalence claim.')
runtime = Path('tools/expanded-preview19-verifier.py')
runtime.write_text(text)
runpy.run_path(str(runtime), run_name='__main__')
