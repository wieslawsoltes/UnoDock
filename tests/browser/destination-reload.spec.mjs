import { test, expect } from '@playwright/test';

// This scenario deliberately suspends an HTTP navigation. A service-worker-owned
// response bypasses Playwright's route hook, so disable workers only here. The
// original reload/recovery suites still exercise the normally published worker.
test.use({ serviceWorkers: 'block' });

const snapshot = page => page.evaluate(() => window.UnoDockBrowser.snapshot());
const ready = async page => {
    await page.waitForFunction(() => window.UnoDockBrowser?.nativeState === 'ready' && window.UnoDockBrowser.applied?.items,
        null, { timeout: 90000 });
    await expect(page.frameLocator('#app').getByRole('button', { name: 'Float selected', exact: true })).toBeVisible();
};

test('reloading destination is unavailable before native readiness and renews its leases', async ({ page }) => {
    await page.goto('');
    await ready(page);
    await page.locator('[data-content="welcome"]').click();
    const opening = page.context().waitForEvent('page');
    await page.getByRole('button', { name: 'Float in browser window', exact: true }).click();
    const child = await opening;
    await ready(child);
    await expect.poll(async () => (await snapshot(child)).items.map(item => item.id)).toEqual(['welcome']);
    const destination = await child.evaluate(() => window.UnoDockBrowser.windowId);
    const before = (await snapshot(child)).items[0];
    const source = (await snapshot(page)).items.find(item => item.id === 'architecture');
    let release, intercepted = false;
    const blocked = new Promise(resolve => { release = resolve; });
    let continuation = Promise.resolve();
    const match = url => url.pathname.endsWith('/gallery/');
    const holdNavigation = route => {
        if (!route.request().isNavigationRequest()) return route.continue();
        intercepted = true;
        continuation = blocked.then(() => route.continue());
        return continuation;
    };
    await child.route(match, holdNavigation);
    try {
        await child.reload({ waitUntil: 'commit' });
        await expect.poll(() => intercepted, {
            timeout: 10000,
            message: 'The fixture must intercept the actual replacement Uno document before checking availability.'
        }).toBe(true);
        await expect.poll(async () => (await snapshot(page)).windows.find(window => window.id === destination)?.ready).toBe(false);
        await expect(page.locator(`#destination option[value="${destination}"]`)).toHaveCount(0);
        const result = await page.evaluate(({ source, destination }) => JSON.parse(window.UnoDockBrowser.call({
            op: 'move', id: source.id, lease: source.lease, destination, zone: 'center'
        })), { source, destination });
        expect(result.ok).toBe(false);
        expect((await snapshot(page)).items.find(item => item.id === source.id)).toEqual(source);
        release();
        await continuation;
        await ready(child);
        await expect(page.locator(`#destination option[value="${destination}"]`)).toHaveCount(1);
        expect((await snapshot(child)).items[0]).toEqual({ ...before, lease: before.lease + 1, revision: before.revision + 1 });
    } finally {
        release();
        await continuation;
        await child.unroute(match, holdNavigation);
        await child.close();
    }
});
