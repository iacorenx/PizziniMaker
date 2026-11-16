# Rapporto di verifica - PizziniMaker 1.0.0

Data: 16/11/2026

## Ambiente

- Windows 10, versione sistema 10.0.19045.
- .NET SDK 10.0.404.
- Target applicazione: `net10.0-windows`, pubblicazione `win-x64` self-contained.

## Verifiche automatiche

- Compilazione Debug e Release completata senza errori.
- **31 test xUnit superati**, nessun test fallito o ignorato.
- Test su numeri di scheda di quattro cifre e zeri iniziali.
- Test su durate di 30 minuti, righe vuote e limite totale di 24 ore.
- Test su settimana corrente in pari senza richiedere il giorno odierno.
- Test su settimane passate complete, weekend, giorni futuri e data di inizio controllo.
- Test su persistenza, conferma, riapertura, recupero da backup e aggiornamento del log cartaceo.

## Verifica dell'interfaccia

- Applicazione avviata realmente con finestra di **400 × 560 pixel**.
- Verificata visualizzazione di dipendente, data italiana, badge verde, riga Scheda, CAD, Su carta e totale.
- Inserita tramite automazione UI la scheda `8000` e confermata la giornata.
- Verificato che CAD fosse disabilitato dopo la conferma mentre Su carta restasse abilitato.
- Modificata Su carta a giorno confermato: JSON e log aggiornati senza riapertura della giornata.

## Verifica del pacchetto

- Cartella portabile copiata in una seconda posizione.
- `PizziniMaker.exe` avviato direttamente dalla copia senza runtime .NET globale.
- ZIP aperto e contenuto controllato: eseguibile, guida utente e cartelle `Dati` e `Log` vuote.
- Nessuna impostazione o giornata di prova inclusa nello ZIP.
- Dimensione eseguibile: circa 77 MB.
- Dimensione ZIP: circa 72 MB.
- SHA-256 finale: `5B77315E8477C5D7B72E262AE9E22BACE7CF72088CF652C436B1A3C8E4F06D7B`.

## Limiti della verifica

Il test reale è stato eseguito su Windows 10. Windows 11 è incluso nei target compatibili, ma non era disponibile un secondo sistema per una prova diretta. Il pacchetto non è firmato digitalmente.
