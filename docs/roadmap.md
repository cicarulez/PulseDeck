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
- Recupero USB limitato: due tentativi con frame completo, identità verificata alla
  riapertura e cancellazione per disconnessione volontaria/arresto; diagnostica API/log.
- Inventario con 617 sensori/parametri rilevati su questo PC; ricerca e filtri.
- Selezione dei sensori nelle otto posizioni del layout, etichette, scale e visibilità;
  configurazione persistente e valori originali per le configurazioni precedenti.
- PawnIO installato e letture di CPU, scheda madre, ventole, RAM, GPU e dischi.
- Spotify tramite sessioni multimediali Windows; profili Desktop/Musica/Gaming.
- Discord integrato: bot collegato, cache dei partecipanti e stato mute/deaf.
  Le transizioni reali con partecipanti devono ancora essere verificate.
- Avvio tramite attività pianificata nella sessione utente, con privilegi elevati,
  processo nascosto, log su file e connessione iniziale al display.
- Gestione della fine sessione Windows e comando di spegnimento del TURZX prima
  dell'uscita; annullamento della fine sessione senza interrompere il pannello.

## Ordine proposto

| Priorità | Attività | Risultato atteso | Verifica per considerarla conclusa |
| --- | --- | --- | --- |
| P0 | Prove della base | Avvio quotidiano affidabile | Accesso Windows reale, arresto volontario, Spotify, display; ingresso/uscita e mute/deaf con partecipanti Discord |
| P0 | Affidabilità USB | Recupero limitato implementato; completare prove fisiche | Errori reali, scollegamento/ricollegamento USB, sospensione/ripresa e immagine corretta dopo recupero |
| P1 | Ricerca di una sorgente Aura in lettura | Superare l'esito negativo della prima prova SDK | Colori dell'effetto ASUS osservabili senza acquisire il controllo né alterare i LED |
| P1 | Architettura per più display | Uno stato condiviso, schermi indipendenti | Due configurazioni con dimensioni, driver e contenuti separati; errore USB isolato per dispositivo |
| P2 | Layout oltre le otto posizioni disponibili | Disporre anche musica e Discord per display/profilo | Configurazione persistente e composizione leggibile alle dimensioni di ogni schermo |
| P2 | Tema con colori Aura | Accenti e barre coordinati al PC | Cambi colore reali, testo leggibile, comportamento corretto quando Aura non è disponibile |
| P2 | FPS e tempi dei fotogrammi | Informazioni del gioco in primo piano | Misure reali attribuite al processo corretto, confronto con uno strumento di riferimento |
| P2 | Chi parla su Discord | Indicatore vocale distinto dal mute | Due utenti, cambio interlocutore, silenzio, mute e riconnessione verificati |
| P3 | Musica e sfondi | Copertina, progresso e tema Battlefield | Cambio traccia, pausa, sorgente multimediale alternativa; sfondo nitido sul display |
| P3 | Regole contestuali avanzate | Widget/profili diversi per app attiva | Priorità esplicite e nessun cambio continuo durante Alt-Tab |
| P3 | Gestione quotidiana | Icona nella tray, recupero del display e aggiornamenti | Uscita senza processi residui, scollegamento USB, sospensione/ripresa, ripristino del colore |

Il `needReSend:1` osservato durante la prova widget è ora gestito reinviando il
fotogramma completo più recente; i timeout richiedono riapertura e nuova verifica
dell'identità. Due tentativi, dopo almeno 2 e 5 secondi, poi **Collega display**.
Il budget si rinnova dopo 60 frame consecutivi confermati, non dopo un singolo
successo. Disconnessione volontaria e arresto annullano il recupero; nessun risveglio
automatico in questa fase. Test con trasporto simulato coprono fallimenti e cancellazione;
le evidenze Windows e i limiti delle prove fisiche sono in `docs/validation.md`.
La 0.1.1 installata ha recuperato autonomamente un timeout reale al primo tentativo;
verificati anche arresto dell'agent e risveglio COM3 → COM5. L'utente ha confermato
visivamente l'immagine completa e corretta, con dati aggiornati e senza residui o
pixel anomali dopo il recupero. Restano una richiesta di reinvio reale e le prove
di cavo/sospensione.

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
- [Interfaccia IAuraSdk](https://www.asus.com/microsite/aurareadydevportal/interface_aura_service_lib_1_1_i_aura_sdk.html).

### Esito della prima prova — 17 settembre 2026

SDK installato **3.07.05.0**, COM `aura.sdk.1`, DLL x64 nella cartella ASUS.
Una sonda PowerShell separata ha chiamato solo `Enumerate(0)` e getter dei LED,
senza `SwitchMode`, setter o `Apply`, lasciando attivi i servizi ASUS.
Il processo non elevato è terminato con codice 9 durante l'enumerazione; quello
elevato ha enumerato RAM, GPU, periferiche, scheda madre e strip.

Tre campioni consecutivi dei primi LED hanno prodotto valori fermi, incoerenti con
il **giallo fisso** confermato dall'utente; alcuni erano compatibili con contenuto di
buffer non valido. L'enumerazione riuscita non dimostra quindi una lettura dell'effetto
attivo. Nessun provider Aura è stato collegato al rendering e il colore manuale resta
attivo. Non è stata eseguita la prova dinamica, perché già il confronto statico falliva.

Il requisito documentato dell'SDK di acquisire il controllo prima delle altre
operazioni rende questa strada insufficiente per la modalità passiva richiesta.
Un'alternativa dovrà essere verificata separatamente senza cambiare l'illuminazione.
Qualunque ulteriore sonda nativa deve restare in un processo isolato dall'agent.

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

Prima versione completata: tre barre, quattro valori centrali e un valore laterale
possono mostrare un riepilogo hardware o un sensore scelto per ID e nome, oppure
essere nascosti. Etichette e scale modificabili, salvataggio esplicito e ripristino
dei valori iniziali; stesso rendering per anteprima e USB. Schema 1 compatibile con
i file precedenti tramite valori predefiniti per la nuova proprietà `widgets`.

Restano futuri il posizionamento libero, lo spostamento di musica/Discord e i layout
per display e profilo. Separare configurazione del tema, profilo e sorgenti dati.

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
locali da sovrascrivere, poi completare le prove fisiche USB e proseguire con
l'architettura multi-display, FPS/Discord e temi/regole. La ricerca di una sorgente
Aura passiva resta isolata dall'agent, tenendo conto dell'esito SDK sopra. Aggiornare
questo piano con risultati e limiti osservati. Ogni incremento deve lasciare il
pannello utilizzabile e avere un commit semantico con la validazione pertinente.
