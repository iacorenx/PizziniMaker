# PizziniMaker

PizziniMaker è un'applicazione desktop portabile per Windows 10 e Windows 11 destinata alla registrazione giornaliera delle ore lavorative. Riprende l'organizzazione e l'aspetto di un piccolo foglio di carta A6 e consente di acquisire, conservare ed esportare le attività svolte.

> **Stato del progetto:** versione 1.4.0 implementata, con giorni stampati segnati come consegnati, attività MANDATO e PREVENTIVO, note spese con allegati e chilometri, stampa/PDF/Word dei giorni in foglietti A6, aggiornamento da GitHub dal menu dell'icona, importazione automatica alla prima installazione, commenti e storico con suggerimenti, ore di viaggio separate, sabato facoltativo, trasferta, attività personalizzate, chiusura a 8 ore, calendario mensile, festività nazionali e test automatici. Evoluzione multi-dipendente: [docs/ROADMAP.md](docs/ROADMAP.md).

## Obiettivi

- Rendere l'inserimento delle ore rapido e immediatamente accessibile durante la giornata.
- Conservare bozze, giornate confermate e impostazioni senza dipendere da servizi cloud.
- Generare un log testuale leggibile per ogni giorno confermato.
- Segnalare i giorni lavorativi mancanti e mostrare chiaramente se una settimana è in pari o completa.
- Consentire il trasferimento dell'intera applicazione su un altro PC copiando una sola cartella.

## Requisiti funzionali

### Avvio e accessibilità

- Avvio automatico facoltativo con Windows per l'utente corrente, senza privilegi di amministratore.
- Applicazione richiamabile dall'area di notifica di Windows.
- Opzione per mantenere la finestra sempre in primo piano.
- Nessun servizio in background separato, database server o componente cloud.
- Menu dell'icona (tasto destro): apertura, stampa giorni, cartella log, **Verifica aggiornamenti…**, versione installata con crediti, uscita.

### Aggiornamenti e installazione

- Le versioni sono pubblicate come release GitHub del repository pubblico `iacorenx/PizziniMaker` con due file: `PizziniMaker-X.Y.Z-win-x64.zip` e `PizziniMaker-X.Y.Z-win-x64.zip.sha256`.
- **Verifica aggiornamenti…** interroga `api.github.com` senza token né dati personali (solo la versione nel campo User-Agent), confronta la versione, mostra le note e, su conferma, scarica lo ZIP, ne verifica lo SHA-256, crea `Backup/Pre-aggiornamento-*.zip`, rinomina l'eseguibile in uso in `PizziniMaker.exe.old`, copia i file del programma e riavvia con `--dopo-aggiornamento`. I file `*.old` sono eliminati all'avvio successivo. Dati, log, note spese, impostazioni e storico non vengono mai sostituiti; in caso di errore i file originali vengono ripristinati.
- Controllo automatico facoltativo all'avvio, al massimo una volta al giorno, solo con fumetto di avviso.
- Nuova cartella: al primo avvio vengono cercate versioni precedenti (cartelle sorelle, Desktop, Documenti, profilo, Download) e proposta l'importazione completa, preferenze comprese.

### Dipendente

- Al primo avvio viene richiesto il nome del dipendente.
- Il nome è un'impostazione persistente, modificabile successivamente dalla schermata delle impostazioni.
- Il nome compare nell'intestazione del foglio e dei log testuali.
- Il file delle impostazioni e tutti i dati personali sono esclusi dal repository Git.

### Navigazione tra i giorni

- Due piccole frecce, sinistra e destra, permettono di scorrere i giorni.
- La data viene mostrata in italiano con il nome del giorno, ad esempio `lunedì 16/11/2026`.
- Le bozze vengono salvate automaticamente quando si cambia giorno o si chiude l'applicazione.
- I giorni futuri possono essere visualizzati, ma non generano avvisi di incompletezza.

### Righe di attività

Ogni giornata contiene una o più righe modificabili. Ogni riga prevede:

- un elenco a discesa per il tipo di attività;
- un elenco a discesa per le ore, con incrementi di 30 minuti;
- controlli specifici visualizzati soltanto quando pertinenti;
- possibilità di aggiungere ed eliminare righe prima della conferma.

Tipi di attività supportati:

| Attività | Numero scheda | Ore | CAD | Scheda cartacea |
| --- | --- | --- | --- | --- |
| `SCHEDA` | Obbligatorio, esattamente 4 cifre | Sì | Sì | Sì |
| `MANDATO` ✎ | Obbligatorio, numero/lettera (`123/X`, 1–6 cifre e una lettera maiuscola) | Sì | Sì | Sì |
| `PREVENTIVO` ✉ | Obbligatorio, numero/anno (`1234/2026`, 1–6 cifre e anno a 4 cifre precompilato) | Sì | Sì | Sì |
| `QU` | No | Sì | No | No |
| `FERIE/PERMESSO` | No | Sì | No | No |
| `MALATTIA` | No | Sì | No | No |
| `INFORTUNIO` ⚠ | No | Sì | No | No |
| `VIAGGIO` 🚗 | No | Sì, contabilità separata | No | No |
| Personalizzata | No | Sì | No | No |

È inclusa inizialmente `FORMAZIONE` 🎓. Dalla 1.2.0 la trasferta è una casella della giornata (🧳, pernotto fuori sede) e non un'attività con ore: la voce `TRASFERTA` viene rimossa dal catalogo con la migrazione delle impostazioni (schema 3), mentre le giornate già salvate restano invariate. Dalle impostazioni è possibile aggiungere, rinominare o eliminare attività personalizzate e assegnare loro un'icona. Nome e icona vengono conservati anche nella singola riga storica, così le modifiche al catalogo non alterano i giorni precedenti.

Per le attività di tipo `SCHEDA`:

- il numero deve rispettare il formato numerico di quattro cifre, inclusi eventuali zeri iniziali;
- la casella **CAD** è selezionata per impostazione predefinita;
- la casella **Segnata su scheda cartacea** è deselezionata per impostazione predefinita;
- le due caselle sono indipendenti;
- un **commento** facoltativo (massimo 60 caratteri) descrive la scheda; mentre si scrive vengono proposte le schede dello storico (`Bac` → `8092 — Baccane`), selezionabili con mouse, frecce e Invio;
- lo storico (`StoricoSchede.json`) contiene una voce per numero con commento, ultimo utilizzo e numero di utilizzi; viene aggiornato alla conferma del giorno, è gestibile dalle impostazioni e viene ricostruito dalle giornate confermate se manca.

### Conferma, blocco e riapertura

- Il pulsante **Conferma giorno** valida e acquisisce le attività del giorno.
- Sotto le 8 ore viene proposto il completamento esatto con `QU` o `FERIE/PERMESSO`; annullando la bozza resta invariata.
- Sopra le 8 ore viene richiesta la conferma dello straordinario, indicando la durata esatta.
- Le ore di `VIAGGIO` contano per le 8 ore ma non diventano mai straordinario: straordinario = ore non di viaggio oltre le 8.
- Il sabato è facoltativo: nessun minimo né completamento; tutte le ore non di viaggio sono straordinario.
- Il vincolo minimo di 8 ore è applicato anche dalla logica di persistenza, non soltanto dall'interfaccia.
- Dopo la conferma, ore, tipi di attività, numeri di scheda e stato CAD diventano di sola lettura.
- La riapertura delle attività richiede un comando separato e una conferma esplicita, per evitare modifiche accidentali.
- La spunta **Segnata su scheda cartacea** resta modificabile anche dopo la conferma, senza riaprire la giornata: la registrazione cartacea può infatti avvenire successivamente.
- Ogni modifica dello stato cartaceo viene salvata e comporta l'aggiornamento del relativo log.
- La conferma genera un file di testo nella cartella `Log`.

### Giorni precedenti non compilati

- Vengono controllati soltanto i giorni da lunedì a venerdì che non siano festività nazionali italiane.
- Sabato, domenica, festività nazionali e giorni futuri non vengono segnalati.
- Domeniche e festività sono bloccate in editing fino alla forzatura esplicita della singola giornata.
- Il calendario delle festività include le date fisse e il lunedì dell'Angelo calcolato annualmente.
- Il controllo parte dalla data di primo utilizzo dell'applicazione, modificabile nelle impostazioni.
- Un punto esclamativo segnala la presenza di giorni lavorativi precedenti non confermati.
- L'indicatore consente di aprire rapidamente i giorni mancanti.

### Stato della settimana

Lo stato si riferisce alla settimana attualmente visualizzata.

#### Settimana corrente

Viene mostrata una spunta verde **Settimana in pari** quando tutti i giorni lavorativi precedenti a oggi sono confermati. Il giorno corrente è escluso dal controllo e i giorni futuri non influiscono.

Esempio: mercoledì la spunta è verde se lunedì e martedì sono confermati, anche se mercoledì non è ancora compilato.

Se manca almeno un giorno lavorativo precedente viene mostrato un punto esclamativo.

#### Settimane precedenti

Viene mostrata una spunta verde **Settimana completa** soltanto quando tutti i giorni da lunedì a venerdì sono confermati. In caso contrario viene mostrato un indicatore di incompletezza.

#### Settimane future

Non vengono mostrati avvisi di incompletezza.

### Calendario mensile

- Finestra navigabile con celle da lunedì a domenica e apertura diretta della data selezionata.
- Verde chiaro per giorni confermati con almeno 8 ore, arancione per giorni obbligatori incompleti e numero rosso per le festività.
- Indicatori 🏝️ per ferie/permesso, farmacia per malattia, ⚠ per infortunio, `€` per straordinario, 🚗 per viaggio, 🧳 per trasferta e icone delle attività personalizzate.
- Riepilogo di giorni e ore per ferie/permesso, malattia, infortunio, straordinario, viaggio, giorni di trasferta e ore necessarie a completare i giorni mancanti.

### Note spese

- Voci per pasto, viaggio (biglietti, pedaggi, parcheggi), chilometri auto, alloggio e altro, con data, importo in centesimi, descrizione, stato **da rimborsare/rimborsata** e data di rimborso.
- Chilometri: importo = km × tariffa €/km delle impostazioni, copiata nella spesa al momento dell'inserimento.
- Allegato facoltativo (foto o PDF), copiato in `NoteSpese/Allegati/AAAA-MM/`; l'originale non viene modificato.
- File `NoteSpese/AAAA-MM.json` con scrittura atomica; riepilogo mensile con totali da rimborsare e rimborsati.

### Stampa, PDF e Word

- Selezione dei giorni da un calendario con spunta per ogni giorno (selezione mantenuta tra i mesi).
- Ogni giorno è un foglietto A6 (105 × 148,5 mm) con bordo tratteggiato rientrato di 3 mm, quattro per A4 verticale.
- Intestazione con nome del dipendente in alto a sinistra e data `GG/MM/AAAA` in alto a destra; elenco attività in 12 pt; quadratino con **V** per le attività registrate su carta, vuoto per quelle non registrate; totale e indicazione BOZZA per i giorni non confermati.
- Stessa disposizione per stampa (WPF), PDF (PDFsharp 6.2.4, licenza MIT) e Word `.docx` (Open XML SDK, licenza MIT).
- **Consegna (dalla 1.4.0):** dopo **Stampa…** i giorni confermati stampati risultano consegnati all'addetta (data e impronta del foglietto salvate nella giornata, campi `deliveredAt` e `deliveredFingerprint`). Il calendario di stampa distingue da stampare, consegnato, modificato dopo la consegna (da ristampare) e bozza; **Seleziona da stampare** spunta i giorni confermati del mese non ancora consegnati o modificati; un avviso segnala i giorni da stampare in altri mesi. Il contrassegno si mette o si toglie a mano dalla finestra di stampa o sotto lo stato di conferma del giorno. PDF e Word non segnano la consegna; le bozze non vengono mai segnate. Cambiare il nome del dipendente o la spunta della scheda cartacea non richiede una ristampa.

### Importazione da una versione precedente

- Da **Impostazioni → Importa dati da una versione precedente…** si seleziona la vecchia cartella portabile.
- Anteprima con giornate nuove, giornate in conflitto, log, schede e file non validi; backup ZIP dei dati attuali in `Backup/` prima di scrivere.
- Fusione non distruttiva: le giornate assenti vengono importate, in caso di stessa data resta quella attuale; i log vengono copiati solo per le giornate importate.
- Storico schede fuso; attività personalizzate e data di inizio controllo importate solo su richiesta esplicita.
- La cartella di origine viene solo letta; è rifiutata la cartella del programma in esecuzione (o una che la contiene).
- Domeniche e festività contrassegnate con lucchetto fino alla forzatura dedicata.

## Interfaccia e grafica

La finestra iniziale avrà dimensioni indicative di **400 × 560 pixel**, corrispondenti a un foglio A6 verticale con ridimensionamento standard di Windows.

Lo stile richiama un foglio di carta:

- sfondo bianco caldo o avorio;
- bordo e ombra leggeri;
- righe orizzontali discrete;
- intestazione con dipendente, data e stato della settimana;
- caratteri leggibili, senza simulare una grafia manoscritta;
- elenco delle attività scorrevole quando supera lo spazio disponibile;
- pulsante di conferma nella parte inferiore;
- logo originale con due pizze sovrapposte (cornicione, mozzarella, salame, basilico e fetta mancante), con disegno semplificato per le icone da 16 e 32 pixel, usato per EXE, finestre e area di notifica;
- crediti nel menu dell'area di notifica: `Energy Srl · Iacopo Preci · 2026`;
- finestre di dialogo ad altezza automatica, perché i testi non vengano tagliati.

## Architettura prevista

- **Linguaggio/UI:** C# e WPF.
- **Runtime:** .NET 10 LTS, pubblicazione `win-x64` self-contained.
- **Persistenza:** file JSON locali con scrittura atomica e backup contro file incompleti o danneggiati.
- **Esportazione:** file di testo `.txt` con codifica UTF-8; stampa, PDF e Word dei giorni selezionati.
- **Distribuzione:** cartella portabile compressa in formato ZIP, senza installer e senza necessità di installare .NET; aggiornamento integrato dalle release GitHub.

La pubblicazione self-contained aumenta la dimensione su disco rispetto a un eseguibile dipendente dal runtime, ma semplifica il trasferimento e l'avvio su un altro PC.

### Struttura del pacchetto portabile

```text
PizziniMaker/
├─ PizziniMaker.exe
├─ Impostazioni.json
├─ StoricoSchede.json
├─ Dati/
│  └─ giornate e bozze locali
├─ Log/
│  └─ log testuali giornalieri
├─ NoteSpese/
│  ├─ AAAA-MM.json
│  └─ Allegati/AAAA-MM/ copie delle ricevute
└─ Backup/
   └─ archivi ZIP creati prima di importazioni e aggiornamenti
```

Per trasferire applicazione, impostazioni e storico è sufficiente copiare l'intera cartella. Se viene spostata dopo aver attivato l'avvio automatico, il collegamento di avvio deve essere riattivato dall'applicazione.

## Formato del log

Esempio indicativo:

```text
Dipendente: Mario Rossi
Giorno: lunedì 16/11/2026

8092 - Baccane - 4,5 ore (CAD) - Carta: sì
8125 - 2 ore - Carta: no
VIAGGIO - 1,5 ore

Totale: 8 ore
Viaggio: 1,5 ore
```

Il formato definitivo dovrà mantenere facilmente riconoscibili numero di scheda, ore, presenza di `(CAD)` e stato della registrazione cartacea.

## Modello dati concettuale

### Impostazioni

- identificativo stabile del dipendente (GUID, dalla 1.3.0) e nome del dipendente;
- data di inizio controllo dei giorni mancanti;
- preferenza di avvio automatico;
- preferenza sempre in primo piano;
- controllo aggiornamenti all'avvio e data dell'ultimo controllo;
- tariffa €/km per le note spese;
- attività personalizzate;
- versione dello schema (4 dalla 1.3.0; la migrazione crea l'identificativo e rimuove eventuali attività personalizzate MANDATO/PREVENTIVO).

### Giornata

- data;
- stato bozza/confermato;
- trasferta (pernotto fuori sede);
- elenco delle righe;
- data e ora dell'ultima modifica;
- data e ora della conferma;
- revisione (contatore dei salvataggi, base per la futura sincronizzazione).

### Riga di attività

- tipo di attività;
- ore espresse in minuti, per evitare errori con i decimali;
- numero di scheda a quattro cifre, conservato come testo;
- commento della scheda;
- stato CAD;
- stato di registrazione sulla scheda cartacea.

## Validazione e test previsti

- Navigazione corretta tra date, mesi, anni e giorni della settimana.
- Validazione dei numeri di scheda, compresi zeri iniziali e input non numerici.
- Selezione delle ore in incrementi di 30 minuti e calcolo corretto dei totali.
- Visibilità dei controlli coerente con il tipo di attività.
- Salvataggio e recupero delle bozze dopo il riavvio.
- Persistenza del nome dipendente e delle altre impostazioni.
- Blocco dei dati confermati e riapertura soltanto esplicita.
- Modifica indipendente dello stato cartaceo dopo la conferma.
- Generazione e aggiornamento dei log UTF-8.
- Esclusione di weekend, giorni futuri e date precedenti all'inizio controllo dagli avvisi.
- Stato verde della settimana corrente con tutti i giorni precedenti a oggi confermati.
- Stato verde delle settimane passate solo con lunedì-venerdì completi.
- Trasferimento della cartella portabile e apertura su un secondo PC Windows 10/11.

## Linee guida per manutenzione ed evoluzioni

- Mantenere la logica di calcolo separata dai controlli WPF, così da poterla verificare con test automatici.
- Non introdurre dipendenze online per le funzioni essenziali.
- Conservare la compatibilità dei file dati; ogni modifica al formato deve prevedere una migrazione esplicita.
- Salvare i dati tramite file temporaneo e sostituzione atomica per ridurre il rischio di corruzione.
- Non versionare impostazioni personali, giornate reali, log o backup.
- Aggiornare questo documento quando cambiano comportamenti, formato dati o procedura di pubblicazione.
- Verificare ogni modifica su Windows 10 e Windows 11 prima di distribuire un nuovo pacchetto.

## Struttura della soluzione

```text
src/PizziniMaker.Core/       Modelli, validazione, calendario, persistenza e log
src/PizziniMaker.App/        Interfaccia WPF, ViewModel, tray e avvio automatico
tests/PizziniMaker.Tests/    Test xUnit del dominio e della persistenza
docs/GUIDA_UTENTE.md         Istruzioni incluse nel pacchetto portabile
scripts/publish-portable.ps1 Test e pubblicazione self-contained
scripts/create-icon.ps1       Generazione del logo PNG/ICO multiformato
```

## Compilazione, test e pubblicazione

È richiesto .NET 10 SDK per lo sviluppo, ma non per eseguire il pacchetto pubblicato.

```powershell
dotnet restore PizziniMaker.slnx
dotnet build PizziniMaker.slnx
dotnet test PizziniMaker.slnx
powershell -ExecutionPolicy Bypass -File scripts\publish-portable.ps1
```

Lo script esegue i test e crea:

- `artifacts\portable\PizziniMaker\PizziniMaker.exe`;
- `artifacts\PizziniMaker-<versione>-win-x64.zip`;
- `artifacts\PizziniMaker-<versione>-win-x64.zip.sha256` (formato `HASH  nomefile`), letto dall'aggiornamento automatico.

Con `-Release` crea anche la release GitHub `v<versione>` con i due file (`gh release create`); senza il parametro non pubblica nulla.

## Possibili evoluzioni future

- Client per più dipendenti e applicazione per la segreteria: vedere [docs/ROADMAP.md](docs/ROADMAP.md).
- Esportazione mensile CSV.
- Calendario aziendale per festività e chiusure.
- Backup automatico pianificato su una cartella scelta dall'utente.
- Scelta di valori massimi diversi per le ore delle singole righe.
- Firma digitale del pacchetto per una distribuzione aziendale centralizzata.

## Riservatezza e versionamento

Il repository contiene codice e documentazione, non dati reali dei dipendenti. Sono esclusi tramite `.gitignore`:

- `Impostazioni.json` e `StoricoSchede.json`;
- contenuto di `Dati/`, `Log/`, `NoteSpese/` e `Backup/`;
- pacchetti pubblicati, eseguibili compilati e file temporanei;
- directory e file locali degli strumenti di sviluppo.
