chrome.runtime.sendMessage({ type: 'status' }).then(status => {
  document.querySelector('#status').textContent = status?.detail ?? 'In attesa di collegamento';
  document.querySelector('#title').textContent = status?.title ?? '';
}).catch(() => { document.querySelector('#status').textContent = 'Estensione in avvio'; });

const toggle = document.querySelector('#tab-toggle');
const tabStatus = document.querySelector('#tab-status');
const permission = { permissions: ['tabs', 'favicon'] };
let enabled = false;
async function refreshPermissions() {
  enabled = await chrome.permissions.contains(permission);
  toggle.textContent = enabled ? 'Disabilita icone delle schede' : 'Abilita icone delle schede';
  tabStatus.textContent = enabled ? 'Attive per la scheda in primo piano. Dati solo locali.' : 'Disattivate: viene usata l’icona di Chrome.';
  toggle.disabled = false;
}
toggle.addEventListener('click', async () => {
  toggle.disabled = true;
  try {
    if (enabled) await chrome.permissions.remove(permission);
    else await chrome.permissions.request(permission);
    await chrome.runtime.sendMessage({ type: 'refresh-tab' });
    await refreshPermissions();
  } catch {
    tabStatus.textContent = 'Impossibile aggiornare i permessi. Riapri il pannello per riprovare.';
    toggle.disabled = false;
  }
});
refreshPermissions().catch(() => { tabStatus.textContent = 'Permessi non disponibili: ricarica l’estensione.'; });
