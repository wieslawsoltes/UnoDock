#!/usr/bin/env python3
"""Print the CHANGELOG.md section for one version, for release notes.

    python3 tools/release-notes.py 0.1.0-preview.1 [--changelog CHANGELOG.md]

Fails when the version has no section or the section is empty, so a release cannot
be published without notes.
"""
from __future__ import annotations

import argparse
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[1]


def section(text: str, version: str) -> str:
    lines = text.splitlines()
    heading = f'## {version}'
    try:
        start = next(i for i, line in enumerate(lines) if line.strip() == heading)
    except StopIteration:
        raise ValueError(f'CHANGELOG.md has no "{heading}" section.') from None
    end = next((i for i in range(start + 1, len(lines)) if lines[i].startswith('## ')), len(lines))
    body = '\n'.join(lines[start + 1:end]).strip()
    if not body:
        raise ValueError(f'The "{heading}" section of CHANGELOG.md is empty.')
    return body + '\n'


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument('version')
    parser.add_argument('--changelog', type=Path, default=ROOT / 'CHANGELOG.md')
    args = parser.parse_args()
    try:
        sys.stdout.write(section(args.changelog.read_text(encoding='utf-8'), args.version.removeprefix('v')))
    except (OSError, ValueError) as error:
        print(f'::error::{error}', file=sys.stderr)
        return 1
    return 0


if __name__ == '__main__':
    sys.exit(main())
