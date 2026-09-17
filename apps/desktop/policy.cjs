'use strict';
const BASE_URL = 'http://127.0.0.1:5178';
function localUrl(value) {
  try { const u = new URL(value); return u.origin === BASE_URL && !u.username && !u.password; }
  catch { return false; }
}
function externalUrl(value) {
  try { const u = new URL(value); return u.protocol === 'https:' && !u.username && !u.password; }
  catch { return false; }
}
module.exports = { BASE_URL, localUrl, externalUrl };
