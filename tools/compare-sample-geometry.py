"""Compare arranged public sample pane geometry, not pixels or PropertyGrid internals.

The reference and candidate must represent the same independently authored sample
at the same viewport. Missing/extra structural controls fail; no nearest-neighbor
matching, text-dependent scaling or coordinate fitting is performed.
"""
from __future__ import annotations
import argparse
from collections import defaultdict
import hashlib
import json
import math
from pathlib import Path
import xml.etree.ElementTree as ET

SCENES = ('classic', 'selected-editor', 'rtl')
TYPES = frozenset(('LayoutPanelControl', 'LayoutDocumentPaneGroupControl',
                  'LayoutAnchorablePaneGroupControl', 'LayoutDocumentPaneControl',
                  'LayoutAnchorablePaneControl', 'LayoutAnchorSideControl'))
COORDINATES = ('x', 'y', 'width', 'height')


def load(path: Path) -> tuple[tuple[float, float], dict, str]:
    data = path.read_bytes()
    if len(data) > 4 * 1024 * 1024 or b'<!DOCTYPE' in data or b'<!ENTITY' in data:
        raise ValueError(f'Unsupported observation XML: {path}')
    root = ET.fromstring(data)
    if root.tag != 'Scene':
        raise ValueError(f'Expected Scene observations: {path}')
    viewport = (float(root.attrib['width']), float(root.attrib['height']))
    if any(not math.isfinite(value) or value <= 0 for value in viewport):
        raise ValueError('Invalid observed viewport')
    occurrences = defaultdict(int)
    rectangles = {}
    for element in root.findall('Element'):
        kind = element.attrib['type']
        if kind not in TYPES:
            continue
        rect = tuple(float(element.attrib[name]) for name in COORDINATES)
        if not all(math.isfinite(value) for value in rect):
            raise ValueError('Nonfinite observed geometry')
        if rect[2] < 0 or rect[3] < 0:
            raise ValueError('Negative observed extent')
        if rect[2] == 0 or rect[3] == 0:
            continue
        key = (kind, element.get('content', ''))
        ordinal = occurrences[key]
        occurrences[key] += 1
        rectangles[(*key, ordinal)] = rect
    if not rectangles:
        raise ValueError(f'No structural docking controls in {path}')
    return viewport, rectangles, hashlib.sha256(data).hexdigest()


def compare(reference: Path, actual: Path, tolerance: float = 1.0) -> dict:
    if not math.isfinite(tolerance) or tolerance < 0:
        raise ValueError('Tolerance must be a nonnegative finite DIP value')
    original_viewport, expected, expected_hash = load(reference)
    actual_viewport, observed, actual_hash = load(actual)
    missing = sorted(set(expected) - set(observed))
    extra = sorted(set(observed) - set(expected))
    differences = []
    maximum = 0.0
    for key in sorted(set(expected) & set(observed)):
        delta = tuple(abs(a - b) for a, b in zip(expected[key], observed[key]))
        maximum = max(maximum, *delta)
        differences.append({'type': key[0], 'content': key[1], 'occurrence': key[2],
                            'expected': dict(zip(COORDINATES, expected[key])),
                            'actual': dict(zip(COORDINATES, observed[key])),
                            'absoluteDelta': dict(zip(COORDINATES, delta))})
    viewport_matches = original_viewport == actual_viewport
    return {'schema': 1, 'referenceSha256': expected_hash, 'actualSha256': actual_hash,
            'scope': 'arranged structural pane, group, panel and side-rail rectangles',
            'pixelEquivalenceAsserted': False, 'toleranceDip': tolerance,
            'referenceViewport': original_viewport, 'actualViewport': actual_viewport,
            'viewportMatches': viewport_matches, 'missing': missing, 'extra': extra,
            'comparedRectangles': len(differences), 'maxCoordinateDeltaDip': maximum,
            'passed': viewport_matches and not missing and not extra and maximum <= tolerance,
            'rectangles': differences}


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('reference_directory', type=Path)
    parser.add_argument('actual_directory', type=Path)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--tolerance', type=float, default=1.0)
    args = parser.parse_args()
    reports = {}
    for scene in SCENES:
        report = compare(args.reference_directory / f'sample-reference-{scene}.xml',
                         args.actual_directory / f'sample-surface-{scene}.xml', args.tolerance)
        reports[scene] = report
        print(f"{scene}: {report['comparedRectangles']} rectangles; max delta "
              f"{report['maxCoordinateDeltaDip']:.8g} DIP; passed={report['passed']}; "
              f"missing={report['missing']}; extra={report['extra']}")
    result = {'schema': 1, 'passed': all(report['passed'] for report in reports.values()), 'scenes': reports}
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(result, indent=2, ensure_ascii=False) + '\n', encoding='utf-8')
    raise SystemExit(0 if result['passed'] else 1)


if __name__ == '__main__':
    main()
