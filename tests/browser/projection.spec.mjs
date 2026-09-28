import { test, expect } from '@playwright/test';

test('idle remote polling does not force repeated native layout projections', async ({ page }) => {
    page.on('pageerror', error => console.error('Native browser page error:', error.message));
    console.log('Opening the compiled Uno workspace.');
    await page.goto('', { waitUntil: 'domcontentloaded' });
    console.log('Waiting for the native model projection.');
    await page.waitForFunction(() => window.UnoDockBrowser?.applied?.items?.length === 4,
        null, { timeout: 90000, polling: 100 });
    console.log('Model projected; checking the native accessibility controls.');
    const control = page.frameLocator('#app').getByRole('button', { name: 'Float selected', exact: true });
    await expect(control).toBeVisible();
    const original = await page.evaluate(() => window.UnoDockBrowser.applied.projectionCount);
    expect(original).toBeGreaterThan(0);
    // Several remote polls must pass, with actual native controls still present.
    await page.waitForTimeout(1200);
    expect(await page.evaluate(() => window.UnoDockBrowser.applied.projectionCount)).toBe(original);
    await expect(control).toBeVisible();
    await page.getByRole('button', { name: 'Theme', exact: true }).click();
    await expect.poll(() => page.evaluate(() => window.UnoDockBrowser.applied.projectionCount)).toBeGreaterThan(original);
    const changed = await page.evaluate(() => window.UnoDockBrowser.applied.projectionCount);
    await page.waitForTimeout(1200);
    expect(await page.evaluate(() => window.UnoDockBrowser.applied.projectionCount)).toBe(changed);
    await expect(control).toBeVisible();
});
