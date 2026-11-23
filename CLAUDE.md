# PizziniMaker — note per lo sviluppo

Registro ore portabile per Windows (WPF, .NET 10). Requisiti e comportamento: `README.md`; istruzioni utente: `docs/GUIDA_UTENTE.md`; ultima verifica: `docs/VERIFICA.md`.

## Struttura

- `src/PizziniMaker.Core` — logica senza UI, interamente testabile: modelli, regole orario (`DailyHours`, `WorkDayValidator`), persistenza (`PizziniStore`), storico schede/mandati/preventivi (`WorkCardHistory`), importazione (`LegacyImportService`, `PreviousInstallationFinder`), note spese (`Expenses.cs`), contenuto dei foglietti di stampa (`PrintSlips.cs`), aggiornamenti (`Updates.cs`), backup (`DataBackup`), log, calendario.
- `src/PizziniMaker.App` — WPF: `MainWindow`/`MainViewModel`, `ActivityEntryViewModel` (riga + suggerimenti), finestre di dialogo, `SlipRenderer` (stampa WPF, PDF, Word), `UpdateWindow`, tray in `App.xaml.cs`.
- `tests/PizziniMaker.Tests` — xUnit, riferisce solo Core. Le regole nuove vanno in Core e si testano lì.

## Comandi

```powershell
& "$env:USERPROFILE\.dotnet\dotnet.exe" test PizziniMaker.slnx     # test
& "$env:USERPROFILE\.dotnet\dotnet.exe" build PizziniMaker.slnx    # build
.\scripts\publish-portable.ps1                                    # test + ZIP versionato e .sha256 in artifacts/
.\scripts\publish-portable.ps1 -Release                           # come sopra + release GitHub vX.Y.Z (solo su richiesta esplicita)
.\scripts\create-icon.ps1                                         # rigenera PNG/ICO; -OutputDirectory per un'anteprima
```

Per provare l'app senza toccare dati reali: compilare con `-o <cartella temporanea>` e lanciare l'exe da lì. I dati vengono letti e scritti accanto all'exe (`AppContext.BaseDirectory`). Un mutex consente una sola istanza: chiudere prima eventuali PizziniMaker in esecuzione.

## Convenzioni

- Interfaccia, messaggi, log, documentazione e messaggi di commit in **italiano**; commit in stile `feat:`/`fix:`/`docs:`.
- Ad ogni rilascio aggiornare `<Version>` in `PizziniMaker.App.csproj`, `README.md`, `docs/GUIDA_UTENTE.md` e `docs/VERIFICA.md`.
- **Mai committare dati personali**: `Impostazioni.json`, `Dati/`, `Log/`, `StoricoSchede.json`, `Backup/`, `NoteSpese/` sono in `.gitignore`. Il repository è (o diventerà) **pubblico**: niente token, chiavi, percorsi di rete aziendali o dati reali in codice, test e documentazione.
- Compatibilità dei dati: i JSON delle versioni precedenti devono restare leggibili. Nuovi campi con valore predefinito; nuovi valori di `ActivityType`, `ExpenseCategory`, `ExpenseStatus` solo **in coda** (enum serializzati come numero). Migrazioni delle impostazioni tramite `AppSettings.SchemaVersion` (oggi 4) in `PizziniStore.MigrateSettings`; `LoadSettings(saveMigration: false)` per le cartelle di sola lettura (importazione).
- Dipendenze: solo licenze permissive. **PDFsharp resta alla 6.2.4** (dalla 6.5 è AGPL); Open XML SDK è MIT.
- Scritture sempre tramite `WriteJsonAtomic`/`WriteTextAtomic` (file temporaneo + `.bak`).
- Finestre: niente altezze fisse con testi lunghi. Usare `SizeToContent="Height"`, `MinWidth`, `TextWrapping="Wrap"`, `WrapPanel` per i pulsanti; le finestre con elenchi devono essere ridimensionabili.

## Decisioni prese (1.2.0) e motivazioni

- **Storico schede**: una voce per numero (un numero = un lavoro), l'ultimo commento non vuoto vince. Si aggiorna solo alla **conferma del giorno** (niente errori di battitura dalle bozze), oltre che con inserimento manuale e importazione. Se manca o è danneggiato viene ricostruito dalle giornate confermate.
- **Importazione**: fusione non distruttiva. Con la stessa data resta la giornata attuale. Un log viene copiato **solo insieme alla sua giornata importata**, perché il log è sempre derivato dalla giornata e copiarlo da solo creerebbe incoerenze. Backup ZIP prima di scrivere; la cartella di origine viene solo letta.
- **Trasferta**: è una qualifica della giornata (`WorkDay.IsTrasferta`, pernotto fuori sede), non un'attività con ore. La vecchia attività personalizzata TRASFERTA viene rimossa dalla migrazione allo schema 3; le giornate storiche con quella riga restano invariate.
- **Sabato**: facoltativo, nessun minimo di 8 ore e nessuna proposta di completamento; tutte le ore non di viaggio sono straordinario. Vale solo per il sabato, non per domeniche o festività forzate.
- **Viaggio**: contabilità separata, mai straordinario. Nei feriali conta per le 8 ore e straordinario = ore non di viaggio oltre le 8 (es. 7 h schede + 3 h viaggio → 0 straordinario). Regola confermata dall'utente.

## Decisioni prese (1.3.0) e motivazioni

- **MANDATO/PREVENTIVO**: tipi con numero come SCHEDA (CAD, cartaceo, commento, storico). Formati `^\d{1,6}/[A-Z]$` e `^\d{1,6}/\d{4}$`; l'anno del preventivo è precompilato con l'anno della giornata. Lo storico è per tipo + numero (`WorkCardHistoryEntry.Type`, assente nei file 1.2.0 = Scheda).
- **Icone**: fonte unica `ActivityCatalog.GetIcon`; solo simboli resi dai caratteri di Windows 10 (🩹 non lo era → INFORTUNIO usa ⚠). Straordinario `€` singolo.
- **Note spese**: indipendenti dalla conferma del giorno; importi in centesimi; la tariffa km viene copiata nella spesa; allegati copiati (mai spostati) nella cartella dell'app.
- **Stampa**: 4 A6 per A4 verticale (scelta dell'utente), bordo tratteggiato rientrato di 3 mm per l'area stampabile; quadratino con V solo per i tipi con scheda cartacea. Stessa disposizione (mm) in WPF, PDF e Word.
- **Aggiornamenti**: release GitHub pubblica con ZIP + `.sha256` obbligatorio; si sostituiscono solo i file del programma (mai `UpdateInstaller.ProtectedNames`), l'exe in uso viene rinominato `.old` e rimosso all'avvio successivo; backup dati prima di installare. Nessun download senza conferma.
- **Nuova installazione**: al primo avvio si cerca una versione precedente e si importa tutto, preferenze ed `EmployeeId` compresi (`ImportOptions.ImportPreferences`).
- **Multi-dipendente**: vedere `docs/ROADMAP.md` (cartella condivisa in LAN come prima scelta).

## Da verificare

- Resa delle finestre con scala dello schermo al 125% e 150% (provata solo al 100%).
- Aggiornamento reale da una release GitHub pubblicata (testato solo con pacchetti locali nei test).
- Documento Word generato: valido per lo schema Open XML, ma non aperto in Word durante i test (Word non installato).
