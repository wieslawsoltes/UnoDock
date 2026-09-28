import { test, expect } from '@playwright/test';

test('an unhandled native error retires stale readiness and explicit recovery preserves leases', async ({ page }) => {
    await page.goto('');
    await page.waitForFunction(() => window.UnoDockBrowser?.applied?.items?.length === 4);
    const before = await page.evaluate(() => window.UnoDockBrowser.snapshot().items);
    const nativeFrame = await page.locator('#app').contentFrame();
    // Inject one unhandled runtime error into the native document after proving
    // real startup. This tests fault reporting, not a simulated editor or broker.
    await nativeFrame.locator('body').evaluate(() => {
        setTimeout(() => { throw new Error('Deliberate native-runtime monitor regression'); }, 0);
    });
    await expect(page.getByRole('alert')).toContainText('Deliberate native-runtime monitor regression');
    await expect(page.getByRole('status')).toContainText('Native renderer stopped');
    await expect(page.getByRole('button', { name: 'Float in browser window', exact: true })).toBeDisabled();
    expect(await page.evaluate(() => window.UnoDockBrowser.applied)).toBeNull();
    expect(await page.evaluate(() => window.UnoDockBrowser.snapshot().items)).toEqual(before);
    await page.getByRole('button', { name: 'Restart native renderer', exact: true }).click();
    await page.waitForFunction(() => window.UnoDockBrowser?.applied?.items?.length === 4);
    await expect(page.getByRole('alert')).toBeHidden();
    await expect(page.frameLocator('#app').getByRole('button', { name: 'Float selected', exact: true })).toBeVisible();
    expect(await page.evaluate(() => window.UnoDockBrowser.snapshot().items)).toEqual(before);
});
