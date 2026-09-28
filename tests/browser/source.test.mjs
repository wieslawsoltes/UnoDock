import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readdirSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { spawnSync } from 'node:child_process';

// Parse all shipped browser modules and test/configuration entry points before
// a costly WASM publish. Imports need not resolve for the syntax-only check.
const roots = [new URL('./', import.meta.url), new URL('../../src/UnoDock.Browser/Assets/', import.meta.url)];
for (const directory of roots) {
    const files = readdirSync(directory, { withFileTypes: true })
        .filter(entry => entry.isFile() && entry.name.endsWith('.mjs'));
    assert.ok(files.length > 0, 'The syntax preflight must not select an empty directory.');
    for (const entry of files) {
        const file = fileURLToPath(new URL(entry.name, directory));
        test('browser source parses: ' + entry.name, () => {
            const result = spawnSync(process.execPath, ['--check', file], {
                encoding: 'utf8', timeout: 10000
            });
            assert.ifError(result.error);
            assert.equal(result.signal, null, result.stderr);
            assert.equal(result.status, 0, result.stderr);
        });
    }
}

test('syntax preflight rejects a truncated configuration', () => {
    const result = spawnSync(process.execPath, ['--input-type=module', '--check'], {
        input: 'export default configure({ projects: []\n', encoding: 'utf8', timeout: 10000
    });
    assert.ifError(result.error);
    assert.notEqual(result.status, 0);
    assert.match(result.stderr, /SyntaxError/);
});
