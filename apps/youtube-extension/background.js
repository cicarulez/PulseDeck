import { selectVideo } from './selection.mjs';
import { createForegroundBridge } from './foreground.mjs';
const foreground = createForegroundBridge(chrome);
const endpoint = 'http://127.0.0.1:5178/api/browser-media';
let busy = false;
let previousTabId;
let status = { connected: false, title: '', detail: 'In attesa di PulseDeck' };

async function poll() {
  if (busy) return;
  busy = true;
  try {
    const [tabs, windows] = await Promise.all([
      chrome.tabs.query({ url: 'https://www.youtube.com/*' }), chrome.windows.getAll()]);
    const focused = windows.find(w => w.focused)?.id;
    const candidates = (await Promise.all(tabs.filter(t => !t.discarded).map(async tab => {
      try {
        const media = await Promise.race([
          chrome.tabs.sendMessage(tab.id, { type: 'read-player' }, { frameId: 0 }),
          new Promise(resolve => setTimeout(() => resolve(null), 800))]);
        return media ? { media, tabId: tab.id, active: tab.active, focused: tab.windowId === focused } : null;
      } catch { return null; } // Existing tabs need one reload after installation.
    }))).filter(Boolean);
    const chosen = selectVideo(candidates, previousTabId);
    previousTabId = chosen?.tabId;
    const response = await fetch(endpoint, { method: 'POST',
      headers: { 'Content-Type': 'application/json', 'X-PulseDeck-Client': 'youtube-extension' },
      body: JSON.stringify({ media: chosen?.media ?? null }), signal: AbortSignal.timeout(1500) });
    if (!response.ok) throw new Error('Bridge unavailable');
    status = { connected: true, title: chosen?.media.title ?? '', detail: chosen
      ? (chosen.media.playing ? 'Video in riproduzione' : 'Video in pausa')
      : 'Nessun video principale: lettura Windows' };
  } catch {
    status = { connected: false, title: '', detail: 'PulseDeck non raggiungibile: lettura Windows' };
  } finally { busy = false; }
  chrome.action.setBadgeText({ text: status.connected ? 'ON' : '' }).catch(() => {});
}
chrome.runtime.onMessage.addListener((message, sender, respond) => {
  if (!sender.tab && message?.type === 'status') respond({ ...status, tab: foreground.status });
  if (!sender.tab && message?.type === 'refresh-tab') { void foreground.refresh(); respond({ ok: true }); }
});
chrome.alarms.onAlarm.addListener(() => { void poll(); });
chrome.runtime.onInstalled.addListener(() => { void poll(); });
chrome.runtime.onStartup.addListener(() => { void poll(); });
chrome.tabs.onActivated.addListener(() => { void poll(); });
chrome.tabs.onRemoved.addListener(() => { void poll(); });
chrome.windows.onFocusChanged.addListener(() => { void poll(); });
// Rebuild state from open tabs after worker restart; no browsing history is stored.
chrome.alarms.create('refresh', { periodInMinutes: 0.5 });
setInterval(() => { void poll(); }, 2000);
void poll();

chrome.tabs.onActivated.addListener(() => { void foreground.refresh(); });
chrome.tabs.onRemoved.addListener(() => { void foreground.refresh(); });
chrome.tabs.onUpdated.addListener((_id, change, tab) => {
  if (tab.active && ['title', 'url', 'favIconUrl', 'status'].some(key => key in change)) void foreground.refresh();
});
chrome.windows.onFocusChanged.addListener(() => { void foreground.refresh(); });
chrome.permissions.onAdded.addListener(() => { void foreground.refresh(); });
chrome.permissions.onRemoved.addListener(() => { void foreground.refresh(); });
chrome.alarms.onAlarm.addListener(() => { void foreground.refresh(); });
setInterval(() => { void foreground.refresh(); }, 2000);
void foreground.refresh();
