import { test, expect } from '@playwright/test';

const snapshot = page => page.evaluate(() => window.UnoDockBrowser.snapshot());
const ids = page => page.evaluate(() => window.UnoDockBrowser.applied?.items.map(item => item.id).sort() ?? []);
const ready = async page => {
    await page.waitForFunction(() => window.UnoDockBrowser?.nativeState === 'ready' && window.UnoDockBrowser.applied?.items,
        null, { timeout: 90000 });
    await expect(page.frameLocator('#app').getByRole('button', { name: 'Float selected', exact: true })).toBeVisible();
};
async function open(page) {
    await page.goto('');
    await ready(page);
    await expect.poll(() => ids(page)).toEqual(['architecture', 'notes', 'output', 'welcome']);
}
async function float(page, id) {
    await page.locator(`[data-content="${id}"]`).click();
    const opening = page.context().waitForEvent('page');
    await page.getByRole('button', { name: 'Float in browser window', exact: true }).click();
    const child = await opening;
    await ready(child);
    await expect.poll(() => ids(child)).toContain(id);
    await expect.poll(() => ids(page)).not.toContain(id);
    return child;
}
async function failRenderer(page) {
    await page.frameLocator('#app').locator('body').evaluate(() => {
        setTimeout(() => { throw new Error('Deliberate destination-availability regression'); }, 0);
    });
    await expect(page.getByRole('alert')).toContainText('Deliberate destination-availability regression');
    expect(await page.evaluate(() => window.UnoDockBrowser.nativeState)).toBe('failed');
}
async function recover(page) {
    await page.getByRole('button', { name: 'Restart native renderer', exact: true }).click();
    await ready(page);
    await expect(page.getByRole('alert')).toBeHidden();
}

test('failed destination is excluded and rejects transfer until native recovery', async ({ page }) => {
    await open(page);
    const child = await float(page, 'welcome');
    const destination = await child.evaluate(() => window.UnoDockBrowser.windowId);
    const before = (await snapshot(page)).items.find(item => item.id === 'architecture');
    await failRenderer(child);
    await expect.poll(async () => (await snapshot(page)).windows.find(window => window.id === destination)?.ready).toBe(false);
    await expect(page.locator(`#destination option[value="${destination}"]`)).toHaveCount(0);
    const result = await page.evaluate(({ before, destination }) => JSON.parse(window.UnoDockBrowser.call({
        op: 'move', id: before.id, lease: before.lease, destination, zone: 'left'
    })), { before, destination });
    expect(result.ok).toBe(false);
    expect(result.error).toContain('destination native renderer');
    expect((await snapshot(page)).items.find(item => item.id === before.id)).toEqual(before);
    await recover(child);
    await expect(page.locator(`#destination option[value="${destination}"]`)).toHaveCount(1);
    await page.locator('[data-content="architecture"]').click();
    await page.locator('#destination').selectOption(destination);
    await page.locator('#placement').selectOption('left');
    await page.getByRole('button', { name: 'Move', exact: true }).click();
    await expect.poll(() => ids(child)).toEqual(['architecture', 'welcome']);
    await expect.poll(() => ids(page)).toEqual(['notes', 'output']);
    await child.close();
    await expect.poll(() => ids(page)).toEqual(['architecture', 'notes', 'output', 'welcome']);
});

test('failed primary blocks dock-all without partial return and resumes after recovery', async ({ page }) => {
    await open(page);
    const child = await float(page, 'architecture');
    const destination = await child.evaluate(() => window.UnoDockBrowser.windowId);
    await page.locator('[data-content="notes"]').click();
    await page.locator('#destination').selectOption(destination);
    await page.locator('#placement').selectOption('left');
    await page.getByRole('button', { name: 'Move', exact: true }).click();
    await expect.poll(() => ids(child)).toEqual(['architecture', 'notes']);
    const before = (await snapshot(child)).items;
    await failRenderer(page);
    const dockAll = child.getByRole('button', { name: 'Dock all & close window', exact: true });
    await expect(dockAll).toBeDisabled();
    const result = await child.evaluate(() => JSON.parse(window.UnoDockBrowser.call({ op: 'dockAll' })));
    expect(result.ok).toBe(false);
    expect(result.error).toContain('destination native renderer');
    expect((await snapshot(child)).items).toEqual(before);
    expect((await snapshot(page)).items.map(item => item.id).sort()).toEqual(['output', 'welcome']);
    expect(child.isClosed()).toBe(false);
    await recover(page);
    await expect(dockAll).toBeEnabled();
    const closed = child.waitForEvent('close');
    await dockAll.click();
    await closed;
    await expect.poll(() => ids(page)).toEqual(['architecture', 'notes', 'output', 'welcome']);
    const returned = (await snapshot(page)).items;
    for (const previous of before) {
        expect(returned.find(item => item.id === previous.id)).toEqual({
            ...previous, owner: 'main', lease: previous.lease + 1, revision: previous.revision + 1
        });
    }
});
