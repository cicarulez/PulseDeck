chrome.runtime.sendMessage({ type: 'status' }).then(status => {
  document.querySelector('#status').textContent = status?.detail ?? 'In attesa di collegamento';
  document.querySelector('#title').textContent = status?.title ?? '';
}).catch(() => { document.querySelector('#status').textContent = 'Estensione in avvio'; });
