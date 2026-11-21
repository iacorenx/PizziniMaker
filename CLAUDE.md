# PizziniMaker — note per lo sviluppo

Registro ore portabile per Windows (WPF, .NET 10). Requisiti e comportamento: `README.md`; istruzioni utente: `docs/GUIDA_UTENTE.md`; ultima verifica: `docs/VERIFICA.md`.

## Struttura

- `src/PizziniMaker.Core` — logica senza UI, interamente testabile: modelli, regole orario (`DailyHours`, `WorkDayValidator`), persistenza (`PizziniStore`), storico schede (`WorkCardHistory`), importazione (`LegacyImportService`), log, calendario.
- `src/PizziniMaker.App` — WPF: `MainWindow`/`MainViewModel`, `ActivityEntryViewModel` (riga + suggerimenti), finestre di dialogo, tray in `App.xaml.cs`.
- `tests/PizziniMaker.Tests` — xUnit, riferisce solo Core. Le regole nuove vanno in Core e si testano lì.

## Comandi

```powershell
& "$env:USERPROFILE\.dotnet\dotnet.exe" test PizziniMaker.slnx     # test
& "$env:USERPROFILE\.dotnet\dotnet.exe" build PizziniMaker.slnx    # build
.\scripts\publish-portable.ps1                                    # test + ZIP self-contained in artifacts/
.\scripts\create-icon.ps1                                         # rigenera PNG/ICO; -OutputDirectory per un'anteprima
```

Per provare l'app senza toccare dati reali: compilare con `-o <cartella temporanea>` e lanciare l'exe da lì. I dati vengono letti e scritti accanto all'exe (`AppContext.BaseDirectory`). Un mutex consente una sola istanza: chiudere prima eventuali PizziniMaker in esecuzione.

## Convenzioni

- Interfaccia, messaggi, log, documentazione e messaggi di commit in **italiano**; commit in stile `feat:`/`fix:`/`docs:`.
- Ad ogni rilascio aggiornare `<Version>` in `PizziniMaker.App.csproj`, `README.md`, `docs/GUIDA_UTENTE.md` e `docs/VERIFICA.md`.
- **Mai committare dati personali**: `Impostazioni.json`, `Dati/`, `Log/`, `StoricoSchede.json`, `Backup/` sono in `.gitignore`.
- Compatibilità dei dati: i JSON delle versioni precedenti devono restare leggibili. Nuovi campi con valore predefinito; nuovi valori di `ActivityType` solo **in coda** (l'enum è serializzato come numero). Migrazioni delle impostazioni tramite `AppSettings.SchemaVersion` (oggi 3) in `PizziniStore.LoadSettings`.
- Scritture sempre tramite `WriteJsonAtomic`/`WriteTextAtomic` (file temporaneo + `.bak`).
- Finestre: niente altezze fisse con testi lunghi. Usare `SizeToContent="Height"`, `MinWidth`, `TextWrapping="Wrap"`, `WrapPanel` per i pulsanti; le finestre con elenchi devono essere ridimensionabili.

## Decisioni prese (1.2.0) e motivazioni

- **Storico schede**: una voce per numero (un numero = un lavoro), l'ultimo commento non vuoto vince. Si aggiorna solo alla **conferma del giorno** (niente errori di battitura dalle bozze), oltre che con inserimento manuale e importazione. Se manca o è danneggiato viene ricostruito dalle giornate confermate.
- **Importazione**: fusione non distruttiva. Con la stessa data resta la giornata attuale. Un log viene copiato **solo insieme alla sua giornata importata**, perché il log è sempre derivato dalla giornata e copiarlo da solo creerebbe incoerenze. Backup ZIP prima di scrivere; la cartella di origine viene solo letta.
- **Trasferta**: è una qualifica della giornata (`WorkDay.IsTrasferta`, pernotto fuori sede), non un'attività con ore. La vecchia attività personalizzata TRASFERTA viene rimossa dalla migrazione allo schema 3; le giornate storiche con quella riga restano invariate.
- **Sabato**: facoltativo, nessun minimo di 8 ore e nessuna proposta di completamento; tutte le ore non di viaggio sono straordinario. Vale solo per il sabato, non per domeniche o festività forzate.
- **Viaggio**: contabilità separata, mai straordinario. Nei feriali conta per le 8 ore e straordinario = ore non di viaggio oltre le 8 (es. 7 h schede + 3 h viaggio → 0 straordinario). Regola confermata dall'utente.

## Da verificare

- Resa delle finestre con scala dello schermo al 125% e 150% (provata solo al 100%).
- Menu della tray con la voce dei crediti, mai aperto durante i test.
