import { defineConfig } from '@playwright/test';
export default defineConfig({
    testDir: '.', testMatch: 'workspace.spec.mjs', timeout: 120000, expect: { timeout: 30000 },
    workers: 1, retries: 0, fullyParallel: false,
    reporter: [['list'], ['junit', { outputFile: '../../artifacts/browser-tests/results.xml' }], ['json', { outputFile: '../../artifacts/browser-tests/results.json' }]],
    outputDir: '../../artifacts/browser-tests/traces',
    use: { baseURL: process.env.UNODOCK_BASE_URL || 'http://127.0.0.1:8765/UnoDock/playground/', viewport: { width: 1360, height: 900 }, trace: 'retain-on-failure', screenshot: 'only-on-failure' },
    projects: [{ name: 'chromium', use: { browserName: 'chromium' } }]
});
