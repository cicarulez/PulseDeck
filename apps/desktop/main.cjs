'use strict';
const { app, BrowserWindow, Menu, dialog, session, shell } = require('electron');
const { execFile } = require('node:child_process');
const path = require('node:path');
const { BASE_URL, localUrl, externalUrl } = require('./policy.cjs');

// The desktop is an unprivileged client, never the owner of the agent or USB.
app.setPath('userData', path.join(process.env.LOCALAPPDATA || app.getPath('appData'), 'PulseDeck', 'desktop'));
app.setAppUserModelId('PulseDeck.Desktop');
let window;
let connecting = false;
let externalPrompt = false;
const singleInstance = app.requestSingleInstanceLock();
if (!singleInstance) app.quit();
else {
  app.on('second-instance', () => {
    if (window) { if (window.isMinimized()) window.restore(); window.show(); window.focus(); }
  });
  app.on('window-all-closed', () => app.quit());
  app.whenReady().then(createWindow).catch(() => {
    dialog.showErrorBox('PulseDeck', 'Impossibile aprire il configuratore desktop. Il display continua a essere gestito dall’agent.');
    app.quit();
  });
}
async function healthy() {
  try {
    const response = await fetch(`${BASE_URL}/api/health`, { signal: AbortSignal.timeout(2000), redirect: 'error' });
    const health = await response.json();
    return response.ok && health.ok === true && health.app === 'PulseDeck';
  } catch { return false; }
}
function startTask() {
  return new Promise(resolve => {
    if (process.platform !== 'win32') return resolve(false);
    execFile(path.join(process.env.SystemRoot || 'C:\\Windows', 'System32', 'schtasks.exe'),
      ['/Run', '/TN', 'PulseDeck'], { windowsHide: true, timeout: 5000 }, error => resolve(!error));
  });
}
async function connect() {
  if (connecting || !window || window.isDestroyed()) return;
  connecting = true;
  try {
    let ready = await healthy();
    if (!ready && await startTask()) {
      for (let attempt = 0; attempt < 30 && !ready && !window.isDestroyed(); attempt++) {
        await new Promise(resolve => setTimeout(resolve, 1000));
        ready = await healthy();
      }
    }
    if (window.isDestroyed()) return;
    if (ready) { await window.loadURL(BASE_URL); return; }
    const { response } = await dialog.showMessageBox(window, {
      type: 'warning', title: 'PulseDeck', message: 'Agent PulseDeck non disponibile',
      detail: 'Avvia PulseDeck tramite la sua attività pianificata Windows, poi premi Riprova. Se l’attività manca, esegui prima la configurazione dell’avvio automatico.',
      buttons: ['Riprova', 'Chiudi'], defaultId: 0, cancelId: 1
    });
    if (response === 0) setTimeout(connect, 0); else window.close();
  } catch {
    if (!window.isDestroyed()) dialog.showMessageBox(window, {
      type: 'error', message: 'Configuratore non raggiungibile',
      detail: 'Usa Visualizza → Ricollega per riprovare.', buttons: ['OK']
    });
  } finally { connecting = false; }
}
async function openExternal(url) {
  if (!externalUrl(url) || externalPrompt || window.isDestroyed()) return;
  externalPrompt = true;
  try {
    const { response } = await dialog.showMessageBox(window, {
      type: 'question', message: 'Aprire questo link nel browser?', detail: url,
      buttons: ['Apri', 'Annulla'], defaultId: 1, cancelId: 1
    });
    if (response === 0) await shell.openExternal(url);
  } catch {
    if (!window.isDestroyed()) await dialog.showMessageBox(window, { type: 'error', message: 'Impossibile aprire il browser.', buttons: ['OK'] });
  } finally { externalPrompt = false; }
}
async function createWindow() {
  const ses = session.defaultSession;
  ses.setPermissionRequestHandler((_contents, _permission, callback) => callback(false));
  ses.setPermissionCheckHandler(() => false);
  ses.on('will-download', event => event.preventDefault());
  ses.webRequest.onHeadersReceived({ urls: [`${BASE_URL}/*`] }, (details, callback) => {
    callback({ responseHeaders: { ...details.responseHeaders, 'Content-Security-Policy': [
      "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data: blob:; connect-src 'self' ws://127.0.0.1:5178; object-src 'none'; frame-src 'none'; base-uri 'self'; form-action 'none'"
    ] } });
  });
  window = new BrowserWindow({
    width: 1440, height: 940, minWidth: 1024, minHeight: 700,
    title: 'PulseDeck', backgroundColor: '#0b151b', show: false,
    webPreferences: { nodeIntegration: false, contextIsolation: true, sandbox: true, webSecurity: true, webviewTag: false }
  });
  window.webContents.on('will-navigate', (event, url) => {
    if (!localUrl(url)) { event.preventDefault(); void openExternal(url); }
  });
  window.webContents.on('will-redirect', (event, url) => { if (!localUrl(url)) event.preventDefault(); });
  window.webContents.setWindowOpenHandler(({ url }) => { void openExternal(url); return { action: 'deny' }; });
  window.webContents.on('will-attach-webview', event => event.preventDefault());
  Menu.setApplicationMenu(Menu.buildFromTemplate([
    { label: 'PulseDeck', submenu: [{ label: 'Chiudi configuratore', click: () => window.close() }] },
    { label: 'Visualizza', submenu: [
      { label: 'Ricollega', accelerator: 'CmdOrCtrl+R', click: connect },
      { role: 'resetZoom', label: 'Zoom originale' }, { role: 'zoomIn', label: 'Ingrandisci' }, { role: 'zoomOut', label: 'Riduci' },
      { role: 'togglefullscreen', label: 'Schermo intero' }
    ] }
  ]));
  window.show();
  await connect();
}
