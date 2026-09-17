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
- Layout compatto con 16 posizioni, valori/barre/anelli e layout classico a otto;
  associazioni precedenti conservate, nuove posizioni nascoste e scale configurabili.
- Copertine dalla sessione Windows, RAM fisica usata/libera/totale e velocità di rete.
- Sfondo scuro statico e nome/icona dell’app in primo piano da Windows (0.2.1).
- Layout Gaming dedicato, sfondi locali per gioco e nickname/mute Discord nell’header (0.2.2).
- Layout Meteo con 12 sensori, città selezionabile nell’app, condizioni Open-Meteo
  e mantenimento di Spotify/Discord (0.2.3). Prima località scelta: Roma, Italia.
- Esperimento GIF abbandonato dall’utente; rimossi animazione e selettore.
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
| In pausa | Ricerca di una sorgente Aura in lettura | Tentativo HAL virtuale interrotto dall'utente; mantenere il colore manuale | Colori dell'effetto ASUS osservabili senza acquisire il controllo né alterare i LED |
| P1 | Architettura per più display | Uno stato condiviso, schermi indipendenti | Due configurazioni con dimensioni, driver e contenuti separati; errore USB isolato per dispositivo |
| P2 | Layout liberi e per profilo (16 posizioni già disponibili) | Disporre anche musica e Discord per display/profilo | Configurazione persistente e composizione leggibile alle dimensioni di ogni schermo |
| In pausa | Tema con colori Aura | Accenti e barre coordinati al PC | Cambi colore reali, testo leggibile, comportamento corretto quando Aura non è disponibile |
| P2 | FPS e tempi dei fotogrammi | Informazioni del gioco in primo piano | Misure reali attribuite al processo corretto, confronto con uno strumento di riferimento |
| P2 | Chi parla su Discord | Indicatore vocale distinto dal mute | Due utenti, cambio interlocutore, silenzio, mute e riconnessione verificati |
| P3 | Musica e sfondi | Temi statici e associazioni per gioco/app | Cambio traccia, pausa, sorgente multimediale alternativa; sfondo nitido sul display |
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

La ricerca Aura è in pausa e non blocca il lavoro grafico con palette manuali.
La precedente priorità alla prova serviva a non progettare un tema attorno
a una sorgente di colore che potrebbe non essere leggibile.

## Aura Sync: il pannello segue il PC

**Stato attuale — 17 settembre 2026:** l'utente ha chiesto di lasciare perdere il
tentativo di far comparire una periferica virtuale in Aura Sync. Indagine sospesa:
sonda disinstallata, registrazioni proprie rimosse e assenza verificata. Restano
codice, evidenze e backup privato; nessuna reinstallazione o ulteriore prova senza
una nuova richiesta. Il colore manuale continua a funzionare. I passaggi tecnici
seguenti documentano la ricerca svolta e possibili verifiche solo per un'eventuale
ripresa esplicita; non sono attività ancora da eseguire in questa sessione.

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

### Nuova pista passiva: file degli effetti — 17 settembre 2026

L'ispezione in sola lettura di
`C:\Program Files (x86)\LightingService\script\LastScript.xml` ha trovato 11 effetti
`StaticSingleEff`, tutti con `initColor` HSL `(0.166667, 1, 0.5)`: conversione
`#FFFF00`, coerente con il giallo fisso confermato in precedenza dall'utente.
Il file contiene trigger `OneTime` e onde `ConstantWave`. Questa sorgente è più
promettente dei getter SDK provati, ma resta una descrizione su disco: non dimostra
da sola che sia attiva, aggiornata a ogni modifica o equivalente al colore istantaneo
dei LED. `LastProfile.xml` contiene invece campi colore `255` e hue `0`, quindi i due
file non vanno considerati intercambiabili o interpretati senza verifiche.

Prossima prova: osservare il file mentre l'utente sceglie almeno due colori statici
diversi in Armoury Crate e confrontare valori, timestamp e LED reali. Poi verificare
come vengono rappresentati gli effetti dinamici; leggere il colore iniziale di un
effetto non basta a seguirne l'animazione. Nessuna chiamata COM, modifica ai file ASUS,
setter, acquisizione del controllo o arresto dei servizi è servita per questa lettura.
Nessun provider abilitato nell'agent; i file proprietari non sono copiati nel repository.

### Dispositivo virtuale: prima prova di rilevamento — 17 settembre 2026

La [sonda HAL](../tools/aura-probe/README.md) crea una voce privata e una factory COM
PulseDeck visibili solo al processo di prova. L'SDK ASUS 3.07.05.0 scopre il GUID
tramite `EumerateHalInfo` e carica il modulo tramite `CreateHal`: prova superata
senza hardware aggiuntivo, privilegi amministrativi o registrazione nel sistema.
Questo verifica il primo livello software, non la comparsa tra i dispositivi Aura.

**Proseguimento della prova:** host e HAL nativi x86 hanno superato quel limite:
`EumerateDevices` restituisce **PulseDeck Virtual Probe**, dispositivo 1×1 con un
LED virtuale. Verificati nome, dimensioni e numero di LED in tre enumerazioni
consecutive, più il caso di HAL senza dispositivi. Nessuna richiesta di effetto
colore o sincronizzazione ricevuta; il prototipo non dichiara effetti supportati.
Il crash `0xC0000005` resta riproducibile nel vecchio host .NET anche con HAL nativo:
il percorso interamente nativo lo evita, senza stabilire la causa esatta.

Dopo il rilascio delle collezioni restano riferimenti COM aperti (HAL=5, dispositivo=4
alla terza enumerazione; 32/31 dopo trenta, inclusa una radice intenzionale ciascuno).
La verifica diretta dei nostri contratti, senza SDK, torna invece a 1/1/1 per cento
cicli, includendo entrambi i metodi di enumerazione. La proprietà dei riferimenti
nel percorso SDK resta da chiarire: processo breve, nessun rilascio forzato e nessuna
promessa di funzionamento continuativo.

**Ricevitore separato verificato:** un secondo processo riceve due messaggi
sintetici attraverso memoria condivisa/eventi privati; il buffer conserva solo il
valore più recente. Senza messaggi restituisce `unavailable`. La prova di timeout
ha terminato entrambi i processi osservati, senza chiavi temporanee residue. Il
trasferimento diretto delle interfacce COM ASUS tra processi non ha funzionato;
il collegamento privato evita quella dipendenza. Nessuna prova dentro LightingService.

Questa è una verifica del trasporto: la callback ricevente HAL è predisposta per
inoltrare un singolo valore grezzo come non verificato, ma **non è stata invocata
da Aura**. Ora l'SDK riconosce il descrittore `Static` (ID 1, sincronizzazione
disabilitata), verificato tramite getter su tre enumerazioni; la callback rifiuta
gli altri ID. Questo prova il descrittore, non il significato dei byte colore o
la ricezione di frame reali.

La nuova sonda `Read-ServiceCapabilities.ps1` legge solamente
`IServiceMediator.get_QueryAllDeviceCap` dal LightingService già in esecuzione,
con limite di tempo e salvataggio privato fuori Git. Lettura riuscita; la risposta
contiene voci Aura Wallpaper, ma non PulseDeck. Non installa o registra la sonda.
L'ispezione statica di `RefreshDeviceManually` mostra un percorso che distrugge
e ricrea esecutori degli effetti: non è un getter passivo e non è stato invocato.
Il controllo di firma osservato in `DoEnumerateHalInfo` riguarda il percorso
`AuraSdk_x86.dll`; non dimostra un rifiuto del nostro HAL.

**COM tra processi ora verificato nella sessione utente:** un server nativo
temporaneo espone il nostro HAL mediante `CoRegisterClassObject`, senza scrivere
registrazioni persistenti. Il client diretto legge Enumerate2/GetCapability.
Il client SDK inizialmente terminava con `0xC0000005` in AuraSdk_x86.dll+0x16b92:
nel percorso Enumerate2 richiede `IAacLedDeviceOpt2`, assente nella prima versione.
Esposte le interfacce ereditate con la corretta disposizione dei metodi, l'SDK
enumera nome, LED, dimensioni ed effetto Static per tre volte dal processo esterno.
I nuovi metodi riceventi degli effetti restano E_NOTIMPL; nessun setter viene chiamato.
Verificata la scomparsa della classe COM dopo uscita normale e arresto forzato del
nostro server. Questo supera il precedente limite del trasporto COM, senza dimostrare
il funzionamento continuativo o risolvere la proprietà dei riferimenti interni SDK.

**Confine LocalSystem verificato con un client nostro:** senza registrazione
aggiuntiva, un client SYSTEM in sessione 0 riceve `REGDB_E_CLASSNOTREG`. Con due
voci temporanee CLSID/AppID e `RunAs=Interactive User`, lo stesso client legge il
dispositivo nella sessione interattiva; anche tre enumerazioni SDK passano.
Nessuna modifica alle impostazioni globali di sicurezza COM o ai servizi ASUS.
Il supervisore elevato crea e rimuove una propria attività SYSTEM di breve durata;
non modifica l'attività PulseDeck e non sposta l'agent in sessione 0.

**Pubblicazione passiva nella categoria Aura provata:** dopo il successo dei client,
una voce HAL PulseDeck è rimasta registrata per 30 secondi. Tredici letture delle
capacità del servizio non contenevano PulseDeck; il server registrava soltanto le
due attivazioni previste dai nostri client, nessuna richiesta colore. Questo non
stabilisce un rifiuto del modulo: non è stato richiesto alcun refresh del servizio.
Chiavi temporanee e attività di prova rimosse e assenza verificata.

L'utente riferisce che non c'è un pulsante di ricerca e la scansione sembra avvenire
entrando nella pagina Aura Sync. Annunciata una seconda finestra di 60 secondi per
questa prova: 26 letture negative e nessuna attivazione aggiuntiva; l'utente conferma
che vede gli stessi dispositivi. Questo non dimostra che il servizio abbia eseguito
una nuova enumerazione degli HAL.
Un'ulteriore lettura SDK delle registrazioni reali, senza attivare gli HAL elencati,
ha trovato 17 voci con esattamente un GUID PulseDeck durante la pubblicazione:
la voce è quindi leggibile fuori dal registro privato del test.

**Installazione sperimentale rimovibile preparata:** un EXE COM separato in
`C:\Program Files\PulseDeck Aura Probe`, protetto da scrittura per gli utenti
standard, ha superato l'avvio su richiesta da SYSTEM/sessione 0, tre enumerazioni
SDK e l'uscita quando i client rilasciano i riferimenti. Usa `LocalServer32` con
identità del chiamante, senza `RunAs=Interactive User`, servizi o task permanenti.
La categoria Aura viene pubblicata solo dopo questi controlli. Rimozione completa,
rifiuto di sovrascrivere un'installazione esistente e reinstallazione verificati.

**Esito del riavvio dell'utente:** boot Windows alle 13:21:59 UTC; sonda avviata
in sessione 0 alle 13:22:10 circa. Il rapporto delle 13:22:26 registra un'attivazione,
due chiamate di enumerazione, una lettura delle capacità e due richieste di effetto.
Non erano stati eseguiti nostri test di attivazione dopo il riavvio. È evidenza
coerente con il caricamento da parte dello stack ASUS, ma il rapporto non identifica
il processo chiamante. L'utente non vede PulseDeck in Aura Sync e la lettura delle
capacità del servizio continua a non contenerne il nome. Nessun colore acquisito.

L'utente chiarisce che una periferica deve prima comparire come scheda e poi essere
selezionata per entrare in Aura Sync. Lo screenshot mostra sette schede selezionate,
nessuna PulseDeck; il pulsante «Cerca dispositivi» appartiene alla sezione Hue.
Le due callback iniziali non dimostrano quindi la partecipazione alla sincronizzazione.

**Identità nel servizio trovata:** il getter separato `get_QueryAllDevice` restituisce
15 elementi, incluso produttore PulseDeck/modello Isolated test destination, tipo
`All` e lightingname `All`. La risposta delle capacità aggrega gruppi/posizioni LED
e non conserva necessariamente il nome dell'HAL: il precedente controllo per nome
non provava l'assenza della periferica. La lettura ora riconosce anche la coppia
produttore/modello e distingue le due risposte.

Il tipo XML iniziale `0` era inadeguato a identificare una periferica concreta.
La mappa del LightingService installato associa `0x64000` (409600) a
`EXTERNAL_GENERAL`. La sonda ora usa questo tipo e il modello PulseDeck Virtual
Probe; l'enumerazione SDK verifica esattamente il nuovo tipo. Il descrittore Static
mantiene `synchronizable=0`: il servizio ne legge il valore per gli effetti, ma
l'indagine non ne ha dimostrato il ruolo nella comparsa della scheda. Non dichiarare
supporto alla sincronizzazione temporizzata senza implementarne il contratto.

**Nuovo tipo acquisito senza un altro boot:** il getter dettagliato restituisce ora
EXTERNAL_GENERAL/PulseDeck Virtual Probe, ma l'utente continua a non vedere la
scheda. Windows conserva il boot delle 13:21:59 UTC e LightingService PID 6456.
I log SDK attribuiscono l'enumerazione al servizio stesso; i log LightingService
riportano fallimento della consegna dell'effetto a PulseDeck. La sonda ha contato
otto richieste; l'ultima è SetEffect2, effetto 0, count 1, VARTYPE 8211
(VT_ARRAY | VT_UI4). Non si tratta più di metadati vecchi o di sola registrazione.

La sonda accetta ora questo preciso formato e conserva solo l'ultimo valore grezzo
con contatore e timestamp monotono, senza interpretarlo come RGB verificato.
Rifiuta effetto diverso da 0, array multidimensionali, tipi diversi, BYREF, puntatori
nulli e lunghezze discordanti. I test esercitano il decoder con dati sintetici,
non chiamano setter o callback effetto. Le altre callback restano non implementate.

Prossimo passaggio: alla prossima attivazione naturale dal servizio, verificare che
la consegna riesca e acquisire il valore ricevuto. Parallelamente verificare i
requisiti del plugin Armoury Crate per la scheda e la selezione: la categoria
EXTERNAL_GENERAL compare nelle sue mappe, ma questo non prova l'ammissibilità della
nostra periferica. Non cambiare categoria o flag di sincronizzazione senza evidenze.
Solo dopo una selezione esplicita e confronto con i LED reali si potrà parlare di
ricezione sincronizzata; il dato grezzo potrebbe anche essere una inizializzazione.
La prova di uscita/rientro nella pagina dopo l'installazione 0.4.0 è ancora negativa:
nessun processo della sonda osservato e nessuna nuova consegna nei log controllati.
Non è una prova del comportamento della nuova callback in uso dal servizio.
Non ripetere registrazioni, cambi di tipo o prove identiche di ingresso nella pagina;
serve distinguere la riattivazione dell'HAL dai criteri che producono la scheda.
Non riavviare i servizi ASUS né invocare setter RGB. Il plug-in ASUS Windows Dynamic
Lighting appartiene all'integrazione ASUS/Windows e non identifica la nostra sonda.
Nessun provider o ricevitore persistente viene installato nell'agent in questa fase.

Il modulo GmAcc installato include già una modalità virtuale, ma usa il canale
locale 11000 occupato da Aura Wallpaper e quel ramo precede quello Wallpaper.
Non è quindi una destinazione aggiuntiva indipendente verificata: nessun flag ASUS
è stato modificato. L'eventuale verifica di firme/registrazione del servizio reale
resta da accertare; l'attivazione locale della nostra factory non dimostra che il
servizio accetti un HAL di terze parti. Non sono state disabilitate verifiche di firma.

Ancora da verificare: tile Armoury Crate, ricezione dei frame colore dal controller
ASUS, transizioni statiche/dinamiche confrontate con i LED reali, rimozione pulita
e convivenza con Aura Wallpaper. Nessun provider nell'agent; è installata solo la
sonda separata descritta sopra. I servizi ASUS non sono stati arrestati o riavviati.

Passi della prova della sorgente colori:

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

La preferenza attuale dell’utente è **12 sensori e meteo a sinistra**. La 0.2.3
introduce una griglia 3×4 anche durante Gaming, con nome/icona del gioco nell’header,
sfondo per gioco e media/Discord conservati. Le altre quattro associazioni restano
salvate per gli altri layout. Sul PC sono attivi i dieci sensori precedenti più
CPU Package power e GPU Core clock; nessuna attribuzione arbitraria delle ventole
generiche della scheda madre. La 0.2.3 è installata con Roma, Italia, selezionata;
ricerca/scelta città e persistenza nel configuratore verificate su Windows,
con acquisizione indipendente dal rendering e trasmissione USB confermata dall'agent.
Le condizioni remote sono stime Open-Meteo, con fonte e ora esplicite e nessun dato
vecchio presentato dopo un errore. L’utente ha confermato la leggibilità del meteo e
di tutti i 12 sensori sul display fisico, con immagine corretta e senza anomalie.

La 0.2.0 aggiunge una griglia compatta 4×4 oltre al classico a otto posizioni.
Le associazioni persistono passando fra i layout; migrazione additiva dello schema 1,
con spazi nuovi nascosti. Valori numerici, barre e anelli selezionabili nel compatto;
RAM usata con totale fisico utilizzabile e quota libera, senza memoria virtuale.
I sensori rete rimangono associati a una specifica interfaccia per ID e nome, con
unità di velocità adattive. Anteprima e USB condividono tutta la composizione.

La 0.2.2 aggiunge una composizione Gaming dedicata (gioco/GPU, 16 widget, musica e
Discord), disattivabile senza perdere il layout base. Sfondi locali associabili per
processo dal configuratore; nessuna ricerca/download automatico di copertine. Il
contesto del gioco segue il cambio profilo e conserva lo sfondo durante un breve
Alt-Tab, senza confonderlo con l’app realmente in primo piano. NVIDIA è una scritta
basata sull’identità GPU reale; FPS non ancora implementati. Prima immagine reale
del gioco e verifica fisica del layout Gaming ancora da completare.

Nell’header compare anche nickname e mute/deaf del solo utente Discord configurato,
con stati espliciti per assenza/disconnessione. Non indica chi sta parlando.

Restano futuri il posizionamento libero, lo spostamento arbitrario di musica/Discord
e i layout per display e profilo completamente configurabili. Separare configurazione
del tema, profilo e sorgenti dati.

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

- Copertina da miniatura Windows implementata con cache singola, limiti e reset
  traccia/sorgente. Dopo la 0.2.0 l'utente ha confermato layout e copertine funzionanti
  rispondendo alla prova fisica, incluso il cambio brano; restano i cambi di player.
- **GIF abbandonate su richiesta dell'utente il 17 settembre.** La 0.2.1 rimuove
  animazione e selettore, mantiene la lettura statica dei vecchi file e usa uno sfondo
  scuro integrato. Nessun'altra prova GIF o aumento di frequenza è in programma.
- Definire regole esplicite: gioco in primo piano, riproduzione multimediale, desktop;
  mantenere override manuale e ritardo prima del cambio, già presenti.
- Acquisizione condivisa e rendering a circa 1 Hz. Il trasporto resta sincrono:
  una scrittura lenta riduce la cadenza, senza code.

## App attiva, NVIDIA e giochi installati

Richiesta dell'utente: mostrare l'icona del gioco attivo ed esplorare fonti locali,
inclusa NVIDIA, per i giochi rilevati o altri dati del PC.

- La 0.2.1 legge nome/icona del programma in primo piano da Windows. La dicitura
  Gioco usa l'elenco dei processi già configurato, con corrispondenza esatta.
  Le icone rimangono locali/in memoria. Nessuna iniezione o cattura del gioco.
- Verifica in sola lettura con `nvidia-smi`: RTX 5070 Ti, driver 610.88; disponibili
  utilizzo, temperatura, VRAM e potenza. Molti dati sono già coperti dall'inventario
  LibreHardwareMonitor. Questo controllo non aggiunge un nuovo provider NVIDIA.
- NVAPI DRS documenta profili e applicazioni associati al driver. Un profilo non
  dimostra l'installazione locale del gioco. Nella ricerca svolta non è stata trovata
  un'API pubblica documentata per replicare la libreria rilevata dalla NVIDIA App.
- Futuro: valutare dati locali dei launcher e verifica del percorso installato, con
  provenienza esplicita; evitare di presentare l'intero catalogo del driver come
  giochi presenti. Nessuna scansione estesa dei dischi o modifica dei profili NVIDIA
  è stata introdotta. App in primo piano, gioco installato e sessione di gioco sono
  concetti distinti; un launcher o un programma che usa la GPU non basta.
- NVML può fornire ulteriori dati GPU dove supportati; FPS/frame-time restano un
  adapter separato, con attribuzione al processo reale tramite PresentMon.

### Chiarimento e ricognizione locale delle immagini — 17 settembre 2026

L’utente intende il recupero automatico di icone/copertine, come nella NVIDIA App;
il percorso manuale introdotto in 0.2.2 resta un override, non soddisfa da solo questa
richiesta. FC26 è stato aggiunto all’elenco locale dei giochi su conferma esplicita,
con backup e senza modificare bf6/Battlefield o le altre preferenze.

La ricognizione in sola lettura ha trovato `%LOCALAPPDATA%/NVIDIA Corporation/NVIDIA
app/NvBackend/ApplicationStorage.json`: i record di Battlefield 6 e EA SPORTS FC 26
contengono nome, ShortName, CmsId, directory installata e percorsi degli eseguibili
in `ImageFiles`/`DetectedFiles`. `ImageFiles` indica qui EXE, non copertine panoramiche.
La registrazione Windows di disinstallazione conferma entrambi i percorsi. Estrarre
l’icona direttamente dal file FC26 riesce (32×32 nella prova), mentre leggere il
percorso dal processo in esecuzione era fallito: introdurre un fallback ai percorsi
installati verificati, senza attach/iniezione o esecuzione del gioco.

Prossimo incremento: adapter limitato al catalogo locale, parsing difensivo, verifica
del file/associazione e invalidazione della cache; quel JSON è un formato interno,
non un’API NVIDIA pubblica garantita. Leggere anche le sorgenti dei launcher dove
disponibili. La cache Steam esiste su questo PC e Steam distingue esplicitamente
icone, capsule e immagini hero; serve legare l’asset al titolo esatto. I 17 PNG 456×253
nella cartella assets NVIDIA includono grafica promozionale, non dimostrano uno sfondo
di FC26 utilizzabile. Non assegnare immagini in base al solo nome generico o alla
vicinanza sul disco. Copertina automatica ad alta risoluzione ancora non verificata;
fallback previsto: icona verificata su sfondo scuro. Nessun file NVIDIA/Steam/EA
modificato, nessun asset copiato nel repository.

Riferimenti: [icone Windows](https://learn.microsoft.com/en-us/windows/win32/api/shlobj_core/nn-shlobj_core-iextracticonw),
[asset della libreria Steam](https://partner.steamgames.com/doc/store/assets/libraryassets).

Fonti: [NVIDIA Driver Settings API](https://docs.nvidia.com/nvapi/group__drsapi.html),
[NVML device queries](https://docs.nvidia.com/deploy/nvml-api/api/group__nvmlDeviceQueries.html),
[Windows executable icons](https://learn.microsoft.com/en-us/dotnet/api/system.drawing.icon.extractassociatedicon?view=windowsdesktop-10.0).

## Come riprendere

Leggere `AGENTS.md` e `docs/validation.md`, verificare che non ci siano modifiche
locali da sovrascrivere, poi completare le prove fisiche USB e proseguire con
l'architettura multi-display, FPS/Discord e temi/regole. La ricerca di una sorgente
Aura passiva resta isolata dall'agent, tenendo conto dell'esito SDK sopra. Aggiornare
questo piano con risultati e limiti osservati. Ogni incremento deve lasciare il
pannello utilizzabile e avere un commit semantico con la validazione pertinente.

## News nel footer (0.2.4)

Richiesta: fonti RSS selezionabili, URL personalizzati e preset pronti. Implementati
cinque preset dai cataloghi ufficiali ANSA (ultime notizie, tecnologia, sport, Lazio)
e Multiplayer.it (news PC), fino a otto canali attivabili singolarmente e interruttore
per il footer. Scelta iniziale esplicita: ultime notizie ANSA, tecnologia ANSA e gaming
PC. Le fonti si alternano ogni 20 secondi, regolabili da 10 a 120, senza animazione
continua e senza spostare meteo o sensori. Testo ingrandito a 24 px su richiesta,
con scelta 20/24/26 px nell’app. Link agli articoli completi nel configuratore.

RSS 2.0/Atom pubblici HTTPS diretti, nessuna chiave o scraping degli articoli. Solo
titoli con fonte/data (data assente dichiarata), fino a dieci recenti per canale,
cache in memoria e aggiornamento ogni quindici minuti. Errori/assenza di notizie
espliciti, richieste indipendenti dal rendering. La prova sul display fisico del
footer a 24 px ha confermato dimensione e leggibilità adeguate; la rotazione è stata
osservata nell’anteprima Windows. La 0.2.4 è installata: tre fonti
collegate, URL personalizzati e persistenza verificati dal configuratore Windows.

Fonti dei preset: [ANSA RSS](https://www.ansa.it/sito/static/ansa_rss.html),
[Multiplayer.it feed](https://multiplayer.it/feed/).
