import { test, expect } from '@playwright/test';
import fs from 'node:fs/promises';
async function ready(page) {
    let crash, close;
    const terminated = new Promise((_, reject) => {
        crash = () => reject(new Error('The browser renderer crashed before native workspace readiness.'));
        close = () => reject(new Error('The browser window closed before native workspace readiness.'));
        page.once('crash', crash); page.once('close', close);
    });
    try {
        await Promise.race([terminated, page.waitForFunction(() => window.UnoDockBrowser?.applied?.items, null, { timeout: 90000 })]);
    } finally {
        page.off('crash', crash); page.off('close', close);
    }
}
const ids = page => page.evaluate(() => window.UnoDockBrowser.applied.items.map(x => x.id));
async function open(page) { await page.goto(''); await ready(page); await expect.poll(() => ids(page)).toContain('welcome'); }
async function popup(page, id) {
    await page.locator(`[data-content="${id}"]`).click();
    const next = page.context().waitForEvent('page');
    await page.getByRole('button', { name: 'Float in browser window', exact: true }).click();
    const child = await next; await ready(child);
    await expect.poll(() => ids(child)).toContain(id);
    await expect.poll(() => ids(page)).not.toContain(id);
    return child;
}

async function replaceNativeText(page, id, text) {
    const editor = page.frameLocator('#app').getByRole('textbox', { name: 'Editor ' + id, exact: true });
    await expect(editor).toBeVisible();
    // Uno's Skia TextBox owns selection and keyboard editing. DOM fill/select()
    // changes the semantic proxy's selection, not the managed text selection.
    // Send actual keyboard events; never invoke broker writes or managed callbacks.
    await editor.press('ControlOrMeta+A');
    await editor.pressSequentially(text);
    await expect.poll(() => page.evaluate(id => window.UnoDockBrowser.snapshot().items.find(x => x.id === id)?.payload, id)).toBe(text);
}

test('real Uno runtime boots and renders the four model-backed editors', async ({ page }) => {
    await open(page);
    await expect.poll(() => ids(page)).toHaveLength(4);
    const frame = page.frameLocator('#app');
    await expect(frame.getByRole('button', { name: 'Float selected', exact: true })).toBeVisible();
    await fs.mkdir('../../artifacts/browser-tests', { recursive: true });
    await page.screenshot({ path: '../../artifacts/browser-tests/workbench.png' });
});

test('native Uno text edits survive popup transfer and popup close', async ({ page }) => {
    await open(page);
    await page.locator('[data-content="welcome"]').click();
    const text = 'Edited in the real Uno browser TextBox.';
    await replaceNativeText(page, 'welcome', text);
    const child = await popup(page, 'welcome');
    await expect.poll(() => child.evaluate(() => window.UnoDockBrowser.applied.items[0]?.payload)).toBe(text);
    const satelliteText = 'Replaced from the native satellite TextBox.';
    await replaceNativeText(child, 'welcome', satelliteText);
    await child.close();
    await expect.poll(() => ids(page)).toContain('welcome');
    await expect.poll(() => page.evaluate(() => window.UnoDockBrowser.applied.items.find(x => x.id === 'welcome')?.payload)).toBe(satelliteText);
});

test('two native browser windows exchange tools and documents without duplicate ownership', async ({ page }) => {
    await open(page);
    const one = await popup(page, 'architecture');
    const two = await popup(page, 'notes');
    const destination = await two.evaluate(() => window.UnoDockBrowser.windowId);
    await one.locator('#destination').selectOption(destination);
    await one.locator('#placement').selectOption('left');
    await one.getByRole('button', { name: 'Move', exact: true }).click();
    await expect.poll(() => ids(one)).not.toContain('architecture');
    await expect.poll(() => ids(two)).toContain('architecture');
    await expect.poll(() => ids(two)).toContain('notes');
    await two.getByRole('button', { name: 'Dock all & close window', exact: true }).click();
    await expect.poll(() => ids(page)).toContain('architecture');
    await expect.poll(() => ids(page)).toContain('notes');
    await one.close();
});

test('lease guards reject stale writes after a content transfer', async ({ page }) => {
    await open(page);
    const lease = await page.evaluate(() => window.UnoDockBrowser.snapshot().items.find(x => x.id === 'welcome').lease);
    const child = await popup(page, 'welcome');
    const response = await page.evaluate(lease => JSON.parse(window.UnoDockBrowser.call({ op: 'update', id: 'welcome', lease, title: 'stale', payload: 'must not overwrite' })), lease);
    expect(response.ok).toBe(false);
    expect(await child.evaluate(() => window.UnoDockBrowser.snapshot().items[0].payload)).not.toBe('must not overwrite');
    await child.close();
});

test('DOM drag protocol docks into a requested edge and consumes its ticket once', async ({ page }) => {
    await open(page);
    const child = await popup(page, 'welcome');
    const ticket = await child.locator('[data-content="welcome"]').evaluate(element => {
        const transfer = new DataTransfer();
        element.dispatchEvent(new DragEvent('dragstart', { bubbles: true, dataTransfer: transfer }));
        return transfer.getData('application/x-unodock-transfer');
    });
    expect(ticket).not.toBe('');
    await page.evaluate(ticket => {
        const transfer = new DataTransfer(); transfer.setData('application/x-unodock-transfer', ticket);
        document.dispatchEvent(new DragEvent('dragenter', { bubbles: true, dataTransfer: transfer }));
        document.querySelector('[data-zone="right"]').dispatchEvent(new DragEvent('drop', { bubbles: true, dataTransfer: transfer }));
    }, ticket);
    await expect.poll(() => ids(page)).toContain('welcome');
    await expect.poll(() => ids(child)).not.toContain('welcome');
    expect(await page.evaluate(() => window.UnoDockBrowser.snapshot().items.find(x => x.id === 'welcome').zone)).toBe('right');
    const replay = await child.evaluate(ticket => JSON.parse(window.UnoDockBrowser.call({ op: 'drop', ticket, zone: 'center' })), ticket);
    expect(replay.ok).toBe(false);
    await child.close();
});

test('blocked popups retain source ownership and show recovery guidance', async ({ page }) => {
    await page.addInitScript(() => { window.open = () => null; });
    await open(page);
    await page.getByRole('button', { name: 'Float in browser window', exact: true }).click();
    await expect(page.getByRole('status')).toContainText('Popup blocked');
    expect(await ids(page)).toHaveLength(4);
});

test('reloading a satellite preserves its leased content', async ({ page }) => {
    await open(page); const child = await popup(page, 'welcome');
    await child.reload(); await ready(child);
    await expect.poll(() => ids(child)).toEqual(['welcome']);
    await expect.poll(() => ids(page)).not.toContain('welcome');
    await child.close();
});

test('journal recovery returns popup content to the reloaded primary', async ({ page }) => {
    await open(page); const child = await popup(page, 'welcome');
    await page.reload(); await ready(page);
    await expect.poll(() => ids(page)).toContain('welcome');
    await expect(child.locator('#recovery')).toBeVisible();
    await child.close();
});
