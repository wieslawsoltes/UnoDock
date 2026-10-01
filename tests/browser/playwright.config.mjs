import { defineConfig } from '@playwright/test';

export default defineConfig({
    testDir: '.',
    // CI runs a minimal smoke set (the published host renders, native typing
    // works); without UNODOCK_BROWSER_MINIMAL every spec runs.
    testMatch: process.env.UNODOCK_BROWSER_MINIMAL === '1' ? ['rendering.spec.mjs', 'local-edit.spec.mjs'] : '*.spec.mjs',
    timeout: 120000,
    globalTimeout: 1080000,
    expect: { timeout: 30000 },
    workers: 1,
    retries: 0,
    // Collect independent scenario failures instead of hiding later regressions
    // behind the first failure. The evidence verifier still rejects any failure.
    maxFailures: 0,
    fullyParallel: false,
    reporter: [
        ['list'],
        ['junit', { outputFile: '../../artifacts/browser-tests/results.xml' }],
        ['json', { outputFile: '../../artifacts/browser-tests/results.json' }]
    ],
    outputDir: '../../artifacts/browser-tests/traces',
    use: {
        launchOptions: process.env.UNODOCK_SOFTWARE_GPU === '1'
            ? { args: ['--use-gl=angle', '--use-angle=swiftshader', '--enable-unsafe-swiftshader'] }
            : {},
        baseURL: process.env.UNODOCK_BASE_URL || 'http://127.0.0.1:8765/UnoDock/playground/',
        viewport: { width: 1360, height: 900 },
        trace: 'retain-on-failure',
        screenshot: 'only-on-failure'
    },
    projects: [{ name: 'chromium', use: { browserName: 'chromium' } }]
});
