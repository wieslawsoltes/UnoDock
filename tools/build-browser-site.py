#!/usr/bin/env python3
"""Build the documentation and real Uno browser gallery into one Pages artifact."""
from pathlib import Path
import argparse
import html
import re
import shutil
import subprocess
import markdown

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--publish', type=Path, required=True)
parser.add_argument('--output', type=Path, required=True)
a = parser.parse_args()
root = Path(__file__).resolve().parents[1]
indexes = sorted(a.publish.rglob('index.html'), key=lambda p: len(p.parts))
if not indexes:
    raise SystemExit('The Uno publish did not produce index.html.')
gallery = indexes[0].parent
out = a.output.resolve()
if out.exists():
    shutil.rmtree(out)
shutil.copytree(gallery, out / 'gallery')
shutil.copytree(root / 'site/playground', out / 'playground')
shutil.copytree(root / 'site/assets', out / 'assets')
for file in (root / 'src/UnoDock.Browser/Assets').glob('*.mjs'):
    shutil.copy2(file, out / 'playground' / file.name)
(out / '.nojekyll').write_text('')
revision = subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=root, text=True).strip()
(out / 'revision.txt').write_text(revision + '\n')
(out / 'docs').mkdir()
nav = [('index', 'Documentation'), ('getting-started', 'Getting started'), ('browser-workspaces', 'Browser windows'),
       ('xaml-workbench', 'XAML & MVVM'), ('xaml-support', 'Consumer workspaces'), ('fluent-controls', 'Fluent styling'),
       ('docking-sizing', 'Docking & sizing'), ('testing', 'Build & verification'), ('architecture', 'Architecture')]

def page(title, body, prefix, current=''):
    navigation = ''.join(f'<a class="{"selected" if key == current else ""}" href="{prefix}docs/{key}.html">{label}</a>' for key, label in nav if (root / 'docs' / (key + '.md')).exists())
    return f'''<!doctype html><html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><meta name="description" content="UnoDock: composable docking for Uno Platform and WinUI, with real browser workspaces."><title>{html.escape(title)} · UnoDock</title><link rel="stylesheet" href="{prefix}assets/site.css"></head><body><header class="site-header"><a class="brand" href="{prefix}index.html">▦ <b>UnoDock</b></a><span>Docking, without boundaries.</span><nav><a href="{prefix}docs/index.html">Documentation</a><a href="{prefix}playground/">Live workbench ↗</a><a href="https://github.com/wieslawsoltes/UnoDock">GitHub</a></nav></header><div class="docs-layout"><aside>{navigation}<small>Source revision<br><code>{revision[:12]}</code></small></aside><main>{body}</main></div><footer>UnoDock contributors · MIT · Independent AvalonDock-style docking. Not affiliated with Xceed or Uno Platform.</footer></body></html>'''

for path in sorted((root / 'docs').glob('*.md')):
    source = path.read_text(encoding='utf-8')
    heading = next((line.lstrip('# ').strip() for line in source.splitlines() if line.startswith('# ')), path.stem)
    body = markdown.markdown(source, extensions=['fenced_code', 'tables', 'toc', 'sane_lists'])
    body = re.sub(r'href="([^"#]+)\.md(#[^"]*)?"', lambda m: 'href="' + m[1] + '.html' + (m[2] or '') + '"', body)
    (out / 'docs' / (path.stem + '.html')).write_text(page(heading, body, '../', path.stem), encoding='utf-8')
hero = '''<section class="hero"><span class="eyebrow">UNO PLATFORM · WINUI · WEBASSEMBLY</span><h1>A workspace that<br>works your way.</h1><p>Documents, tool windows, native floating chrome, and a coherent Fluent interface. Compose an IDE-style workspace in C# and XAML—and carry it into the browser.</p><div class="hero-actions"><a class="button primary" href="playground/">Open browser workbench ↗</a><a class="button" href="docs/getting-started.html">Get started</a></div><div class="facts"><span><b>C# + XAML</b>Native layout models</span><span><b>Desktop + Web</b>Shared docking engine</span><span><b>MIT</b>Independent implementation</span></div></section><section class="features"><article><h2>Compose, don’t imitate</h2><p>Declarative layout trees, observable document sources, native templated controls, and application-owned editors.</p></article><article><h2>Float beyond the canvas</h2><p>Real browser windows with guarded content transfer, drag-to-dock targets, close recovery, and a local journal.</p></article><article><h2>Know what is verified</h2><p>Source checks, cross-platform runtime suites, browser automation, and explicitly documented compatibility boundaries.</p></article></section><section class="next"><h2>Built for working applications.</h2><p>Explore the <a href="docs/xaml-workbench.html">compiled XAML samples</a>, read the <a href="docs/browser-workspaces.html">browser hosting contract</a>, or open the <a href="gallery/">full control gallery</a>.</p></section>'''
(out / 'index.html').write_text(page('Composable workspaces', hero, './'), encoding='utf-8')
print('Gallery source:', gallery)
print('Site:', out, 'revision:', revision)
print('Generated documentation pages:', len(list((out / 'docs').glob('*.html'))))
