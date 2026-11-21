# PizziniMaker

PizziniMaker è un'applicazione desktop portabile per Windows 10 e Windows 11 destinata alla registrazione giornaliera delle ore lavorative. Riprende l'organizzazione e l'aspetto di un piccolo foglio di carta A6 e consente di acquisire, conservare ed esportare le attività svolte.

> **Stato del progetto:** versione 1.2.0 implementata, con commenti e storico delle schede con suggerimenti, ore di viaggio separate, sabato facoltativo, trasferta come qualifica della giornata, importazione da versioni precedenti, attività personalizzate, chiusura a 8 ore, calendario mensile, festività nazionali, logo dedicato e test automatici.

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
| `QU` | No | Sì | No | No |
| `FERIE/PERMESSO` | No | Sì | No | No |
| `MALATTIA` | No | Sì | No | No |
| `INFORTUNIO` | No | Sì | No | No |
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
- Indicatori 🏝️ per ferie/permesso, farmacia per malattia, `€€` per straordinario, 🚗 per viaggio, 🧳 per trasferta e icone delle attività personalizzate.
- Riepilogo di giorni e ore per ferie/permesso, malattia, straordinario, viaggio, giorni di trasferta e ore necessarie a completare i giorni mancanti.

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
- **Esportazione:** file di testo `.txt` con codifica UTF-8.
- **Distribuzione:** cartella portabile compressa in formato ZIP, senza installer e senza necessità di installare .NET.

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
└─ Backup/
   └─ archivi ZIP creati prima delle importazioni
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

- nome del dipendente;
- data di inizio controllo dei giorni mancanti;
- preferenza di avvio automatico;
- preferenza sempre in primo piano;
- attività personalizzate;
- versione dello schema (3 dalla 1.2.0).

### Giornata

- data;
- stato bozza/confermato;
- trasferta (pernotto fuori sede);
- elenco delle righe;
- data e ora dell'ultima modifica;
- data e ora della conferma.

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
- `artifacts\PizziniMaker-win-x64.zip`;
- hash SHA-256 dello ZIP, mostrato al termine.

## Possibili evoluzioni future

- Esportazione mensile CSV o PDF.
- Calendario aziendale per festività e chiusure.
- Backup automatico pianificato su una cartella scelta dall'utente.
- Scelta di valori massimi diversi per le ore delle singole righe.
- Firma digitale del pacchetto per una distribuzione aziendale centralizzata.

## Riservatezza e versionamento

Il repository contiene codice e documentazione, non dati reali dei dipendenti. Sono esclusi tramite `.gitignore`:

- `Impostazioni.json`;
- contenuto di `Dati/` e `Log/`;
- pacchetti pubblicati, eseguibili compilati e file temporanei;
- directory e file locali degli strumenti di sviluppo.
