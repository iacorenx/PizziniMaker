# Rapporto di verifica - PizziniMaker 1.4.0

Data: 27/11/2026

## Novità 1.4.0: giorni consegnati

- **161 test xUnit superati** (153 della 1.3.0 + 8 nuovi in `DeliveryTests`), compilazione della soluzione senza errori né avvisi.
- Test automatici: giornata 1.3.0 senza i nuovi campi letta come non consegnata; `MarkDelivered` non cambia revisione né data di modifica e ignora bozze e giorni inesistenti; modifica dopo la consegna (numero scheda, trasferta, riapertura) → da ristampare; riconferma senza modifiche → di nuovo consegnato; spunta cartacea e nome del dipendente non richiedono ristampa; `ClearDelivered`; `GetDaysToPrint`.
- Interfaccia provata in una cartella temporanea con dati fittizi: calendario di stampa con stati *stampare*, *✓ dato*, *ristampa*, *bozza* e legenda; avviso arancio per i giorni da stampare in altri mesi; **Seleziona da stampare** spunta solo i confermati non consegnati; **Segna come consegnati** scrive `deliveredAt` e `deliveredFingerprint` e aggiorna calendario e riepilogo; nella finestra principale compare «✓ Consegnato il …» con **Togli consegnato**, e il contrassegno resta dopo la navigazione tra i giorni (nessuna sovrascrittura dal salvataggio automatico).
- Non provati: stampa reale (il contrassegno dopo **Stampa…** usa lo stesso `MarkDelivered` provato sopra) e la conferma di **Togli consegnato** (finestra di messaggio).

Le sezioni seguenti riportano la verifica completa della 1.3.0, ancora valida per le parti non modificate.

# Verifica 1.3.0 (23/11/2026)
## Ambiente

- Windows 10, versione sistema 10.0.19045, scala dello schermo al 100%.
- .NET SDK 10.0.404.
- Target applicazione: `net10.0-windows`, pubblicazione `win-x64` self-contained, file singolo.
- Versione file e prodotto: `1.3.0.0` / `1.3.0`.
- Nuove dipendenze: PDFsharp **6.2.4** (MIT; la 6.5 è AGPL e non va usata) e DocumentFormat.OpenXml 3.5.1 (MIT).

## Verifiche automatiche

- Compilazione Debug e Release completata senza errori né avvisi.
- **153 test xUnit superati** (88 della 1.2.0 + 65 nuovi), nessun test fallito o ignorato.
- MANDATO e PREVENTIVO: formati `123/X` e `1234/2026` validi e non validi (minuscole, lettere doppie, anno a 2 cifre, barre multiple), composizione e scomposizione del riferimento, messaggi d'errore per tipo, valori dell'enum in coda (7 e 8).
- Storico: voci separate per tipo e numero, ricerca filtrata per tipo, `StoricoSchede.json` della 1.2.0 letto come Scheda.
- Log: `MANDATO 123/X - Bianchi - 4 ore (CAD) - Carta: sì`.
- Impostazioni: migrazione 3 → 4 salvata subito (identificativo del dipendente stabile tra due letture), attività personalizzate MANDATO/PREVENTIVO rimosse; l'analisi di importazione non scrive nella cartella di origine.
- Calendario: icona ⚠ e totali per INFORTUNIO, straordinario con `€` singolo.
- Note spese: importi in formato italiano, km × tariffa, validazione, file mensili, spostamento tra mesi, allegato copiato ed eliminato con la spesa, stato rimborso con data, importazione con allegati, backup ZIP che include `NoteSpese/`.
- Nuova installazione: ricerca della cartella precedente sorella, importazione di nome, preferenze, tariffa ed `EmployeeId`.
- Stampa: contenuto del foglietto (nome in maiuscolo, data, righe, quadratino con V solo per i tipi con scheda cartacea, totale, BOZZA), 4 foglietti per pagina, misure A6 su A4, ritorno a capo del testo.
- Aggiornamenti: lettura dei tag (`v1.3.0`), confronto a tre cifre, analisi della risposta GitHub (pacchetto e `.sha256`, link non HTTPS scartati, pre-release ignorate), formati del file `.sha256`, installazione che sostituisce il programma ma non i dati, pulizia dei file `.old`, rifiuto di pacchetti senza eseguibile.

## Verifica dell'interfaccia

Applicazione avviata realmente in cartelle di prova con dati fittizi (dipendente "Mario Rossi").

- Finestra principale: nuovi pulsanti 🧾 (note spese, con numero di spese del giorno) e 🖨 (stampa) accanto a 📅 e ⚙.
- Elenco attività: ✎ MANDATO, ✉ PREVENTIVO e **⚠ INFORTUNIO visibili** insieme alle altre icone.
- MANDATO: caselle `123` / `X` (la `x` digitata diventa maiuscola), salvato come `123/X` (tipo 7). PREVENTIVO: anno `2026` precompilato, salvato come tipo 8.
- Note spese: inserimento di un pasto da 12,50 €, elenco mensile con stato "Da rimborsare" e riepilogo; finestra di modifica con categoria, importo, descrizione, allegato facoltativo e "Già rimborsata".
- Stampa: calendario con spunte (verde confermato, giallo bozza), "Seleziona confermati del mese", conteggio giorni e fogli A4, anteprima con foglietti tratteggiati.
- Impostazioni: titolo con versione, tariffa €/km, controllo aggiornamenti all'avvio, riquadro versione con **Verifica aggiornamenti…**.
- Aggiornamenti: contatto con GitHub riuscito; risposta "Non risultano versioni pubblicate" (repository ancora privato e senza release al momento della prova).
- Prima installazione in una cartella accanto a una precedente: proposta di importazione con percorso, nome e data di ultimo utilizzo; anteprima con 2 giornate, 1 log, 1 scheda, 1 nota spese; dopo l'importazione nome e identificativo del dipendente coincidono con la cartella precedente e si apre direttamente la finestra principale.

## Verifica di stampa, PDF e Word

- Pagina di stampa WPF e PDF generati con 5 giorni: 2 pagine A4, foglietti A6 2 × 2 con bordo tratteggiato, nome in alto a sinistra, data in alto a destra, righe lunghe a capo, quadratini con V e vuoti, totale con viaggio/straordinario/trasferta/BOZZA. PDF con testo selezionabile e carattere Segoe UI incorporato.
- Documento Word: **valido per il validatore Open XML (0 errori)**; non è stato aperto in Word perché sul PC di prova non sono installati né Word né LibreOffice.

## Verifica del pacchetto e dell'aggiornamento

- ZIP controllato: `PizziniMaker.exe`, `GUIDA_UTENTE.md` e cartelle `Dati` e `Log` vuote; nessun dato di prova incluso.
- Aggiornamento simulato con lo ZIP reale su una cartella 1.2.0 con dati: SHA-256 verificato, sostituiti solo `PizziniMaker.exe` e `GUIDA_UTENTE.md`, `Impostazioni.json`, giornate e log invariati, vecchio eseguibile conservato come `.old`.
- Dimensione eseguibile: circa 87 MB; ZIP: circa 81 MB.
- SHA-256: `7AFE3C56F3493C6401C1D200C4D541FAEC7576962F5E6929138CC0F54B70FDF9`.

## Controllo privacy per la pubblicazione del repository

- Nessun token, password, chiave o percorso assoluto nei file tracciati; nessun file di dati (`Dati/`, `Log/`, `Impostazioni.json`, `StoricoSchede.json`, `Backup/`, `NoteSpese/`) mai presente nello storico Git.
- Dati personali visibili: nome e azienda nei crediti (voluti) e **indirizzo e-mail dell'autore nei metadati dei commit**.

## Limiti della verifica

- Aggiornamento completo da una release GitHub reale (download, riavvio con `--dopo-aggiornamento`) non provato: richiede repository pubblico e una release.
- Menu dell'area di notifica non aperto con il mouse; le stesse funzioni sono state provate dalle finestre.
- Stampa su stampante fisica non provata (verificata l'anteprima e il documento generato).
- Scala dello schermo al 125% e 150% non provata. Pacchetto non firmato digitalmente.
