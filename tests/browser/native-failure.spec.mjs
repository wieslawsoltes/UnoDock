import { test, expect } from '@playwright/test';

test('an unhandled native error retires stale readiness and recovery preserves content with renewed leases', async ({ page }) => {
    await page.goto('');
    await page.waitForFunction(() => window.UnoDockBrowser?.applied?.items?.length === 4);
    const before = await page.evaluate(() => window.UnoDockBrowser.snapshot());
    const nativeFrame = page.locator('#app').contentFrame();
    // Deliberately raise one unhandled error only after actual native startup.
    // This is failure-handling coverage, not a substitute renderer/editor.
    await nativeFrame.locator('body').evaluate(() => {
        setTimeout(() => { throw new Error('Deliberate native-runtime monitor regression'); }, 0);
    });
    await expect(page.getByRole('alert')).toContainText('Deliberate native-runtime monitor regression');
    await expect(page.getByRole('status')).toContainText('Native renderer stopped');
    await expect(page.getByRole('button', { name: 'Float in browser window', exact: true })).toBeDisabled();
    expect(await page.evaluate(() => window.UnoDockBrowser.applied)).toBeNull();
    expect(await page.evaluate(() => window.UnoDockBrowser.snapshot().items)).toEqual(before.items);
    await page.getByRole('button', { name: 'Restart native renderer', exact: true }).click();
    await page.waitForFunction(() => window.UnoDockBrowser?.applied?.items?.length === 4);
    await expect(page.getByRole('alert')).toBeHidden();
    await expect(page.frameLocator('#app').getByRole('button', { name: 'Float selected', exact: true })).toBeVisible();
    const recovered = await page.evaluate(() => window.UnoDockBrowser.snapshot());
    expect(recovered.session).toBe(before.session);
    expect(recovered.windowId).toBe(before.windowId);
    // Existing WorkspaceState.ready rotates every owned record when a host is
    // replaced. Preserve that contract, not the obsolete runtime's authority.
    expect(recovered.items).toEqual(before.items.map(item => ({
        ...item, lease: item.lease + 1, revision: item.revision + 1
    })));
    const previous = before.items[0];
    const stale = await page.evaluate(item => JSON.parse(window.UnoDockBrowser.call({
        op: 'update', id: item.id, lease: item.lease, title: item.title, payload: 'obsolete editor'
    })), previous);
    expect(stale.ok).toBe(false);
    expect(await page.evaluate(() => window.UnoDockBrowser.snapshot().items)).toEqual(recovered.items);
});
