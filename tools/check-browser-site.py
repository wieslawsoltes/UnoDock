#!/usr/bin/env python3
"""Validate local documentation routes and required real-runtime deployment assets."""
from __future__ import annotations

import argparse
from html.parser import HTMLParser
from pathlib import Path
from urllib.parse import unquote, urlsplit


class Links(HTMLParser):
    def __init__(self) -> None:
        super().__init__()
        self.urls: list[str] = []
        self.title = False
        self.main = False

    def handle_starttag(self, tag: str, attrs: list[tuple[str, str | None]]) -> None:
        attributes = dict(attrs)
        self.title |= tag == 'title'
        self.main |= tag == 'main'
        for name in ('href', 'src'):
            if attributes.get(name):
                self.urls.append(attributes[name])


def verify(root: Path) -> dict[str, int]:
    root = root.resolve(strict=True)
    required = ['index.html', 'docs/index.html', 'docs/getting-started.html',
                'docs/browser-workspaces.html', 'docs/deployment.html',
                'playground/index.html', 'playground/host.mjs', 'playground/session.mjs',
                'gallery/index.html', 'revision.txt']
    for route in required:
        if not (root / route).is_file():
            raise ValueError('Missing required site route: ' + route)
    revision = (root / 'revision.txt').read_text().strip()
    if len(revision) != 40 or any(c not in '0123456789abcdef' for c in revision):
        raise ValueError('The site must name its exact source revision.')
    if not any((root / 'gallery/_framework').glob('*.wasm')):
        raise ValueError('The real Uno WebAssembly runtime was not included.')
    documents = [root / 'index.html', root / 'playground/index.html', *sorted((root / 'docs').rglob('*.html'))]
    checked = 0
    for document in documents:
        parsed = Links()
        parsed.feed(document.read_text(encoding='utf-8'))
        if not parsed.title or not parsed.main:
            raise ValueError(f'Missing semantic title/main: {document.relative_to(root)}')
        for value in parsed.urls:
            url = urlsplit(value)
            if url.scheme or url.netloc or not url.path:
                continue
            if url.path.startswith('/'):
                raise ValueError(f'Documentation must use relocatable links: {value}')
            path = (document.parent / unquote(url.path)).resolve()
            if not path.is_relative_to(root):
                raise ValueError(f'A local link escapes the site: {value}')
            if path.is_dir():
                path /= 'index.html'
            if not path.is_file():
                raise ValueError(f'Broken route in {document.relative_to(root)}: {value}')
            checked += 1
    return {'documents': len(documents), 'localLinks': checked}


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('site', type=Path)
    args = parser.parse_args()
    print(verify(args.site))
