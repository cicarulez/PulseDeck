# PulseDeck

Un pannello locale per Windows che porta sensori hardware, musica, informazioni di
Discord e profili contestuali sul display TURZX 8,8″ 1920×480.

Il configuratore è disponibile su **http://127.0.0.1:5178**. Puoi chiudere il browser:
l'agent continua a lavorare in background. L'avvio automatico non apre né console
né browser e collega il display.

Il [piano di sviluppo](docs/roadmap.md) raccoglie le prossime attività: Aura Sync,
layout avanzati, più schermi, FPS, attività vocale e temi. Le verifiche realmente
eseguite sono descritte in [docs/validation.md](docs/validation.md).

## Funzionalità disponibili

- Anteprima e display condividono lo stesso rendering 1920×480, con sfondo locale.
- Pagina **Sensori** con ricerca, filtri, valori correnti/minimi/massimi e diagnostica
  dei permessi. Sul PC di sviluppo: 617 sensori e parametri, incluse soglie dei dispositivi.
- Pagina **Widget**: otto posizioni configurabili con sensori, etichette e scale delle barre.
- Titolo, artista, stato e progresso di Spotify o altri player compatibili con Windows.
- Profili Desktop, Gaming e Musica; selezione manuale o automatica con ritardo configurabile.
- Bot Discord integrato: partecipanti del canale, mute/deaf e utente da evidenziare.
- Collegamento TURZX con verifica dell'identità e aggiornamenti completi/parziali.
- Avvio nascosto all'accesso Windows, con permessi amministrativi e log su file.

Le letture mancanti appaiono come `—`. Il bot collegato è stato verificato; le
transizioni ingresso/uscita e mute/deaf con partecipanti richiedono ancora una prova.

**Non ancora disponibili:** FPS, indicatore di chi parla, colori Aura Sync, più display,
copertine musicali, sfondi video, layout libero e icona nella tray. Il solo valore
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
Il display mantiene l'ultimo fotogramma finché non riceve nuovi dati. PulseDeck non
modifica firmware o file dei temi originali.

## Scegliere i widget

Apri **Widget** nel configuratore, scegli una delle otto posizioni (tre barre a
sinistra, quattro valori centrali e un valore a destra) e seleziona un riepilogo
hardware o un sensore dell'inventario. Puoi filtrare per nome/hardware, cambiare
l'etichetta, impostare il valore a barra piena oppure nascondere la posizione.
Per esempio, una ventola in RPM può usare una scala massima di 3000.

Premi **Salva widget** per applicare le modifiche all'anteprima e al display.
**Annulla modifiche** recupera l'ultimo salvataggio; **Ripristina layout iniziale**
prepara i valori originali, da confermare con il salvataggio. La configurazione
rimane su disco e le configurazioni precedenti ricevono automaticamente le otto
associazioni originali. Un sensore mancante mostra `—`, conservando il collegamento.
Le aree musica e Discord restano nelle loro posizioni attuali.

La prova Aura del 17 settembre 2026 con SDK 3.07.05.0 ha enumerato i dispositivi,
ma i valori RGB non corrispondevano al giallo fisso confermato dall'utente.
La sincronizzazione non è quindi attiva: il colore resta quello manuale scelto
in **Configurazione**. Dettagli e prossimi passi nel [piano](docs/roadmap.md#aura-sync-il-pannello-segue-il-pc).

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

Gli script eseguono i test Core, pubblicano l'agent win-x64 con runtime e copiano
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
