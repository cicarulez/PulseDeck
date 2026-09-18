import test from 'node:test';
import assert from 'node:assert/strict';
import { selectedTab, sameTab, createForegroundBridge } from '../../apps/youtube-extension/foreground.mjs';

const window = { id: 1, focused: true, incognito: false };
const tab = { id: 2, windowId: 1, active: true, status: 'complete', title: 'Page', url: 'https://example.test/', favIconUrl: 'https://example.test/favicon.ico' };
function rig() {
  const r = { window, tab, allowed: true, calls: [], faviconReads: 0, failIcon: false };
  const chrome = {
    permissions: { contains: async () => r.allowed },
    windows: { getLastFocused: async () => r.window },
    tabs: { query: async () => [r.tab] },
    runtime: { getURL: path => 'chrome-extension://test' + path }
  };
  const fetcher = async (url, options) => {
    if (url.startsWith('chrome-extension:')) {
      r.faviconReads++;
      await r.onIcon?.();
      if (r.failIcon) throw new Error('missing favicon');
      const bytes = new Uint8Array(33); bytes.set([137, 80, 78, 71, 13, 10, 26, 10]);
      return { ok: true, arrayBuffer: async () => bytes.buffer };
    }
    assert.equal(url, 'http://127.0.0.1:5178/api/browser-tab');
    r.calls.push(JSON.parse(options.body));
    return { ok: true };
  };
  r.bridge = createForegroundBridge(chrome, fetcher);
  return r;
}

test('only the focused active HTTP tab is eligible, excluding private and loading pages', () => {
  assert.equal(selectedTab(window, [tab]), tab);
  for (const patch of [{ active: false }, { discarded: true }, { incognito: true }, { status: 'loading' },
    { title: '' }, { title: 'x'.repeat(513) }, { url: 'chrome://settings' }, { url: 'file:///tmp/page.html' }])
    assert.equal(selectedTab(window, [{ ...tab, ...patch }]), null);
  assert.equal(selectedTab({ ...window, focused: false }, [tab]), null);
  assert.equal(selectedTab({ ...window, incognito: true }, [tab]), null);
  assert.equal(selectedTab({ ...window, id: 3 }, [tab]), null);
});
test('matching identity includes window, navigation, title and favicon', () => {
  assert.equal(sameTab(tab, { ...tab }), true);
  for (const patch of [{ id: 4 }, { windowId: 4 }, { title: 'Other' }, { url: 'https://other.test/' }, { favIconUrl: 'new' }])
    assert.equal(sameTab(tab, { ...tab, ...patch }), false);
});
test('cache reuses the icon and sends no URL or other tab metadata to the agent', async () => {
  const r = rig(); await r.bridge.refresh(); await r.bridge.refresh();
  assert.equal(r.faviconReads, 1);
  assert.deepEqual(Object.keys(r.calls[0].tab).sort(), ['faviconPng', 'title']);
  assert.equal(r.calls[0].tab.title, 'Page');
  assert.ok(r.calls[0].tab.faviconPng);
  r.tab = { ...tab, favIconUrl: 'new' };
  await r.bridge.refresh(); assert.equal(r.faviconReads, 2);
});
test('permission removal and leaving Chrome immediately clear the tab', async () => {
  const r = rig(); await r.bridge.refresh();
  r.allowed = false; await r.bridge.refresh();
  assert.equal(r.calls.at(-1).tab, null);
  assert.equal(r.faviconReads, 1);
  r.allowed = true; r.window = { ...window, focused: false };
  await r.bridge.refresh(); assert.equal(r.calls.at(-1).tab, null);
});
test('focus changing during favicon read cannot publish a stale icon', async () => {
  const r = rig(); r.onIcon = () => { r.tab = { ...tab, id: 9, title: 'Other' }; };
  await r.bridge.refresh(); assert.equal(r.calls.at(-1).tab, null);
});
test('events during an in-flight read are coalesced and only latest state is sent', async () => {
  const r = rig();
  r.onIcon = () => { r.window = { ...window, focused: false }; void r.bridge.refresh(); };
  await r.bridge.refresh();
  assert.deepEqual(r.calls, [{ tab: null }]);
});
test('missing favicon keeps title and allows native icon fallback', async () => {
  const r = rig(); r.failIcon = true;
  await r.bridge.refresh();
  assert.deepEqual(r.calls.at(-1), { tab: { title: 'Page', faviconPng: null } });
});
