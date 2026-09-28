import { test, expect } from '@playwright/test';

test('acknowledged native typing preserves caret order without docking refreshes', async ({ page }) => {
    await page.goto('');
    await page.waitForFunction(() => window.UnoDockBrowser?.applied?.items?.length === 4);
    await page.locator('[data-content="welcome"]').click();
    const editor = page.frameLocator('#app').getByRole('textbox', { name: 'Editor welcome', exact: true });
    await expect(editor).toBeVisible();
    await expect.poll(() => page.evaluate(() => window.UnoDockBrowser.applied.active)).toBe('welcome');
    const refreshes = await page.evaluate(() => window.UnoDockBrowser.applied.projectionCount);
    const chunks = ['Primary editor: ', 'the caret must remain here. ', 'Polling is not a render clock.'];
    await editor.press('ControlOrMeta+A');
    let expected = '';
    for (const chunk of chunks) {
        await editor.pressSequentially(chunk);
        expected += chunk;
        await expect.poll(() => page.evaluate(() => window.UnoDockBrowser.snapshot().items.find(x => x.id === 'welcome')?.payload)).toBe(expected);
        await expect.poll(() => page.evaluate(() => window.UnoDockBrowser.applied.items.find(x => x.id === 'welcome')?.payload)).toBe(expected);
        // Deliberately cross several polling periods before resuming the same
        // native selection. This assertion must fail on a polling-induced refresh.
        await page.waitForTimeout(500);
        expect(await page.evaluate(() => window.UnoDockBrowser.applied.projectionCount)).toBe(refreshes);
    }
    const broker = await page.evaluate(() => window.UnoDockBrowser.snapshot().items.find(x => x.id === 'welcome'));
    const projected = await page.evaluate(() => window.UnoDockBrowser.applied.items.find(x => x.id === 'welcome'));
    expect(projected.revision).toBe(broker.revision);
    expect(projected.payload).toBe(expected);
});
