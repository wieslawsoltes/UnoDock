"""Resolve stable package versions once; builds consume the resulting pins."""
import json
import pathlib
import urllib.request

def latest(name):
    with urllib.request.urlopen(f'https://api.nuget.org/v3-flatcontainer/{name}/index.json', timeout=60) as response:
        versions = json.load(response)['versions']
    return max((v for v in versions if '-' not in v), key=lambda v: tuple(map(int, v.split('.'))))

versions = {name: latest(name.lower()) for name in ['Uno.Sdk', 'Uno.WinUI', 'Uno.Templates']}
pathlib.Path('contracts').mkdir(exist_ok=True)
pathlib.Path('contracts/versions.json').write_text(json.dumps(versions, indent=2) + '\n')
pathlib.Path('global.json').write_text(json.dumps({'sdk': {'version': '10.0.100', 'rollForward': 'latestFeature'}, 'msbuild-sdks': {'Uno.Sdk': versions['Uno.Sdk']}}, indent=2) + '\n')
print(json.dumps(versions, indent=2))
