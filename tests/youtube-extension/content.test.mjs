import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import vm from 'node:vm';
const source = readFileSync(new URL('../../apps/youtube-extension/content.js', import.meta.url), 'utf8');
function read({ path = '/watch?v=abcdefghijk', pageId = 'abcdefghijk', ad = false, main = true } = {}) {
  let listener;
  const video = { readyState: 4, paused: false, ended: false, currentTime: 12, duration: 100, playbackRate: 1 };
  const player = { classList: { contains: () => ad }, querySelector: s => main && s === 'video.html5-main-video' ? video : null };
  const page = { getAttribute: () => pageId, querySelector: s => s === '#movie_player' ? player
    : { textContent: s.startsWith('h1') ? 'Main title' : 'Author' } };
  vm.runInNewContext(source, { URL, location: { href: 'https://www.youtube.com' + path },
    document: { querySelector: s => s === 'ytd-watch-flexy' ? page : null },
    chrome: { runtime: { onMessage: { addListener: cb => { listener = cb; } } } } });
  let result; listener({ type: 'read-player' }, {}, value => { result = value; }); return result;
}
test('reads the main watch player', () => { assert.equal(read().title, 'Main title'); assert.equal(read().playing, true); });
test('ignores home previews, sidebar-only video, ads and stale SPA player', () => {
  assert.equal(read({ path: '/' }), null);
  assert.equal(read({ main: false }), null);
  assert.equal(read({ ad: true }), null);
  assert.equal(read({ pageId: 'oldoldold12' }), null);
});
