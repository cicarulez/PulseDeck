# PulseDeck

Un pannello locale per Windows che porta sensori hardware, musica, informazioni di
Discord e profili contestuali sul display TURZX 8,8″ 1920×480.

Il configuratore è disponibile su **http://127.0.0.1:5178**. Puoi chiudere il browser:
l'agent continua a lavorare in background. L'avvio automatico non apre né console
né browser e collega il display.

Il [piano di sviluppo](docs/roadmap.md) raccoglie le prossime attività: layout liberi,
più schermi, FPS, attività vocale e temi. La ricerca Aura è in pausa. Le verifiche realmente
eseguite sono descritte in [docs/validation.md](docs/validation.md).

## Funzionalità disponibili

- Anteprima e display condividono lo stesso rendering 1920×480, con sfondo locale.
- Pagina **Sensori** con ricerca, filtri, valori correnti/minimi/massimi e diagnostica
  dei permessi. Sul PC di sviluppo: 617 sensori e parametri, incluse soglie dei dispositivi.
- Pagina **Widget**: layout compatto fino a 16 posizioni, valori, barre e anelli;
  resta disponibile il layout classico a otto posizioni.
- Layout **Meteo**: 12 sensori in una griglia 3×4, meteo a sinistra e Spotify/Discord
  a destra. Località ricercabile e selezionabile nell’app; inizialmente Roma, Italia,
  sul PC di sviluppo.
- News nel footer con preset, feed RSS/Atom personalizzati e dimensione testo regolabile.
- Copertina, titolo, artista, stato e progresso di Spotify o altri player Windows.
- RAM fisica usata/libera/totale utilizzabile; sensori rete con velocità in B/s, KiB/s o MiB/s.
- Sfondo statico scuro integrato, oppure un’immagine locale scelta dall’utente.
- Nome e icona dell’app in primo piano; indicazione Gioco per gli eseguibili configurati.
- Profili Desktop, Gaming e Musica; selezione manuale o automatica con ritardo configurabile.
- Layout Gaming dedicato con icona/nome del gioco, identificazione GPU e gli stessi 16 widget;
  sfondo statico locale associabile a ciascun gioco.
- Nickname e stato mute/deaf del solo utente Discord seguito nell’header, accanto all’ora.
- Bot Discord integrato: partecipanti del canale, mute/deaf e utente da evidenziare.
- Collegamento TURZX con verifica dell'identità e aggiornamenti completi/parziali.
- Recupero USB limitato per timeout e `needReSend:1`, con reinvio completo e diagnostica.
- Avvio nascosto all'accesso Windows, con permessi amministrativi e log su file.
- Comando di spegnimento del TURZX all'arresto di PulseDeck e alla fine della sessione Windows.

Le letture mancanti appaiono come `—`. Il bot collegato è stato verificato; le
transizioni ingresso/uscita e mute/deaf con partecipanti richiedono ancora una prova.

**Non ancora disponibili:** FPS, indicatore di chi parla, colori Aura Sync, più display,
video MP4/WebM, animazioni fluide, layout libero e icona nella tray. Il solo valore
`mute=false` non viene interpretato come attività vocale.

## Cosa installare per usarlo

| Componente | Necessità | Versione/provenienza e note |
| --- | --- | --- |
| Windows nella sessione dell'utente | Obbligatorio | Agent Windows x64; deve accedere a sessioni multimediali e programma in primo piano |
| Cartella completa PulseDeck pubblicata | Obbligatoria | Include eseguibile, runtime .NET, librerie e `wwwroot`; non copiare soltanto l'EXE |
| PawnIO | Per i sensori a basso livello | Provato con **2.2.0**; [release ufficiale](https://github.com/namazso/PawnIO.Setup/releases/tag/2.2.0). Installazione una tantum, poi riavvio dell'agent come amministratore |
| Browser | Per configurare | Può essere chiuso dopo la configurazione |
| Spotify o altro player Windows compatibile | Per la musica | Nessuna API key Spotify richiesta; serve una sessione multimediale attiva |
| Bot Discord con accesso al server/canale | Per Discord | Token e ID importati localmente; nessun backend Node separato nella modalità integrata |
| ASUS Armoury Crate/Aura SDK | Solo integrazione futura | Non necessari alle funzioni attuali. Prima modalità prevista: leggere i colori RGB del PC |
| PresentMon | Solo integrazione futura | Non installarlo per questa versione: l'adapter FPS non è ancora implementato |

Il pacchetto Windows è **self-contained**: per usarlo non servono SDK .NET, Node.js,
Python, AIDA64 o LibreHardwareMonitor come programma separato. La libreria dei sensori
è inclusa. Disponibilità e nomi delle letture dipendono da hardware, driver e permessi.

PawnIO 2.2.0 verificato sul PC di sviluppo: firma Authenticode valida e SHA-256 uguale
al digest pubblicato per l'installer ufficiale:

```text
1f519a22e47187f70a1379a48ca604981c4fcf694f4e65b734aaa74a9fba3032
```

L'installer non è incluso nel repository. Non installare la variante unrestricted
per sviluppatori: per PulseDeck è stata usata l'installazione standard.

## Primo avvio

1. Copia tutta la cartella pubblicata `artifacts/windows` in una cartella locale Windows.
   Sul PC di sviluppo è `%LOCALAPPDATA%\PulseDeck\app`.
2. Installa PawnIO se vuoi le letture di CPU, scheda madre e ventole che lo richiedono.
3. Avvia `Start-PulseDeck.cmd`: richiede i permessi amministrativi per l'agent e apre
   il configuratore. Il processo dell'agent rimane nascosto.
4. Nella configurazione scegli la porta del display. Il dispositivo provato usa **COM5**.
5. Chiudi TURZX, compresa l'icona nella tray, lasciando il display acceso e collegato.
6. Premi **Collega display**. Il pannello precedente viene sostituito da PulseDeck.

Per una prova con permessi standard: `Start-PulseDeck.ps1 -StandardUser`. Alcuni
sensori potrebbero mancare. Usa **Arresta PulseDeck** nel configuratore per chiudere
l'agent e liberare la porta seriale.

Per tornare al programma TURZX, arresta PulseDeck e riapri TURZX con il suo tema.
**Arresta PulseDeck** invia il comando di spegnimento prima di chiudere la porta;
**Scollega display** libera soltanto la porta, lasciando l'ultimo fotogramma e il
dispositivo disponibili per un altro programma. PulseDeck non modifica firmware o
file dei temi originali.

Allo spegnimento, riavvio o logout di Windows, l'agent riceve la notifica di fine
sessione tramite una finestra invisibile e tenta lo spegnimento del display prima
di terminare. Annullare lo spegnimento non ferma il pannello. È necessario che
PulseDeck sia in esecuzione e abbia ancora la connessione USB: una terminazione
forzata o una perdita di alimentazione non permettono l'invio del comando.
Il modello provato cambia porta e identità USB in standby (`CT88INCH`, COM3 sul PC
di sviluppo). PulseDeck riconosce questa modalità e lo risveglia prima di collegarsi
alla porta attiva configurata, COM5. Non occorre impostare COM3 nel configuratore.
Il risveglio può richiedere alcuni secondi e un tentativo aggiuntivo: l'avvio
automatico gestisce i tentativi; dalla pagina usa di nuovo **Collega display** se
compare ancora il messaggio di risveglio.

In caso di `needReSend:1`, PulseDeck scarta la base degli aggiornamenti parziali e
ritenta con il fotogramma completo più recente. Dopo un timeout o una risposta
non valida, chiude la porta e verifica nuovamente VID/PID e identità prima del reinvio.
Sono consentiti **due tentativi**, con attese minime di 2 e 5 secondi sui successivi
aggiornamenti del renderer; nessuna coda di fotogrammi. Il budget si rinnova solo dopo
60 fotogrammi consecutivi confermati. Esauriti i tentativi, usa **Collega display**.
Il recupero automatico non risveglia dispositivi in standby. **Scollega display**
annulla anche i tentativi pendenti e quelli del launcher di avvio; l'arresto li blocca.

`GET /api/display` espone `status` (`recovering` durante il recupero),
`recoveryAttempts`, `recoveries`, `acknowledgedFrames`, `lastAcknowledgedAt` e
`lastTransportError`. Questi contatori valgono dalla connessione esplicita più recente;
`recoveryAttempts` è il budget consumato, azzerato dopo 60 conferme consecutive.
Gli esiti sono anche nel log dell'agent. Le conferme USB non sostituiscono una verifica
visiva del pannello; vedi [validazione](docs/validation.md).

## Scegliere i widget

Apri **Widget** e scegli **Compatto** (16 posizioni in una griglia 4×4) oppure
**Classico** (le otto posizioni precedenti). Musica con copertina e Discord mantengono
aree dedicate. Le associazioni esistenti sono conservate; le otto posizioni nuove
partono nascoste. Il passaggio al classico conserva anche quelle aggiuntive.

Seleziona una posizione, poi un riepilogo hardware o un sensore dell'inventario.
Puoi filtrare per nome/hardware, cambiare l'etichetta o nascondere la posizione.
Nel compatto scegli valore numerico, barra oppure anello. Per barre/anelli imposta
il valore massimo nell'unità del sensore: 100 per una percentuale, 3000 per RPM.
L'anello **RAM usata** ricava automaticamente il totale utilizzabile e mostra anche
la memoria libera; non somma la memoria virtuale né la memoria riservata all'hardware.
Per la rete scegli Download Speed/Upload Speed dell'interfaccia desiderata, evitando
di sommare le copie dei filtri virtuali. Le unità si adattano alla velocità corrente.

Premi **Salva widget** per applicare le modifiche a anteprima e display.
**Annulla modifiche** recupera l'ultimo salvataggio; **Ripristina layout iniziale**
prepara le associazioni originali e nasconde gli spazi aggiuntivi, da confermare con
il salvataggio. Un sensore mancante mostra `—` conservando il collegamento.

### Meteo e dodici sensori (0.2.3)

Installata sul PC di sviluppo con Roma selezionata: ricerca/salvataggio città,
anteprima Windows e trasmissione USB verificati. L’utente ha confermato meteo e tutti
i 12 sensori leggibili sul display fisico, senza residui o pixel anomali.

Scegli **Meteo · 12 sensori + musica e Discord** nella scheda Widget. Le prime dodici
posizioni occupano tre colonne e quattro righe; le quattro aggiuntive restano salvate
per gli altri layout. Questa disposizione rimane anche nel profilo Gaming: identità
del gioco nell’header e sfondo associato continuano a funzionare. Il layout compatto
a sedici e quello classico a otto restano selezionabili.

In **Configurazione → Meteo**, cerca un comune (per esempio «Roma, Italia»), scegli
il risultato con provincia/paese corretti e premi **Salva configurazione**. Nessuna
geolocalizzazione automatica: il servizio riceve solo la ricerca o le coordinate
della località selezionata. Il meteo viene richiesto solo usando il layout Meteo.

Il riquadro mostra temperatura, condizioni, percepita, minima/massima giornaliera,
umidità, vento e ora locale del dato. Fonte: [Open-Meteo](https://open-meteo.com/),
stime dei modelli meteorologici; località basate su GeoNames. Aggiornamento ogni
15 minuti, richieste limitate a 5 secondi e indipendenti dal rendering. Un errore
mostra «Meteo non disponibile» e cancella i valori; nuovo tentativo dopo 2 minuti.
Il cambio città cancella subito la lettura precedente. Nessuna immagine o misura
dimostrativa; le icone meteo sono disegnate dal renderer condiviso.

Sul PC di sviluppo i 12 widget conservano i dieci precedenti e aggiungono potenza
CPU Package e frequenza GPU Core, legate ai sensori reali per ID e nome.

### News nel footer (0.2.4)

Installata sul PC di sviluppo con Ultime notizie ANSA, Tecnologia ANSA e gaming PC
di Multiplayer.it attivi. Ricezione delle tre fonti e configuratore verificati;
leggibilità fisica del nuovo footer ancora da confermare.

In **Configurazione → News**, aggiungi i preset ANSA (ultime notizie, tecnologia,
sport, Lazio) e Multiplayer.it (gaming PC), oppure un feed RSS/Atom personalizzato.
Puoi salvare fino a otto canali, metterli in pausa singolarmente e attivare le news
nel footer. Premi **Salva configurazione** per applicare le modifiche.

Il display alterna fonte, titolo e data/ora ogni 20 secondi (regolabili da 10 a 120),
senza spostare meteo e sensori. **Dimensione testo** offre 20, 24 o 26 px;
il valore iniziale è 24 px. I titoli lunghi sono abbreviati mantenendo la dimensione
scelta; nel configuratore puoi leggere il titolo completo e aprire l'articolo sul
sito originale.
L'aggiornamento avviene ogni 15 minuti; in caso di errore il canale viene indicato
come non disponibile e ritentato dopo 2 minuti. Le altre fonti restano utilizzabili.
Sono accettati feed pubblici HTTPS diretti, senza autenticazione o reindirizzamenti;
RSS 2.0 e Atom. Nessun download del testo completo o delle immagini degli articoli.
Le configurazioni precedenti mantengono le news disattivate fino alla selezione.

## Copertine, sfondo e app attiva

La copertina proviene dalla miniatura della sessione multimediale Windows: non serve
un account/API key aggiuntivo. La cache conserva solo l'immagine corrente in memoria,
si svuota al cambio traccia/sorgente o alla perdita della sessione e viene ricontrollata
ogni 30 secondi (ogni 5 se manca). Se il player non fornisce un'immagine valida appare
**Copertina non disponibile**, senza riutilizzare quella di un'altra traccia.

Lo sfondo predefinito è scuro, statico, con sfumature discrete e pannelli leggibili.
In **Configurazione**, lascia vuoto il percorso per usarlo oppure scegli un PNG/JPEG/
WebP locale. L'immagine riempie il pannello con ritaglio centrale; un rapporto 4:1
permette di scegliere precisamente l'inquadratura. File fino a 32 MiB e 4 megapixel;
un'immagine illeggibile mantiene lo sfondo base con un avviso.

**L'esperimento GIF è stato abbandonato su richiesta dell'utente.** La 0.2.1 rimuove
animazione, comando UI e cadenza a 2 Hz. Vecchi file/configurazioni rimangono leggibili,
ma viene decodificato solo il primo fotogramma; il vecchio flag di animazione è ignorato.
L'aggiornamento normale resta a circa 1 Hz, senza cambiare il protocollo USB.

La testata mostra nome e icona dell'eseguibile in primo piano, ricavati da Windows.
**Gioco** compare solo se il processo corrisponde all'elenco in Configurazione; le
altre applicazioni sono indicate come **App**. Il profilo Gaming manuale non trasforma
un browser in un gioco rilevato. L'icona viene cercata localmente e conservata in una
cache limitata in memoria: nessuna immagine del gioco entra nel repository. Processi
protetti o applicazioni senza icona possono mostrare il nome e un segnaposto APP.
L'icona segue il primo piano, quindi cambia anche passando a un'altra app con Alt-Tab.
La scoperta automatica dei giochi installati non è ancora implementata.
La ricognizione successiva ha trovato nel catalogo locale della NVIDIA App le
associazioni di FC26 e Battlefield 6 ai rispettivi eseguibili. È una possibile fonte
in sola lettura per automatizzare il riconoscimento e recuperare le icone anche quando
il processo non espone il percorso; l’adapter non è ancora integrato. Icona e sfondo
panoramico sono risorse distinte: il recupero automatico delle copertine resta da fare.

### Modalità Gaming e utente Discord nell’header (0.2.2)

Il profilo Gaming usa un’area gioco/GPU a sinistra, i 16 widget al centro e musica/
Discord a destra. Conserva le associazioni e gli stili dei sensori. In **Configurazione →
Layout nel profilo Gaming** puoi scegliere di mantenere invece il layout abituale.
La scritta NVIDIA compare solo se la GPU letta dai sensori è NVIDIA; non usa un logo
copiato né modifica impostazioni del driver. FPS resta esplicitamente non disponibile.

In **Configurazione → Sfondi dei giochi**, aggiungi un processo presente nell’elenco
giochi e il percorso Windows di un PNG/JPEG/WebP locale. Nessun download automatico.
Il ritaglio centrale riempie il display e viene oscurato; valgono i limiti di 32 MiB/
4 megapixel. Vuoto o senza associazione: sfondo generale. File cancellato/non leggibile:
gradiente scuro con avviso, senza conservare l’immagine del gioco precedente.
Le associazioni restano salvate se togli un processo dall’elenco, ma non si attivano.

Sfondo e layout seguono il profilo: in automatico rispettano l’attesa configurata.
Durante un breve Alt-Tab lo sfondo conserva il gioco fino al cambio profilo, mentre
la testata continua a identificare l’app effettivamente in primo piano. Passando
direttamente fra due giochi riconosciuti cambia l’associazione al tick successivo.
Gaming manuale senza un gioco riconosciuto mostra «Nessun gioco in primo piano» e
lo sfondo generale. Le vecchie configurazioni acquisiscono i nuovi campi in memoria,
senza riscrittura del file al solo avvio.

L’header usa lo stesso **ID dell’utente da evidenziare** già configurato per Discord.
Mostra nickname e **MIC ATTIVO**, **MUTE** o **DEAF** dalla presenza corrente nel canale.
Non seleziona un altro partecipante se quello seguito manca; indica utente fuori canale,
collegamento non disponibile o utente non scelto. MIC ATTIVO indica solo assenza di mute,
non attività vocale. Non viene conservato uno stato mute precedente quando Discord cade.

La prova Aura del 17 settembre 2026 con SDK 3.07.05.0 ha enumerato i dispositivi,
ma i valori RGB non corrispondevano al giallo fisso confermato dall'utente.
La sincronizzazione non è quindi attiva: il colore resta quello manuale scelto
in **Configurazione**. Dettagli e prossimi passi nel [piano](docs/roadmap.md#aura-sync-il-pannello-segue-il-pc).

Il tentativo di far comparire PulseDeck come periferica Aura Sync è stato
**interrotto su richiesta dell'utente** il 17 settembre 2026. Il servizio ASUS
riconosceva la sonda, ma Armoury Crate non ne mostrava una scheda selezionabile.
La sonda e la sua registrazione sono state rimosse; nessun provider Aura è attivo.
Il colore manuale resta disponibile e lo sviluppo delle altre funzioni può proseguire.
Codice sperimentale ed evidenze rimangono nella [documentazione della sonda](tools/aura-probe/README.md),
senza ulteriori prove o reinstallazioni automatiche.

## Avvio automatico senza finestre

Dalla cartella pubblicata, esegui una volta come amministratore:

```powershell
./Install-PulseDeckStartup.ps1
```

Lo script crea l'attività pianificata **PulseDeck** per lo stesso utente Windows,
con privilegi elevati e ritardo di 15 secondi dopo l'accesso. Salva una copia XML e
disabilita l'attività TURZX `TempMonitor_8`; se il nome è diverso, specifica
`-TurzxTaskName` e, se necessario, `-TurzxTaskPath`.

La configurazione è già stata applicata sul PC di sviluppo. La nuova attività avvia
`Start-PulseDeck.Background.ps1`, che riusa un agent eventualmente già attivo oppure
lo avvia nascosto, aspetta che sia pronto e tenta il collegamento USB. Non apre il browser.
I tentativi USB sono limitati alla fase iniziale: se colleghi il display molto dopo,
usa il pulsante nel configuratore.

Non è un servizio Windows prima del login: musica, programma attivo e credenziali
cifrate sono associati alla sessione dell'utente. L'attività non ha un limite di durata,
evita avvii concorrenti e può ritentare tre volte dopo un'uscita anomala. Un arresto
volontario tramite il configuratore termina normalmente e non richiede il riavvio.

Per ripristinare l'avvio TURZX, da PowerShell amministratore:

```powershell
Disable-ScheduledTask -TaskName 'PulseDeck'
Enable-ScheduledTask -TaskName 'TempMonitor_8'
```

Disabilitare un'attività non arresta un programma già in esecuzione: chiudi PulseDeck
prima di avviare TURZX, così la porta seriale è libera.

## Importare Discord una volta

Arresta PulseDeck ed esegui nella cartella pubblicata:

```powershell
./Import-Discord.ps1 -SourcePath 'E:\CodeProjects\discord-overlay'
./Start-PulseDeck.ps1
```

L'importatore legge `services/discord-service/.env`, con `config.json` come fallback,
e cifra token, server e canale con Windows DPAPI CurrentUser. Conserva le altre
impostazioni e importa l'utente da evidenziare se non ne è già stato scelto uno.
Il progetto sorgente resta intatto. Ripeti l'importazione e riavvia l'agent per cambiare
bot o canale; usa lo stesso utente Windows dell'importazione.

**Integrato in PulseDeck** è la modalità predefinita. Il client Discord.Net usa gli
intent Guilds e GuildVoiceStates e legge lo stato del canale dalla cache aggiornata
dal Gateway. Non si collega all'audio e non invia messaggi. Gestisce la riconnessione;
i tentativi di login falliti vengono ripetuti dopo 30 secondi.

L'opzione **Backend esterno** conserva l'adapter HTTP per il vecchio servizio senza
JWT, su `http://127.0.0.1:5090` per impostazione predefinita. Le varianti protette da
JWT non sono supportate da questo adapter: per il progetto importato usa la modalità integrata.

## Configurazione, log e credenziali

I dati locali sono sotto `%LOCALAPPDATA%\PulseDeck`, oppure nella cartella indicata
con `PULSEDECK_DATA_DIR`:

| File/cartella | Contenuto |
| --- | --- |
| `config.json` | Preferenze del pannello, profili e collegamenti |
| `discord.credentials` | Credenziali Discord cifrate per l'utente Windows |
| `agent.stdout.log`, `agent.stderr.log` | Output dell'agent avviato dall'attività pianificata |
| `startup.log` | Esito di avvio, collegamento USB e uscita dell'agent |
| `startup-backups` | Copie XML delle attività modificate |

Codice, versioni delle dipendenze, istruzioni e script sono versionati. Token, `.env`,
dati personali, log, backup e immagini locali non devono essere aggiunti a Git.
Il token non è restituito dall'API né passato al browser.

L'API ascolta solo su loopback. Le modifiche richiedono l'header del configuratore e
le origini browser esterne sono respinte. Non esporre questa API come servizio di rete.

## Compilazione dal sorgente

Servono **Node.js 24.15+** compatibile con Angular 22 e **SDK .NET 10**. La versione Node
è in `.nvmrc`, la selezione SDK in `global.json`, i pacchetti .NET nei `.csproj` e quelli
frontend in `package.json`/`package-lock.json`.

PowerShell su Windows:

```powershell
./scripts/build.ps1
./artifacts/windows/Start-PulseDeck.ps1
```

Linux/WSL, per compilare e poi eseguire il pacchetto su Windows:

```bash
./scripts/build.sh
```

Gli script eseguono i test Core e di rendering, pubblicano l'agent win-x64 con runtime e copiano
interfaccia, licenze e script di avvio/importazione/installazione in `artifacts/windows`.
Per i test reali servono Windows e i dispositivi: la sola build in WSL non verifica
Spotify, sensori, Discord o USB. La raggiungibilità del loopback Windows da WSL dipende
dalla configurazione locale.

## Struttura e stack

- `apps/agent`: C#/.NET 10, ASP.NET Core/SignalR, SkiaSharp, LibreHardwareMonitor e Discord.Net.
- `apps/configurator`: Angular 22/TypeScript, componenti di configurazione e inventario sensori.
- `src/PulseDeck.Core`: contratti, validazione, profili e protocollo TURZX.
- `tests/PulseDeck.Core.Tests`: test dei profili e regressioni del protocollo.
- `scripts`: build, avvio, importazione credenziali e configurazione dell'avvio Windows.
- `docs`: architettura, prove effettuate e piano futuro.
- `tools/turzx-probe`: diagnostica Python iniziale; dipendenze descritte nel suo README,
  non necessarie per l'uso normale dell'agent C#.

Il driver attuale è validato sul TURZX `0525:A4A7`, `chs_88inch.dev1_rom1.90`, COM5.
Il secondo schermo da 3,5″ richiederà identificazione del modello, adapter e gestione
multipla: non è già supportato da PulseDeck.

Licenza **GPL-3.0-or-later**, incluso il codice di encoding adattato dal progetto
upstream. Vedi [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md). Non sono inclusi
programmi TURZX, temi proprietari o asset Battlefield/EA.
