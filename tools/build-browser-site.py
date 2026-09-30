#!/usr/bin/env python3
"""Assemble documentation, the real Uno WASM gallery, and the browser workspace."""
from pathlib import Path
from urllib.parse import urlsplit, quote, unquote
import argparse
import html
import os
import re
import shutil
import subprocess
import markdown

NAVIGATION = [('index', 'Documentation'), ('getting-started', 'Getting started'),
              ('browser-workspaces', 'Browser windows'), ('xaml-workbench', 'XAML & MVVM'),
              ('xaml-support', 'Consumer workspaces'), ('fluent-controls', 'Fluent styling'),
              ('docking-sizing', 'Docking & sizing'), ('testing', 'Build & verification'),
              ('architecture', 'Architecture'), ('deployment', 'Publishing')]


def build(publish: Path, output: Path) -> None:
    root = Path(__file__).resolve().parents[1]
    publish = publish.resolve()
    output = output.resolve()
    if output == root or output == publish or output in publish.parents or output in root.parents:
        raise ValueError('The output must not replace the repository or publish input.')
    indexes = sorted(publish.rglob('index.html'), key=lambda path: (len(path.parts), str(path)))
    if not indexes:
        raise ValueError('Uno publishing did not produce index.html.')
    gallery = indexes[0].parent
    if output.exists():
        shutil.rmtree(output)
    shutil.copytree(gallery, output / 'gallery')
    shutil.copytree(root / 'site/playground', output / 'playground')
    shutil.copytree(root / 'site/assets', output / 'assets')
    for path in (root / 'src/UnoDock.Browser/Assets').glob('*.mjs'):
        shutil.copy2(path, output / 'playground' / path.name)
    (output / '.nojekyll').write_text('')
    revision = subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=root, text=True).strip()
    (output / 'revision.txt').write_text(revision + '\n')
    docs = list((root / 'docs').rglob('*.md'))
    docs += [root / name for name in ('README.md', 'CONTRIBUTING.md') if (root / name).exists()]
    routes = {path.resolve(): path.relative_to(root).with_suffix('.html').as_posix() for path in docs}

    def source_url(path: str) -> str:
        return f'https://github.com/wieslawsoltes/UnoDock/blob/{revision}/' + quote(path, safe='/')

    def transform_links(body: str, source: Path, route: str) -> str:
        def replace(match):
            attribute, value = match.group(1), html.unescape(match.group(2))
            parsed = urlsplit(value)
            if parsed.scheme or parsed.netloc or not parsed.path:
                return match.group(0)
            # The historical README was authored relative to repository root.
            origin = root if source.name == 'early-preview-notes.md' else source.parent
            destination = (origin / unquote(parsed.path)).resolve()
            if not destination.is_relative_to(root):
                return f'{attribute}="{html.escape(source_url(parsed.path), quote=True)}"'
            if destination in routes:
                target = os.path.relpath(routes[destination], Path(route).parent).replace(os.sep, '/')
            else:
                target = source_url(destination.relative_to(root).as_posix())
            if parsed.fragment:
                target += '#' + parsed.fragment
            return f'{attribute}="{html.escape(target, quote=True)}"'
        return re.sub(r'(href|src)="([^"]+)"', replace, body)

    def page(title: str, body: str, route: str, current: str = '') -> str:
        prefix = '../' * len(Path(route).parent.parts) or './'
        navigation = ''.join(f'<a class="{"selected" if key == current else ""}" href="{prefix}docs/{key}.html">{label}</a>' for key, label in NAVIGATION)
        return f'''<!doctype html><html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><meta name="description" content="UnoDock: composable docking for Uno Platform and WinUI, with real browser workspaces."><title>{html.escape(title)} · UnoDock</title><link rel="stylesheet" href="{prefix}assets/site.css"></head><body><header class="site-header"><a class="brand" href="{prefix}index.html">▦ <b>UnoDock</b></a><span>Docking, without boundaries.</span><nav aria-label="Main navigation"><a href="{prefix}docs/index.html">Documentation</a><a href="{prefix}playground/">Live workbench ↗</a><a href="https://github.com/wieslawsoltes/UnoDock">GitHub</a></nav></header><div class="docs-layout"><aside aria-label="Documentation">{navigation}<small>Source revision<br><code>{revision[:12]}</code></small></aside><main>{body}</main></div><footer>UnoDock contributors · MIT · Independent docking following the reference WPF docking library. Not affiliated with Xceed or Uno Platform.</footer></body></html>'''

    for source in sorted(docs):
        text = source.read_text(encoding='utf-8')
        title = next((line[2:].strip() for line in text.splitlines() if line.startswith('# ')), source.stem)
        body = markdown.markdown(text, extensions=['fenced_code', 'tables', 'toc', 'sane_lists'])
        route = routes[source.resolve()]
        body = transform_links(body, source, route)
        path = output / route
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(page(title, body, route, source.stem), encoding='utf-8')
    hero = '''<section class="hero"><span class="eyebrow">UNO PLATFORM · WINUI · WEBASSEMBLY</span><h1>A workspace that<br>works your way.</h1><p>Documents, tool windows, native floating chrome, and a coherent Fluent interface. Compose an IDE-style workspace in C# and XAML—and carry it into the browser.</p><div class="hero-actions"><a class="button primary" href="playground/">Open browser workbench ↗</a><a class="button" href="docs/getting-started.html">Get started</a></div><div class="facts"><span><b>C# + XAML</b>Native layout models</span><span><b>Desktop + Web</b>Shared docking engine</span><span><b>MIT</b>Independent implementation</span></div></section><section class="features"><article><h2>Compose, don’t imitate</h2><p>Declarative layout trees, observable document sources, native templated controls, and application-owned editors.</p></article><article><h2>Float beyond the canvas</h2><p>Real browser windows with guarded content transfer, drag-to-dock targets, close recovery, and a local journal.</p></article><article><h2>Know what is verified</h2><p>Source checks, cross-platform runtime suites, browser automation, and explicitly documented compatibility boundaries.</p></article></section><section class="next"><h2>Built for working applications.</h2><p>Explore the <a href="docs/xaml-workbench.html">compiled XAML samples</a>, read the <a href="docs/browser-workspaces.html">browser hosting contract</a>, or open the <a href="gallery/">full control gallery</a>.</p></section>'''
    (output / 'index.html').write_text(page('Composable workspaces', hero, 'index.html'), encoding='utf-8')
    print(f'Gallery input: {gallery}\nSite: {output}\nRevision: {revision}\nDocumentation pages: {len(docs)}')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--publish', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    arguments = parser.parse_args()
    build(arguments.publish, arguments.output)
