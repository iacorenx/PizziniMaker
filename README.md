# PizziniMaker

PizziniMaker è un'applicazione desktop portabile per Windows 10 e Windows 11 destinata alla registrazione giornaliera delle ore lavorative. Riprende l'organizzazione e l'aspetto di un piccolo foglio di carta A6 e consente di acquisire, conservare ed esportare le attività svolte.

> **Stato del progetto:** versione 1.1.0 implementata, con attività personalizzate, chiusura a 8 ore, calendario mensile, festività nazionali, logo dedicato e test automatici.

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
| Personalizzata | No | Sì | No | No |

Sono incluse inizialmente `TRASFERTA` ✈️ e `FORMAZIONE` 🎓. Dalle impostazioni è possibile aggiungere, rinominare o eliminare attività personalizzate e assegnare loro un'icona. Nome e icona vengono conservati anche nella singola riga storica, così le modifiche al catalogo non alterano i giorni precedenti.

Per le attività di tipo `SCHEDA`:

- il numero deve rispettare il formato numerico di quattro cifre, inclusi eventuali zeri iniziali;
- la casella **CAD** è selezionata per impostazione predefinita;
- la casella **Segnata su scheda cartacea** è deselezionata per impostazione predefinita;
- le due caselle sono indipendenti.

### Conferma, blocco e riapertura

- Il pulsante **Conferma giorno** valida e acquisisce le attività del giorno.
- Sotto le 8 ore viene proposto il completamento esatto con `QU` o `FERIE/PERMESSO`; annullando la bozza resta invariata.
- Sopra le 8 ore viene richiesta la conferma dello straordinario, indicando la durata esatta.
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
- Indicatori 🏝️ per ferie/permesso, farmacia per malattia, `€€` per straordinario e icone personalizzate come ✈️ per trasferta.
- Riepilogo di giorni e ore per ferie/permesso, malattia, straordinario e ore necessarie a completare i giorni mancanti.
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
- logo originale con due pizze pepperoni sovrapposte, usato per EXE, finestre e area di notifica.

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
├─ Dati/
│  └─ giornate e bozze locali
└─ Log/
   └─ log testuali giornalieri
```

Per trasferire applicazione, impostazioni e storico è sufficiente copiare l'intera cartella. Se viene spostata dopo aver attivato l'avvio automatico, il collegamento di avvio deve essere riattivato dall'applicazione.

## Formato del log

Esempio indicativo:

```text
Dipendente: Mario Rossi
Giorno: lunedì 16/11/2026

8000 - 4,5 ore (CAD) - Carta: sì
8125 - 2 ore - Carta: no
QU - 1,5 ore

Totale: 8 ore
```

Il formato definitivo dovrà mantenere facilmente riconoscibili numero di scheda, ore, presenza di `(CAD)` e stato della registrazione cartacea.

## Modello dati concettuale

### Impostazioni

- nome del dipendente;
- data di inizio controllo dei giorni mancanti;
- preferenza di avvio automatico;
- preferenza sempre in primo piano.

### Giornata

- data;
- stato bozza/confermato;
- elenco delle righe;
- data e ora dell'ultima modifica;
- data e ora della conferma.

### Riga di attività

- tipo di attività;
- ore espresse in minuti, per evitare errori con i decimali;
- numero di scheda a quattro cifre, conservato come testo;
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
