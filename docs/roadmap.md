# Piano di sviluppo PulseDeck

Aggiornato il 17 settembre 2026. Questo file conserva decisioni, priorità e verifiche
per riprendere il lavoro nelle prossime sessioni. Le attività future non sono già
implementate e non richiedono di installare tutte le dipendenze in anticipo.

## Obiettivo

Un pannello TURZX 1920×480 che combina sensori reali, musica, informazioni del gioco
e Discord, con contenuti e colori adatti al contesto. Un solo avvio in background
nella sessione Windows dell'utente, senza console aperta.

## Base già disponibile

- Agent C#/.NET 10, configuratore Angular 22, rendering unico con SkiaSharp.
- Trasporto TURZX verificato sul dispositivo, aggiornamenti completi e parziali.
- Inventario con 617 sensori/parametri rilevati su questo PC; ricerca e filtri.
- PawnIO installato e letture di CPU, scheda madre, ventole, RAM, GPU e dischi.
- Spotify tramite sessioni multimediali Windows; profili Desktop/Musica/Gaming.
- Discord integrato: bot collegato, cache dei partecipanti e stato mute/deaf.
  Le transizioni reali con partecipanti devono ancora essere verificate.
- Avvio tramite attività pianificata nella sessione utente, con privilegi elevati,
  processo nascosto, log su file e connessione iniziale al display.

## Ordine proposto

| Priorità | Attività | Risultato atteso | Verifica per considerarla conclusa |
| --- | --- | --- | --- |
| P0 | Prove della base | Avvio quotidiano affidabile | Accesso Windows reale, arresto volontario, Spotify, display; ingresso/uscita e mute/deaf con partecipanti Discord |
| P1 | Prova Aura Sync in lettura | Capire se il pannello può seguire i colori RGB del PC | Colori dell'effetto ASUS osservabili senza acquisire il controllo né alterare i LED |
| P1 | Architettura per più display | Uno stato condiviso, schermi indipendenti | Due configurazioni con dimensioni, driver e contenuti separati; errore USB isolato per dispositivo |
| P1 | Scelta dei widget e layout | Scegliere quali sensori e informazioni mettere sul TURZX | Configurazione persistente, anteprima coerente, nessuna sovrapposizione a 1920×480 |
| P2 | Tema con colori Aura | Accenti e barre coordinati al PC | Cambi colore reali, testo leggibile, comportamento corretto quando Aura non è disponibile |
| P2 | FPS e tempi dei fotogrammi | Informazioni del gioco in primo piano | Misure reali attribuite al processo corretto, confronto con uno strumento di riferimento |
| P2 | Chi parla su Discord | Indicatore vocale distinto dal mute | Due utenti, cambio interlocutore, silenzio, mute e riconnessione verificati |
| P3 | Musica e sfondi | Copertina, progresso e tema Battlefield | Cambio traccia, pausa, sorgente multimediale alternativa; sfondo nitido sul display |
| P3 | Regole contestuali avanzate | Widget/profili diversi per app attiva | Priorità esplicite e nessun cambio continuo durante Alt-Tab |
| P3 | Gestione quotidiana | Icona nella tray, recupero del display e aggiornamenti | Uscita senza processi residui, scollegamento USB, sospensione/ripresa, ripristino del colore |

La prova Aura precede il lavoro grafico esteso: evita di progettare un tema attorno
a una sorgente di colore che potrebbe non essere leggibile.

## Aura Sync: il pannello segue il PC

**Decisione dell'utente:** la prima modalità deve seguire i colori già impostati
sul PC. PulseDeck non deve diventare il controller dell'illuminazione.

Ricognizione locale del 17 settembre: `LightingService`, `ArmouryCrateService` e
`Aura Wallpaper Service` attivi; DLL Aura SDK x64/x86 presenti nella cartella ASUS.
Questo dimostra la presenza dei componenti, non la compatibilità dell'integrazione.

La documentazione ASUS descrive un'interfaccia COM utilizzabile da C#, proprietà
RGB leggibili/scrivibili e acquisizione/rilascio del controllo. I getter non provano
che sia possibile leggere passivamente l'effetto applicato da un altro programma:
potrebbero riflettere soltanto lo stato mantenuto dall'SDK. Questo è il punto da
risolvere nella prova, prima di promettere una sincronizzazione completa.

Fonti ufficiali consultate:

- [Tutorial C# Aura SDK](https://www.asus.com/microsite/aurareadydevportal/tutorial_csharp.html).
- [Interfaccia IAuraRgbLight](https://www.asus.com/microsite/aurareadydevportal/interface_aura_service_lib_1_1_i_aura_rgb_light.html).

Passi della prova:

1. Verificare interfacce e registrazione COM della versione effettivamente installata,
   compatibilità con .NET 10 x64 e accesso dalla sessione interattiva.
2. Enumerare e leggere, se consentito, senza chiamare `SwitchMode`, `Apply` o setter
   dei LED. Non fermare Armoury Crate o il servizio ASUS per ottenere il controllo.
3. Confrontare le letture con un colore statico e un effetto dinamico selezionati
   dall'utente in Armoury Crate. Distinguere una lettura aggiornata da un buffer fermo.
4. Se la lettura passiva funziona, introdurre un `AuraColorProvider` con stato,
   timestamp e colore sorgente, separato dal rendering e dagli altri provider.
5. Se non funziona, documentare il limite e mantenere il colore manuale. Eventuali
   alternative dovranno rispettare la direzione PC → PulseDeck; non sostituire
   automaticamente Armoury Crate o modificare l'illuminazione del PC.

Per il tema: scegliere un dispositivo/LED di riferimento o una regola esplicita
di riduzione della palette. Smussare i cambi rapidi, mantenere contrasto e leggibilità
dei testi e limitare il campionamento in base al costo misurato. Lo stato offline
deve usare un fallback dichiarato, senza presentare l'ultimo colore come aggiornato.

## Widget e temi

Prima versione: posizioni predefinite e selezione dei contenuti, evitando di rendere
obbligatorio un editor drag-and-drop completo. Le aree possono contenere metriche,
musica, FPS o Discord. Separare configurazione del tema, profilo e sorgenti dati.

- ID dei widget stabili e configurazione versionata con migrazione.
- Binding dei sensori che distingua anche identificatori duplicati nella libreria.
- Scale/unità esplicite; nessun dato simulato quando un sensore non è disponibile.
- Stessa composizione per anteprima e USB, con controllo di margini e dimensioni.
- Palette manuale e Aura selezionabili; sfondo e testo rimangono leggibili.

## Più schermi e possibile TURZX da 3,5 pollici

**Richiesta dell'utente:** prevedere almeno due display, incluso il possibile acquisto
di un 3,5 pollici 320×480 da affiancare all'8,8 pollici attuale.

PulseDeck oggi usa un solo `TurzxDisplay` e un canvas 1920×480. Il supporto multiplo
richiede un incremento esplicito; non basta inserire una seconda porta COM.

- Introdurre una collezione di configurazioni display con identità, driver, dimensioni,
  orientamento, profilo e tema. Migrare la configurazione del singolo schermo esistente.
- Acquisire sensori, Spotify e Discord una volta sola e condividere le letture.
- Rendere dimensioni e composizione parametriche, con layout specifici per 1920×480
  e 320×480/480×320; evitare di ridurre semplicemente il tema grande.
- Separare connessione, rendering, invio, timeout e stato di ciascuno schermo.
  Uno schermo lento o scollegato non deve bloccare l'altro.
- Riconoscere i dispositivi mediante identificatori disponibili, gestendo anche
  seriali mancanti/duplicati e cambi di porta; prevedere associazione manuale.
- Nel configuratore scegliere lo schermo, vedere la sua anteprima e collegarlo
  individualmente. All'accesso connettere ciascun dispositivo configurato.
- Per il 3,5 pollici identificare la revisione e implementare/testare il relativo
  adapter. La dicitura USB Type-C non identifica il protocollo del dispositivo.

Il progetto upstream documenta diversi modelli da 3,5 pollici, con protocolli diversi
da quelli dell'8,8 attuale. La famiglia è quindi una candidata plausibile, ma il titolo
di un annuncio non garantisce la compatibilità dell'esemplare venduto.

Fonti di riferimento: [revisioni hardware](https://github.com/mathoudebine/turing-smart-screen-python/wiki/Hardware-revisions)
e [mappatura dei modelli](https://github.com/mathoudebine/turing-smart-screen-python/blob/main/configure.py).

Uso proposto: schermo grande per prestazioni/gioco, piccolo per Spotify o Discord,
con testo ampio e pochi elementi. Prima dell'acquisto verificare l'annuncio esatto,
software fornito, foto del retro e revisione.

Annuncio ricevuto: [AliExpress 1005008850981488](https://it.aliexpress.com/item/1005008850981488.html).
Consultato il 17 settembre 2026: titolo 3,5 pollici 320×480 con software TURZX,
marca nelle specifiche **SHCHV**, venditore IceCrab Global Store, prezzo mostrato
17,15 EUR per la variante Black. Il prezzo può variare. L'annuncio da solo non
identifica il protocollo; il successivo controllo del software ha fornito elementi
più precisi. Le sintesi generate dalla piattaforma non costituiscono prova tecnica.

### Software del venditore: verifica del 17 settembre 2026

Cartella fornita dall'utente: [download del venditore](https://drive.google.com/drive/folders/1KMiDKiGRQc3uLGeVeZ--59YLoGJu5pUN).
È stato esaminato `35inchENG.rar` (23.533.528 byte, file Drive
`14FQQ-vAjWnH4UNGQzyo21kBbnrGGbFut`), SHA-256
`f4eb676c05d893ca4456c5a4078ae95b9434da4449f60d316ec8ec30f8d6a891`.

Risultati dell'ispezione statica, senza eseguire/installare il software:

- Contiene `UsbMonitor.exe` e `Driver/usbser/cdc.inf`; quest'ultimo usa il driver
  seriale USB CDC di Windows, senza identificare uno specifico VID/PID.
- L'eseguibile apre una porta seriale a 115200 baud, 8N1, e gestisce 320×480/480×320.
- Formato del comando a 6 byte, impacchettamento delle coordinate e comando bitmap
  197 coincidono con il driver upstream `lcd_comm_rev_a.py`.
- Comando orientamento 121, pacchetto a 16 byte, orientamento con offset 100 e
  dimensioni corrispondono al driver upstream; presente anche il comando mirror 122.

Conclusione: forte evidenza che il pacchetto controlli la famiglia Turing 3,5 pollici
revision A, con protocollo già documentato e quindi una base concreta per l'adapter
C#. Nessuna prova su un esemplare fisico: va ancora confermata la corrispondenza tra
il software pubblicato e la revisione effettivamente spedita. PulseDeck non supporta
ancora questo display finché non vengono implementati adapter, layout e gestione multipla.

Riferimento: [driver revision A upstream](https://github.com/mathoudebine/turing-smart-screen-python/blob/main/library/lcd/lcd_comm_rev_a.py).
Archivio ed eseguibile proprietari restano fuori dal repository; viene versionato
soltanto questo resoconto dei fatti osservati.

Criterio di completamento: due schermi fisici con contenuti differenti, riavvio,
scollegamento di uno solo, scambio di porte e continuità dell'altro verificati.

## FPS e Discord

**FPS:** valutare l'integrazione PresentMon disponibile al momento dell'implementazione,
identificare il PID del gioco in primo piano e distinguere FPS dell'applicazione,
fotogrammi visualizzati e frame time. Verificare privilegi, distribuzione dei componenti,
costo del campionamento e giochi compatibili. Il dato non deve mescolare launcher,
desktop, browser e gioco. Rimuovere le letture vecchie quando il gioco termina.

**Discord:** prima confermare ingresso/uscita e mute/deaf nell'integrazione attuale.
Poi provare il trasporto Voice compatibile con il protocollo Discord corrente e gli
eventi necessari per identificare chi parla. La connessione del bot al canale audio
va resa visibile nella configurazione; non servono registrazione o conservazione audio.
Non dedurre la voce attiva da mute=false. In caso di incompatibilità mostrare
"attività vocale non disponibile", mantenendo utilizzabili partecipanti e mute/deaf.

## Musica, Battlefield e regole

- Usare la miniatura della sessione Windows per la copertina; gestire cache, cambio
  traccia e sorgenti multiple senza rendere necessario un account Spotify aggiuntivo.
- Prima uno sfondo statico scelto dall'utente; scene/video Battlefield solo dopo una
  misura della banda USB e del carico di rendering. Gli asset restano separati dal codice.
- Definire regole esplicite: gioco in primo piano, riproduzione multimediale, desktop;
  mantenere override manuale e ritardo prima del cambio, già presenti.
- Separare frequenza di acquisizione dei sensori, aggiornamento UI e invio al display.

## Come riprendere

Leggere `AGENTS.md` e `docs/validation.md`, verificare che non ci siano modifiche
locali da sovrascrivere, poi iniziare dalla prova Aura in sola lettura. Aggiornare
questo piano con risultati e limiti osservati. Ogni incremento deve lasciare il
pannello utilizzabile e avere un commit semantico con la validazione pertinente.
