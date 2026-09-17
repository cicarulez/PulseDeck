const { test } = require('node:test');
const assert = require('node:assert/strict');
const { localUrl, externalUrl } = require('./policy.cjs');
test('only the exact local agent origin can navigate inside the shell', () => {
  for (const url of ['http://127.0.0.1:5178', 'http://127.0.0.1:5178/api/preview.png']) assert.equal(localUrl(url), true);
  for (const url of ['http://127.0.0.1:5179', 'http://127.0.0.1:5178.evil.test', 'http://user@127.0.0.1:5178', 'file:///tmp/test', 'javascript:alert(1)', 'invalid']) assert.equal(localUrl(url), false);
});
test('external browser links must be HTTPS without credentials', () => {
  assert.equal(externalUrl('https://www.ansa.it/article'), true);
  for (const url of ['file:///C:/Windows', 'javascript:alert(1)', 'ms-settings:privacy', 'http://example.com', 'https://user:pass@example.com']) assert.equal(externalUrl(url), false);
});
