import { test, expect } from '@playwright/test';

test('published browser host uses the real Uno software canvas and controls', async ({ page }) => {
    await page.goto('');
    await page.waitForFunction(() => window.UnoDockBrowser?.applied?.items?.length === 4);
    const frame = page.frameLocator('#app');
    await expect(frame.getByRole('button', { name: 'Float selected', exact: true })).toBeVisible();
    const surface = await frame.locator('canvas').first().evaluate(canvas => ({
        width: canvas.width,
        height: canvas.height,
        software: canvas.getContext('2d') !== null
    }));
    expect(surface.width).toBeGreaterThan(100);
    expect(surface.height).toBeGreaterThan(100);
    expect(surface.software).toBe(true);
});
