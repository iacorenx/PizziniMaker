# Guida rapida a PizziniMaker

## Installazione e trasferimento

PizziniMaker non richiede installazione né runtime aggiuntivi.

1. Estrarre `PizziniMaker-win-x64.zip` in una cartella scrivibile, ad esempio `Documenti\PizziniMaker`.
2. Avviare `PizziniMaker.exe` con l'icona delle due pizze.
3. Al primo avvio inserire il nome del dipendente e la data dalla quale controllare i giorni mancanti.
4. Per spostare il programma, chiuderlo dal menu dell'icona nell'area di notifica e copiare l'intera cartella.

Nella cartella vengono creati:

- `Impostazioni.json`: nome, preferenze e attività personalizzate;
- `Dati`: bozze e giornate confermate;
- `Log`: riepiloghi testuali dei giorni confermati.

Copiare periodicamente l'intera cartella per avere una copia di sicurezza. I file `.bak` contengono la versione precedente dei dati.

## Attività e compilazione del giorno

- Usare **←** e **→** per cambiare giorno.
- Selezionare il tipo di attività e le ore; ogni intervallo è di 30 minuti.
- Per **Scheda**, inserire esattamente quattro cifre. Gli zeri iniziali sono conservati.
- **CAD** è attivo inizialmente e può essere deselezionato.
- **Su carta** indica che l'attività è stata riportata anche sulla scheda cartacea.
- Usare **+ Riga** per aggiungere attività e **×** per eliminarne una.
- Le bozze vengono salvate automaticamente, anche quando contengono meno di 8 ore.

Sono disponibili inizialmente **TRASFERTA ✈️** e **FORMAZIONE 🎓**. Da **Impostazioni → Gestisci attività personalizzate** è possibile aggiungere, modificare o eliminare altre attività e scegliere la relativa icona. Le modifiche al catalogo non alterano le attività già salvate nei giorni precedenti.

## Chiusura a 8 ore e straordinario

Il pulsante **Conferma giorno** valida i dati, blocca le attività e crea `Log\AAAA-MM-GG.txt`.

- Con **meno di 8 ore**, il programma indica la durata mancante e chiede se completare e chiudere con **QU** o **FERIE/PERMESSO**. La riga viene aggiunta con la durata esatta necessaria. **Annulla** lascia la bozza invariata.
- Con **esattamente 8 ore**, viene richiesta la conferma normale.
- Con **più di 8 ore**, viene indicata la quantità di straordinario. La chiusura avviene solo dopo conferma; **Annulla** lascia il giorno aperto.

Il log riporta totale ed eventuale straordinario. Per modificare ore, attività o numeri di scheda dopo la chiusura usare **Riapri giorno per modifiche**. La riapertura rimuove il log precedente, ricreato alla nuova conferma.

La spunta **Su carta** resta modificabile anche dopo la conferma e aggiorna subito il log senza riaprire il giorno.

## Calendario mensile

Il pulsante **📅** apre il calendario del mese visualizzato. È possibile cambiare mese e selezionare una data per aprirla nel foglio.

- **Verde chiaro:** giorno confermato con almeno 8 ore.
- **Arancione:** giorno lavorativo precedente incompleto.
- **Numero rosso:** festività nazionale.
- **Bordo blu:** giorno corrente.
- **🔒:** domenica o festività che richiede forzatura.

Le celle possono mostrare 🏝️ per ferie/permesso, il simbolo della farmacia per malattia, `€€` per straordinario, ✈️ per trasferta e le icone delle altre attività personalizzate.

Il riepilogo inferiore indica giorni e ore di ferie/permesso e malattia, giorni e ore di straordinario, giorni da completare e ore necessarie. Il giorno corrente e quelli futuri non vengono contati tra i giorni mancanti.

## Domeniche e festività nazionali

Domeniche e festività nazionali italiane, incluso il lunedì dell'Angelo, sono escluse dagli obblighi di compilazione e inizialmente non sono editabili.

Per registrarvi attività usare **Forza apertura solo per questo giorno** e confermare. La forzatura vale soltanto per la data selezionata e viene salvata nella giornata. Il sabato resta normalmente editabile ma non è un giorno obbligatorio.

## Settimane e giorni mancanti

- **✓ Settimana in pari**: tutti i giorni lavorativi precedenti a oggi sono confermati con almeno 8 ore.
- **✓ Settimana completa**: in una settimana precedente tutti i giorni richiesti da lunedì a venerdì sono confermati.
- **! Settimana incompleta**: manca almeno un giorno richiesto o è inferiore a 8 ore.
- Il pulsante con il punto esclamativo apre l'elenco dei giorni precedenti da compilare.

Sabati, domeniche, festività nazionali, giorni futuri e date precedenti all'inizio controllo non vengono segnalati.

## Area di notifica e avvio automatico

La chiusura della finestra con **X** la nasconde ma lascia PizziniMaker disponibile accanto all'orologio. Fare doppio clic sull'icona delle pizze per riaprirlo. Per chiudere completamente usare **Esci** nel menu dell'icona.

Nelle impostazioni è possibile cambiare dipendente, data di inizio controllo, attività personalizzate, modalità sempre in primo piano e avvio con Windows. Se la cartella viene spostata, all'avvio successivo il percorso di avvio automatico viene aggiornato quando questa opzione è attiva.

## Privacy

I dati restano esclusivamente sul PC e nella cartella scelta. L'applicazione non usa account, servizi cloud o connessioni di rete per le proprie funzioni.
