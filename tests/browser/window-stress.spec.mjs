import { test, expect } from '@playwright/test';

const read = page => page.evaluate(() => ({
    state: window.UnoDockBrowser.nativeState,
    ids: window.UnoDockBrowser.applied?.items.map(x => x.id).sort() ?? []
}));
async function ready(page) {
    await page.waitForFunction(() => window.UnoDockBrowser?.nativeState === 'failed' ||
        window.UnoDockBrowser?.applied?.items, null, { timeout: 90000 });
    expect((await read(page)).state).toBe('ready');
    await expect(page.frameLocator('#app').getByRole('button', { name: 'Float selected', exact: true })).toBeVisible();
}
async function float(page, id) {
    await page.locator(`[data-content="${id}"]`).click();
    const opening = page.context().waitForEvent('page');
    await page.getByRole('button', { name: 'Float in browser window', exact: true }).click();
    const child = await opening;
    await ready(child);
    await expect.poll(async () => (await read(child)).ids).toContain(id);
    await expect.poll(async () => (await read(page)).ids).not.toContain(id);
    return child;
}

test('repeated native window returns retain edited payloads without runtime faults', async ({ page, context }) => {
    // Six cycles each boot two browser windows; the AOT runtime start dominates.
    test.setTimeout(360000);
    const errors = [];
    const observe = window => {
        window.on('pageerror', error => errors.push(error.message));
        window.on('crash', () => errors.push('Browser process crashed: ' + window.url()));
    };
    observe(page);
    context.on('page', observe);
    await page.goto('');
    await ready(page);
    const expectedIds = ['architecture', 'notes', 'output', 'welcome'];
    for (const [cycle, edge] of ['left', 'right', 'top', 'bottom', 'left', 'center'].entries()) {
        const one = await float(page, 'architecture');
        const two = await float(page, 'notes');
        const text = 'Native satellite edit, cycle ' + cycle;
        // Keyboard input needs the editor's content projected as active; focus sent
        // to a collapsed TextBox's semantic proxy is refused and not retried.
        await expect.poll(() => one.evaluate(() => window.UnoDockBrowser.applied.active)).toBe('architecture');
        const editor = one.frameLocator('#app').getByRole('textbox', { name: 'Editor architecture', exact: true });
        await expect(editor).toBeVisible();
        await editor.press('ControlOrMeta+A');
        await editor.pressSequentially(text);
        await expect.poll(() => one.evaluate(() => window.UnoDockBrowser.snapshot().items.find(x => x.id === 'architecture')?.payload)).toBe(text);
        const destination = await two.evaluate(() => window.UnoDockBrowser.windowId);
        await one.locator('#destination').selectOption(destination);
        await one.locator('#placement').selectOption(edge);
        await one.getByRole('button', { name: 'Move', exact: true }).click();
        await expect.poll(async () => (await read(two)).ids).toEqual(['architecture', 'notes']);
        await expect.poll(async () => (await read(one)).ids).toEqual([]);
        const closing = two.waitForEvent('close');
        // This click closes its own window. Playwright's post-click hit-target round
        // trip into the closing page races the close (TargetClosedError); the close
        // event and the returned content are the evidence that the click landed.
        await two.getByRole('button', { name: 'Dock all & close window', exact: true }).click({ noWaitAfter: true }).catch(error => {
            // The click itself can race the window it closes; only that is tolerated.
            if (!/Target page, context or browser has been closed/.test(error.message))
                throw error;
        });
        await closing;
        await expect.poll(() => read(page)).toEqual({ state: 'ready', ids: expectedIds });
        await page.locator('[data-content="architecture"]').click();
        await expect(page.frameLocator('#app').getByRole('textbox', { name: 'Editor architecture', exact: true })).toHaveValue(text);
        await one.close();
        expect(errors, 'Unhandled native errors after cycle ' + cycle).toEqual([]);
    }
    await expect.poll(() => page.evaluate(() => window.UnoDockBrowser.snapshot().windows.map(x => x.id))).toEqual(['main']);
    expect(errors).toEqual([]);
});
