# Guida rapida a PizziniMaker

## Installazione e trasferimento

PizziniMaker non richiede installazione né runtime aggiuntivi.

1. Estrarre `PizziniMaker-win-x64.zip` in una cartella scrivibile, ad esempio `Documenti\PizziniMaker`.
2. Avviare `PizziniMaker.exe`.
3. Al primo avvio inserire il nome del dipendente e la data dalla quale controllare i giorni mancanti.
4. Per spostare il programma su un altro PC, chiuderlo dall'icona nell'area di notifica e copiare l'intera cartella `PizziniMaker`.

Nella cartella vengono creati:

- `Impostazioni.json`: nome e preferenze;
- `Dati`: bozze e giornate confermate;
- `Log`: riepiloghi testuali dei giorni confermati.

Copiare periodicamente l'intera cartella per avere una copia di sicurezza. I file `.bak` sono copie di sicurezza automatiche della versione precedente.

## Compilazione del giorno

- Usare **←** e **→** per cambiare giorno.
- Selezionare il tipo di attività e le ore; ogni intervallo è di 30 minuti.
- Per **Scheda**, inserire esattamente quattro cifre. Gli zeri iniziali sono conservati.
- **CAD** è attivo inizialmente e può essere deselezionato.
- **Su carta** indica che l'attività è stata riportata anche sulla scheda cartacea.
- Usare **+ Riga** per aggiungere attività e **×** per eliminarne una.
- Le bozze vengono salvate automaticamente.

## Conferma e modifiche

**Conferma giorno** valida i dati, blocca le attività e crea il file `Log\AAAA-MM-GG.txt`.

Per modificare ore, attività o numeri di scheda usare **Riapri giorno per modifiche**. La riapertura richiede conferma e rimuove il log precedente, che sarà ricreato alla nuova conferma.

La spunta **Su carta** resta invece modificabile anche dopo la conferma e aggiorna subito il log, senza riaprire il giorno.

## Settimane e giorni mancanti

- **✓ Settimana in pari**: nella settimana corrente tutti i giorni lavorativi precedenti a oggi sono confermati. Oggi e i giorni futuri non sono richiesti.
- **✓ Settimana completa**: in una settimana precedente tutti i giorni da lunedì a venerdì sono confermati.
- **! Settimana incompleta**: manca almeno un giorno richiesto.
- Il pulsante con il punto esclamativo apre l'elenco dei giorni precedenti da compilare.

Sabati, domeniche, giorni futuri e date precedenti all'inizio controllo non vengono segnalati.

## Area di notifica e avvio automatico

La chiusura della finestra con **X** la nasconde ma lascia PizziniMaker disponibile accanto all'orologio. Fare doppio clic sull'icona per riaprirla. Per chiudere completamente il programma usare **Esci** nel menu dell'icona.

Nelle impostazioni è possibile:

- cambiare il nome del dipendente;
- cambiare la data di inizio controllo;
- mantenere la finestra sempre in primo piano;
- attivare o disattivare l'avvio con Windows.

Se la cartella viene spostata, all'avvio successivo PizziniMaker aggiorna automaticamente il percorso di avvio con Windows quando questa opzione è attiva.

## Privacy

I dati restano esclusivamente sul PC e nella cartella scelta. L'applicazione non usa account, servizi cloud o connessioni di rete per le proprie funzioni.
