# PulseDeck · YouTube (opzionale)

Estensione Chrome Manifest V3 per PulseDeck 0.4.0 o successivo. Senza estensione,
o dopo 10 secondi senza dati validi, PulseDeck usa le sessioni multimediali Windows.
Spotify e gli altri player nativi in riproduzione mantengono la precedenza esistente.

## Installazione locale

1. Apri `chrome://extensions` e attiva **Modalità sviluppatore**.
2. Premi **Carica estensione non pacchettizzata** e seleziona la cartella
   `youtube-extension` della distribuzione PulseDeck. Nell'installazione di questo PC:
   `%LOCALAPPDATA%\PulseDeck\app\youtube-extension`.
3. Ricarica le schede YouTube già aperte una volta. Non serve riavviare Chrome.
4. Apri un video e, dall'icona dell'estensione, verifica il collegamento a PulseDeck.

Per aggiornare i file premi **Ricarica** nella scheda dell'estensione e ricarica
le pagine YouTube. Per disabilitarla usa l'interruttore nella stessa scheda:
PulseDeck tornerà alla lettura Windows entro 10 secondi, senza riconfigurazione.

## Comportamento

- Legge solo il player principale delle pagine `https://www.youtube.com/watch?v=…`.
  Non legge i player delle anteprime, i video incorporati, Shorts o YouTube Music.
- Un video in riproduzione prevale sempre su uno in pausa. Se più video sono in
  riproduzione, prevale la scheda attiva nella finestra in primo piano; senza una
  finestra YouTube in primo piano resta selezionato il precedente video ancora valido.
- Non cambia volume, riproduzione, schede o finestre. Le schede sospese non vengono
  riattivate. Durante annunci o navigazione incompleta il player non viene segnalato.
  Se nessun player principale è leggibile si usa Windows, inclusi i suoi limiti.
- La selezione è globale per il profilo Chrome che ospita l'estensione. Installazioni
  simultanee in più profili non sono supportate: l'ultimo aggiornamento prevale.
- Il worker interroga le schede ogni due secondi; un alarm ripristina l'attività
  dopo la sospensione del worker. Nel frattempo l'agent torna a Windows.

## Dati e permessi

Solo accesso a YouTube, invio HTTP a `127.0.0.1:5178` e alarm di ripresa. Nessun
permesso cronologia, cookie, audio, debugger o lettura di altri siti. Titolo,
autore, ID video, posizione, durata, velocità e stato restano in memoria; nessuna
cronologia viene salvata. L'agent recupera la copertina dall'host fisso `i.ytimg.com`
utilizzando l'ID video validato (quindi YouTube riceve questa richiesta di immagine).

Il manifest contiene una **chiave pubblica**, non una credenziale: mantiene l'ID
`fdnmkffkacgdkcajobjemocddofgkpdf` stabile per l'origine HTTP ammessa dall'agent.
Il bridge accetta solo POST JSON dall'origine dell'estensione con header dedicato,
limita corpo e campi e non apre CORS agli altri siti/API. Non è un confine di
sicurezza contro altri processi locali dello stesso utente.

Il DOM di YouTube può cambiare: se i selettori non trovano il player, l'integrazione
rimane facoltativa e torna alla lettura Windows. Verifica reale da eseguire dopo
l'installazione: due finestre, un video in pausa, hover sulle anteprime, cambio video,
chiusura della scheda e disabilitazione dell'estensione.
