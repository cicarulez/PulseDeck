import test from 'node:test';
import assert from 'node:assert/strict';
import { selectVideo } from '../../apps/youtube-extension/selection.mjs';
const item = (tabId, playing, focused = false) => ({ tabId, active: true, focused, media: { playing } });
test('paused foreground tab cannot steal a playing background video', () => {
  assert.equal(selectVideo([item(1, true), item(2, false, true)], 2).tabId, 1);
});
test('two playing videos follow focused window, then remain stable without focus', () => {
  assert.equal(selectVideo([item(1, true), item(2, true, true)], 1).tabId, 2);
  assert.equal(selectVideo([item(1, true), item(2, true)], 2).tabId, 2);
});
test('closing last video clears the selection', () => assert.equal(selectVideo([], 1), null));
