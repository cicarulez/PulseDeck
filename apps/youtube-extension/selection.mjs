// Playback wins over focus. Focus breaks ties; otherwise keep the chosen playing tab.
export function selectVideo(candidates, previousTabId) {
  return [...candidates].sort((a, b) =>
    Number(b.media.playing) - Number(a.media.playing) ||
    Number(b.focused && b.active) - Number(a.focused && a.active) ||
    Number(b.tabId === previousTabId) - Number(a.tabId === previousTabId) ||
    Number(b.active) - Number(a.active) || a.tabId - b.tabId)[0] ?? null;
}
