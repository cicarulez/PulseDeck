# PulseDeck · Chrome (opzionale)

Estensione Chrome Manifest V3: YouTube richiede PulseDeck 0.4.0 o successivo;
le icone delle schede richiedono PulseDeck 0.7.6 o successivo. Senza estensione,
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

## Titolo e icona della scheda Chrome

PulseDeck 0.7.6 legge **il titolo della scheda dalla finestra Windows**, anche senza
estensione. Se manca il titolo torna al nome dell’applicazione. Per la favicon:

1. Dopo l’aggiornamento premi **Ricarica** in `chrome://extensions` sulla stessa
   estensione, ora chiamata **PulseDeck · Chrome**. La cartella e l’ID non cambiano.
2. Nella barra di Chrome premi l’icona **Estensioni** (il puzzle), poi
   **PulseDeck · Chrome**: si apre il popup dell’estensione. Qui premi
   **Abilita icone delle schede** e accetta la richiesta di Chrome per i permessi
   opzionali `tabs` e `favicon`. Ricaricare l’estensione da solo non abilita le icone.
3. Passa tra schede di siti diversi. Titolo e icona seguono la scheda selezionata
   nella finestra Chrome in primo piano, indipendentemente dal video YouTube che
   continua a suonare in background.

L’opzione è disattivata inizialmente e non serve per YouTube. Puoi rimuovere i due
permessi con **Disabilita icone delle schede**. Senza dati recenti (sei secondi), con
un titolo diverso da quello della finestra, o senza favicon valida, resta l’icona
originale di Chrome. Il nome nativo della scheda continua a funzionare.

Vengono considerate solo schede HTTP/HTTPS, non in caricamento/sospese, fuori
incognito. Pagine interne, file locali e finestre private usano l’icona nativa;
il titolo della finestra resta visibile anche in questi casi. Il confronto è sul
titolo: due finestre con titoli identici non sono distinguibili con certezza.
Più profili Chrome con l’estensione contemporaneamente restano non supportati.

## Comportamento YouTube

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

Per YouTube: accesso a YouTube, invio HTTP a `127.0.0.1:5178` e alarm di ripresa.
L’opzione per le icone aggiunge solo i permessi opzionali `tabs` e `favicon`: Chrome
può descriverli come accesso ai dati di navigazione. Non viene richiesta l’API
cronologia, né accesso a cookie, audio, debugger o contenuto degli altri siti. Titolo,
autore, ID video, posizione, durata, velocità e stato restano in memoria; nessuna
cronologia viene salvata. L'agent recupera la copertina dall'host fisso `i.ytimg.com`
utilizzando l'ID video validato (quindi YouTube riceve questa richiesta di immagine).

Per l’icona della scheda, l’URL viene usato **dentro Chrome** per consultare il suo
endpoint locale `/_favicon/`; il worker non scarica URL di icone esterni e l’agent
non riceve l’URL della pagina. Invia solo titolo e PNG limitato a 32 KiB. L’agent
valida dimensioni e decodifica, conserva l’ultimo dato in memoria e non salva una
cronologia. Lo stato live/anteprima può comunque mostrare titoli privati: considera
questo aspetto prima di condividere il display o screenshot.

Il manifest contiene una **chiave pubblica**, non una credenziale: mantiene l'ID
`fdnmkffkacgdkcajobjemocddofgkpdf` stabile per l'origine HTTP ammessa dall'agent.
Il bridge accetta solo POST JSON dall'origine dell'estensione con header dedicato,
limita corpo e campi e non apre CORS agli altri siti/API. Non è un confine di
sicurezza contro altri processi locali dello stesso utente.

Il DOM di YouTube può cambiare: se i selettori non trovano il player, l'integrazione
rimane facoltativa e torna alla lettura Windows. Verifica reale da eseguire dopo
l'installazione: due finestre, un video in pausa, hover sulle anteprime, cambio video,
chiusura della scheda e disabilitazione dell'estensione.

Riferimenti: [API delle schede](https://developer.chrome.com/docs/extensions/reference/api/tabs),
[cache delle favicon](https://developer.chrome.com/docs/extensions/how-to/ui/favicons).
