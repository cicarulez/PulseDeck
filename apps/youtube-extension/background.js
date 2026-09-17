import { selectVideo } from './selection.mjs';
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
  if (!sender.tab && message?.type === 'status') respond(status);
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
