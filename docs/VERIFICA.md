# Rapporto di verifica - PizziniMaker 1.2.0

Data: 21/11/2026

## Ambiente

- Windows 10, versione sistema 10.0.19045.
- .NET SDK 10.0.404.
- Target applicazione: `net10.0-windows`, pubblicazione `win-x64` self-contained.
- Versione file e prodotto: `1.2.0.0` / `1.2.0`.

## Verifiche automatiche

- Compilazione Debug e Release completata senza errori né avvisi.
- **88 test xUnit superati**, nessun test fallito o ignorato.
- Storico schede: `Bac` → `8092 Baccane`, ricerca per numero senza distinzione tra maiuscole e minuscole, ordinamento per pertinenza e poi per uso recente, un commento per numero, fusione e ricostruzione dalle giornate confermate.
- Commenti: persistenza nella giornata, scrittura nel log (`8092 - Baccane - 8 ore (CAD) - Carta: no`), compatibilità con i JSON 1.1.0 privi del campo, storico aggiornato alla conferma.
- Viaggio: conta per le 8 ore, non diventa straordinario, riga `Viaggio:` nel log e totali mensili.
- Sabato: confermabile sotto le 8 ore, ore non di viaggio interamente straordinario, giorno completo in calendario.
- Trasferta: riga `Trasferta: sì` nel log, giorni contati nel riepilogo mensile, migrazione delle impostazioni v2 → v3 che rimuove la voce TRASFERTA e giornate storiche con riga TRASFERTA invariate.
- Importazione: giornate assenti importate, conflitti con giornata attuale mantenuta, log solo per giornate importate, recupero da `.bak`, JSON corrotti e nomi non validi segnalati, backup ZIP, cartella di origine non modificata, impostazioni importate solo su richiesta, rifiuto della cartella in esecuzione e di cartelle annidate, selezione della sottocartella `Dati` accettata.
- Restano superati i test della 1.1.0: numeri di scheda, completamento a 8 ore, festività, giorni mancanti, calendario, persistenza e recupero da backup.

## Verifica dell'interfaccia

Applicazione avviata realmente (sabato 21/11/2026) in una cartella di prova con dati 1.1.0 simulati.

- Finestra principale da **410 × 620 pixel**: casella **🧳 Trasferta**, avviso del sabato facoltativo, riga Scheda con campo **Commento** e totali con straordinario/viaggio su righe separate.
- Digitando `bac` compaiono `8100 — Baccane cantiere nord` e `8092 — Baccane` (più recente prima); con **↓** e **Invio** la riga diventa `8092` / `Baccane` ed è salvata nel JSON.
- Conferma del sabato con 0,5 ore: finestra **Sabato facoltativo** senza proposta di completamento; log con `Straordinario: 0,5 ore`.
- Impostazioni: voce TRASFERTA rimossa dal catalogo; riquadri **Storico schede** e **Versione precedente** visibili.
- Gestione storico: elenco ordinato per ultimo uso, con commento e numero di utilizzi.
- Importazione da cartella 1.1.0: anteprima con 2 giornate nuove, 1 conflitto mantenuto, 1 log, 1 scheda nuova e 1 file corrotto ignorato; rapporto finale e backup `Backup\Pre-import-*.zip` creati.
- Calendario: icone 🚗 e 🧳, vecchia riga TRASFERTA ✈️ invariata, sabato confermato verde, legenda e riepilogo su più righe senza testi tagliati.
- Dialoghi di conferma, completamento e modifica ad altezza automatica (`SizeToContent`), pulsanti con larghezza minima.

## Verifica del logo e del pacchetto

- Nuovo logo con due pizze sovrapposte: cornicione dorato con bruciature, salsa, mozzarella, salame con dettagli, basilico e fetta mancante.
- Icone da 16 e 32 pixel disegnate in versione semplificata (una pizza con fetta mancante), riconoscibili nell'area di notifica.
- ZIP controllato: eseguibile, guida utente e cartelle `Dati` e `Log` vuote; nessun dato di prova incluso.
- Dimensione eseguibile: circa 78 MB.
- Dimensione ZIP: circa 72 MB.
- SHA-256 finale: `E99D3754D1E7114A7D99E1FA2680603A7FF8001026D0022993850C3EED2FE9F5`.

## Limiti della verifica

Il test reale è stato eseguito su Windows 10 al 100% di scala. Le finestre di dialogo si adattano al contenuto, ma la resa con scala del testo al 125% o 150% non è stata provata direttamente. Il menu dell'area di notifica con i crediti è stato verificato nel codice ma non aperto con il mouse durante il test. Il pacchetto non è firmato digitalmente.
