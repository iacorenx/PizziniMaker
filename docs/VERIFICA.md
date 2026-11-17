# Rapporto di verifica - PizziniMaker 1.1.0

Data: 16/11/2026

## Ambiente

- Windows 10, versione sistema 10.0.19045.
- .NET SDK 10.0.404.
- Target applicazione: `net10.0-windows`, pubblicazione `win-x64` self-contained.
- Versione file e prodotto: `1.1.0.0` / `1.1.0`.

## Verifiche automatiche

- Compilazione Debug e Release completata senza errori.
- **57 test xUnit superati**, nessun test fallito o ignorato.
- Numeri di scheda, zeri iniziali, mezz'ore e limiti giornalieri.
- Completamento a 8 ore, rifiuto delle conferme insufficienti e calcolo dello straordinario.
- Attività personalizzate, nomi nei log e catalogo iniziale TRASFERTA/FORMAZIONE.
- Festività fisse e Pasqua/lunedì dell'Angelo per più anni.
- Esclusione di weekend, festività, giorni futuri e date precedenti all'inizio controllo.
- Blocco delle domeniche senza forzatura e migrazione dei soli JSON legacy.
- Stati, icone, giorni e ore dei riepiloghi mensili.
- Persistenza, conferma, riapertura, recupero da backup e aggiornamento del log cartaceo.

## Verifica dell'interfaccia

- Applicazione avviata realmente con finestra A6 di **400 × 560 pixel**.
- Completamento tramite UI da 7,5 a 8 ore con riga `QU - 0,5 ore` e giorno confermato.
- Conferma di 8,5 ore con avviso e log `Straordinario: 0,5 ore`.
- Selezione di TRASFERTA e log con nome personalizzato e 8 ore.
- Aggiunta tramite impostazioni di `CORSO SICUREZZA 💼`, salvata correttamente in JSON.
- Calendario verificato con giorni verdi/arancioni, domeniche bloccate e simboli `€€`, ✈️, 🏝️ e farmacia.
- Domenica inizialmente non editabile; dopo la forzatura controlli attivi e campo persistente.
- Opzione **Avvia PizziniMaker con Windows** interamente visibile nella schermata impostazioni.
- Riepiloghi mensili verificati: ferie, malattia, straordinario, giorni mancanti e ore necessarie.

## Verifica del logo e del pacchetto

- Logo originale con due pizze pepperoni sovrapposte, in PNG e ICO multiformato.
- Verifica dei pixel non trasparenti nelle icone da 16, 32, 48 e 256 pixel.
- Icona EXE da 32 pixel verificata: 529 pixel visibili su 1024.
- Cartella portabile copiata in una seconda posizione e avviata senza runtime .NET globale.
- ZIP controllato: eseguibile, guida utente e cartelle `Dati` e `Log` vuote.
- Nessuna impostazione o giornata di prova inclusa nello ZIP.
- Dimensione eseguibile: circa 77 MB.
- Dimensione ZIP: circa 72 MB.
- SHA-256 finale: `735141A435247A8000EA7CA6B2E41BD2561B101929EE2C13DE65E78240FD58D6`.

## Limiti della verifica

Il test reale è stato eseguito su Windows 10. Windows 11 è incluso nei target compatibili, ma non era disponibile un secondo sistema per una prova diretta. Il pacchetto non è firmato digitalmente.
