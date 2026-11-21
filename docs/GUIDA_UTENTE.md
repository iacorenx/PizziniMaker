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
- `Log`: riepiloghi testuali dei giorni confermati;
- `StoricoSchede.json`: numeri di scheda e commenti usati per i suggerimenti;
- `Backup`: archivi ZIP creati automaticamente prima di ogni importazione.

Copiare periodicamente l'intera cartella per avere una copia di sicurezza. I file `.bak` contengono la versione precedente dei dati.

## Attività e compilazione del giorno

- Usare **←** e **→** per cambiare giorno.
- Selezionare il tipo di attività e le ore; ogni intervallo è di 30 minuti.
- Per **Scheda**, inserire esattamente quattro cifre. Gli zeri iniziali sono conservati.
- Sotto il numero è disponibile il **Commento** (ad esempio cliente o cantiere). Mentre si scrive compaiono le schede già usate: ad esempio `Bac` propone `8092 — Baccane`. Si sceglie con il mouse oppure con **↑**/**↓** e **Invio**; numero e commento vengono compilati insieme. La ricerca funziona anche sul numero e non distingue maiuscole e minuscole.
- Digitando un numero già presente nello storico con il commento vuoto, il commento viene proposto automaticamente.
- **VIAGGIO 🚗** registra le ore di viaggio: contano per le 8 ore ma hanno una contabilità separata e non diventano mai straordinario.
- **CAD** è attivo inizialmente e può essere deselezionato.
- **Su carta** indica che l'attività è stata riportata anche sulla scheda cartacea.
- Usare **+ Riga** per aggiungere attività e **×** per eliminarne una.
- Le bozze vengono salvate automaticamente, anche quando contengono meno di 8 ore.

La casella **🧳 Trasferta (pernotto fuori)** sotto la data qualifica l'intera giornata quando si dorme fuori sede: non aggiunge ore (le 8 ore si registrano come di consueto con le schede) e compare nel log, nel calendario e nel riepilogo mensile.

È disponibile inizialmente **FORMAZIONE 🎓**. Dalla versione 1.2.0 la trasferta non è più un'attività con ore: la voce TRASFERTA viene tolta dal catalogo, mentre le giornate già salvate con una riga TRASFERTA restano invariate. Da **Impostazioni → Gestisci attività personalizzate** è possibile aggiungere, modificare o eliminare altre attività e scegliere la relativa icona. Le modifiche al catalogo non alterano le attività già salvate nei giorni precedenti.

## Chiusura a 8 ore e straordinario

Il pulsante **Conferma giorno** valida i dati, blocca le attività e crea `Log\AAAA-MM-GG.txt`.

- Con **meno di 8 ore**, il programma indica la durata mancante e chiede se completare e chiudere con **QU** o **FERIE/PERMESSO**. La riga viene aggiunta con la durata esatta necessaria. **Annulla** lascia la bozza invariata.
- Con **esattamente 8 ore**, viene richiesta la conferma normale.
- Con **più di 8 ore**, viene indicata la quantità di straordinario. La chiusura avviene solo dopo conferma; **Annulla** lascia il giorno aperto. Le ore di viaggio oltre le 8 ore non sono straordinario.
- Il **sabato** è facoltativo: non c'è un minimo di ore e non viene proposto il completamento a 8 ore. Tutte le ore lavorate valgono come straordinario, tranne quelle di **VIAGGIO**, conteggiate a parte.

Il log riporta totale, eventuale straordinario, ore di viaggio, trasferta e il commento di ogni scheda (ad esempio `8092 - Baccane - 4 ore (CAD) - Carta: no`). Le schede delle giornate confermate entrano nello storico dei suggerimenti. Per modificare ore, attività o numeri di scheda dopo la chiusura usare **Riapri giorno per modifiche**. La riapertura rimuove il log precedente, ricreato alla nuova conferma.

La spunta **Su carta** resta modificabile anche dopo la conferma e aggiorna subito il log senza riaprire il giorno.

## Calendario mensile

Il pulsante **📅** apre il calendario del mese visualizzato. È possibile cambiare mese e selezionare una data per aprirla nel foglio.

- **Verde chiaro:** giorno confermato con almeno 8 ore (il sabato basta la conferma).
- **Arancione:** giorno lavorativo precedente incompleto.
- **Numero rosso:** festività nazionale.
- **Bordo blu:** giorno corrente.
- **🔒:** domenica o festività che richiede forzatura.

Le celle possono mostrare 🏝️ per ferie/permesso, il simbolo della farmacia per malattia, `€€` per straordinario, 🚗 per viaggio, 🧳 per trasferta e le icone delle attività personalizzate (✈️ per le vecchie righe TRASFERTA).

Il riepilogo inferiore indica giorni e ore di ferie/permesso e malattia, giorni e ore di straordinario e di viaggio, giorni di trasferta, giorni da completare e ore necessarie. Il giorno corrente e quelli futuri non vengono contati tra i giorni mancanti.

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

La chiusura della finestra con **X** la nasconde ma lascia PizziniMaker disponibile accanto all'orologio. Fare doppio clic sull'icona delle pizze per riaprirlo. Per chiudere completamente usare **Esci** nel menu dell'icona, che riporta anche i crediti (Energy Srl · Iacopo Preci · 2026).

Nelle impostazioni è possibile cambiare dipendente, data di inizio controllo, attività personalizzate, storico schede, modalità sempre in primo piano e avvio con Windows. Se la cartella viene spostata, all'avvio successivo il percorso di avvio automatico viene aggiornato quando questa opzione è attiva.

## Storico schede

Da **Impostazioni → Gestisci storico schede** è possibile cercare per numero o commento, aggiungere manualmente una scheda con il suo commento, modificarla o eliminarla. Ogni numero ha un solo commento. Le modifiche valgono per i suggerimenti e non alterano le giornate già salvate.

## Importazione da una versione precedente

Per passare a una nuova versione: estrarre la nuova versione in una cartella diversa, avviarla e usare **Impostazioni → Importa dati da una versione precedente…**, scegliendo la vecchia cartella `PizziniMaker` (o la sua sottocartella `Dati`).

1. Viene mostrata un'anteprima con giornate da importare, giornate già presenti, log, nuove schede ed eventuali file non validi.
2. Dopo la conferma viene creato un backup ZIP dei dati attuali nella cartella `Backup`.
3. Le giornate assenti vengono importate; se una data esiste già, **viene mantenuta la giornata attuale**. I log vengono copiati solo per le giornate importate.
4. Lo storico schede viene fuso e completato con le giornate importate.
5. Solo se si spunta l'opzione dedicata vengono aggiunte le attività personalizzate mancanti e anticipata la data di inizio controllo.

La cartella di origine non viene modificata e non è possibile selezionare la cartella del programma in esecuzione. Al termine un rapporto indica elementi importati, mantenuti e ignorati.

## Privacy

I dati restano esclusivamente sul PC e nella cartella scelta. L'applicazione non usa account, servizi cloud o connessioni di rete per le proprie funzioni.
