// Active browser tab metadata is independent from the playing YouTube selection.
// Only Chrome's own favicon cache is read; no requests to arbitrary page/icon URLs.
export function selectedTab(window, tabs) {
  if (!window?.focused || window.incognito) return null;
  const tab = tabs.find(t => t.active && t.windowId === window.id);
  if (!tab || tab.incognito || tab.discarded || tab.status === 'loading'
      || typeof tab.title !== 'string' || !tab.title.trim() || tab.title.length > 512) return null;
  try { if (!['http:', 'https:'].includes(new URL(tab.url).protocol)) return null; }
  catch { return null; }
  return tab;
}
export function sameTab(a, b) {
  return !!a && !!b && a.id === b.id && a.windowId === b.windowId
    && a.title === b.title && a.url === b.url && a.favIconUrl === b.favIconUrl;
}

export function createForegroundBridge(chrome, fetcher = fetch, now = Date.now) {
  const permissions = { permissions: ['tabs', 'favicon'] };
  let revision = 0, running = false, queued = false;
  let cachedKey, cachedPng = null, expires = 0;
  const status = { enabled: false, connected: false };
  async function active() {
    const window = await chrome.windows.getLastFocused();
    if (!window?.focused || window.incognito) return null;
    return selectedTab(window, await chrome.tabs.query({ active: true, windowId: window.id }));
  }
  async function favicon(tab) {
    if (!tab.favIconUrl) return null;
    const key = JSON.stringify([tab.url, tab.favIconUrl]);
    if (key === cachedKey && now() < expires) return cachedPng;
    cachedKey = key; cachedPng = null;
    expires = now() + 5000;
    try {
      const url = new URL(chrome.runtime.getURL('/_favicon/'));
      url.searchParams.set('pageUrl', tab.url);
      url.searchParams.set('size', '32');
      const response = await fetcher(url.href, { signal: AbortSignal.timeout(1000) });
      if (!response.ok) return null;
      const bytes = new Uint8Array(await response.arrayBuffer());
      if (bytes.length < 33 || bytes.length > 32768
          || ![137, 80, 78, 71, 13, 10, 26, 10].every((v, i) => bytes[i] === v)) return null;
      cachedPng = btoa(String.fromCharCode(...bytes));
      expires = now() + 30000;
    } catch { /* Native title and executable icon remain available. */ }
    return cachedPng;
  }
  async function update(version) {
    try {
      status.enabled = await chrome.permissions.contains(permissions);
      let tab = status.enabled ? await active() : null;
      const faviconPng = tab ? await favicon(tab) : null;
      // Tab/window focus can change while reading the favicon. Never publish that old icon.
      if (tab && !sameTab(tab, await active())) tab = null;
      if (version !== revision) return;
      if (!tab) { cachedKey = undefined; cachedPng = null; }
      const response = await fetcher('http://127.0.0.1:5178/api/browser-tab', {
        method: 'POST', headers: { 'Content-Type': 'application/json', 'X-PulseDeck-Client': 'youtube-extension' },
        body: JSON.stringify({ tab: tab ? { title: tab.title, faviconPng } : null }),
        signal: AbortSignal.timeout(1500)
      });
      status.connected = response.ok;
    } catch { status.connected = false; }
  }
  async function refresh() {
    revision++; queued = true;
    if (running) return;
    running = true;
    try {
      while (queued) { queued = false; await update(revision); }
    } finally { running = false; }
  }
  return { refresh, status };
}
