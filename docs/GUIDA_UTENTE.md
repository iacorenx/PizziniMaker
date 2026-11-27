# Guida rapida a PizziniMaker

## Installazione e trasferimento

PizziniMaker (versione 1.4.0) non richiede installazione né runtime aggiuntivi.

1. Estrarre `PizziniMaker-1.4.0-win-x64.zip` in una cartella scrivibile, ad esempio `Documenti\PizziniMaker`.
2. Avviare `PizziniMaker.exe` con l'icona delle due pizze.
3. Al primo avvio il programma cerca una versione precedente (cartelle vicine, Desktop, Documenti, Download) e propone di importarne **giornate, log, note spese, storico, nome del dipendente e preferenze**. In alternativa si può scegliere la cartella a mano o partire da zero.
4. Se non si importa nulla, inserire il nome del dipendente e la data dalla quale controllare i giorni mancanti.
5. Per spostare il programma, chiuderlo dal menu dell'icona nell'area di notifica e copiare l'intera cartella.

Nella cartella vengono creati:

- `Impostazioni.json`: nome, preferenze e attività personalizzate;
- `Dati`: bozze e giornate confermate;
- `Log`: riepiloghi testuali dei giorni confermati;
- `StoricoSchede.json`: numeri di scheda, mandati, preventivi e commenti usati per i suggerimenti;
- `NoteSpese`: note spese mensili e copie delle ricevute allegate;
- `Backup`: archivi ZIP creati automaticamente prima di ogni importazione e di ogni aggiornamento.

Copiare periodicamente l'intera cartella per avere una copia di sicurezza. I file `.bak` contengono la versione precedente dei dati.

## Attività e compilazione del giorno

- Usare **←** e **→** per cambiare giorno.
- Selezionare il tipo di attività e le ore; ogni intervallo è di 30 minuti.
- Per **Scheda**, inserire esattamente quattro cifre. Gli zeri iniziali sono conservati.
- Sotto il numero è disponibile il **Commento** (ad esempio cliente o cantiere). Mentre si scrive compaiono le schede già usate: ad esempio `Bac` propone `8092 — Baccane`. Si sceglie con il mouse oppure con **↑**/**↓** e **Invio**; numero e commento vengono compilati insieme. La ricerca funziona anche sul numero e non distingue maiuscole e minuscole.
- Digitando un numero già presente nello storico con il commento vuoto, il commento viene proposto automaticamente.
- **MANDATO ✎** si scrive come numero/lettera, ad esempio `123/X`: numero nella prima casella, lettera nella seconda (diventa maiuscola).
- **PREVENTIVO ✉** si scrive come numero/anno, ad esempio `1234/2026`: l'anno è già compilato con quello della giornata e si può modificare.
- Mandati e preventivi funzionano come le schede: ore, **CAD**, **Su carta**, commento e suggerimenti dallo storico (cercati solo tra i riferimenti dello stesso tipo).
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

Le celle possono mostrare 🏝️ per ferie/permesso, il simbolo della farmacia per malattia, ⚠ per infortunio, `€` per straordinario, 🚗 per viaggio, 🧳 per trasferta e le icone delle attività personalizzate (✈️ per le vecchie righe TRASFERTA).

Il riepilogo inferiore indica giorni e ore di ferie/permesso, malattia e infortunio, giorni e ore di straordinario e di viaggio, giorni di trasferta, giorni da completare e ore necessarie. Il giorno corrente e quelli futuri non vengono contati tra i giorni mancanti.

## Note spese

Il pulsante **🧾** in alto (con il numero di spese del giorno, se presenti) apre le note spese del mese. Le spese sono indipendenti dalla conferma del giorno e restano sempre modificabili.

- **Aggiungi**: data, categoria (**Pasto**, **Viaggio** per biglietti/pedaggi/parcheggi, **Chilometri auto**, **Alloggio**, **Altro**), importo in euro (es. `12,50`) e descrizione.
- **Chilometri auto**: si indicano i km; l'importo è calcolato con la tariffa €/km di **Impostazioni → Note spese**. Ogni spesa conserva la tariffa con cui è stata inserita.
- **Allegato** (facoltativo): con **Sfoglia…** si sceglie la foto o il PDF dello scontrino; ne viene salvata una copia in `NoteSpese\Allegati`, l'originale non viene toccato. **Apri** lo mostra, **Rimuovi** lo toglie.
- **Segna rimborsata / da rimborsare** registra lo stato e la data del rimborso.
- In basso: totale del mese (o del solo giorno, con l'apposita casella), importo da rimborsare, già rimborsato e km.

## Stampa, PDF e Word

Il pulsante **🖨** in alto (o **Stampa giorni…** nel menu dell'icona) apre la finestra di stampa.

1. Nel calendario spuntare i giorni da stampare; si può cambiare mese mantenendo la selezione. Sono selezionabili i giorni con attività: verde = confermato da stampare, grigio = già consegnato, arancio = modificato dopo la consegna (da ristampare), giallo = bozza. **Seleziona da stampare** spunta i giorni confermati del mese non ancora consegnati o da ristampare; se ce ne sono in altri mesi, un avviso arancio in alto li elenca.
2. Il riepilogo indica giorni selezionati e fogli A4 necessari.
3. **Anteprima**, **Stampa…**, **Salva PDF…** o **Salva Word…**.

Ogni giorno è un foglietto **A6** con bordo tratteggiato, **quattro per foglio A4 verticale**: in alto a sinistra il nome del dipendente, in alto a destra la data `GG/MM/AAAA`, sotto l'elenco delle attività in caratteri grandi e in fondo il totale (con viaggio, straordinario, trasferta e la scritta BOZZA se il giorno non è confermato). Accanto a schede, mandati e preventivi c'è un quadratino: con la **V** se l'attività è registrata su carta, vuoto altrimenti. Il bordo tratteggiato è rientrato di 3 mm per restare nell'area stampabile delle stampanti.

### Giorni consegnati

Dopo **Stampa…** i giorni confermati stampati vengono segnati come **consegnati** (i foglietti si danno all'addetta che raccoglie gli orari). **Salva PDF…** e **Salva Word…** non li segnano, e le bozze non vengono mai segnate.

- Se la stampa è andata persa o va rifatta, selezionare i giorni e premere **Togli consegnato**: torneranno fra quelli da stampare.
- **Segna come consegnati** mette il contrassegno senza stampare (ad esempio per i giorni consegnati prima della versione 1.4.0).
- Anche nella finestra principale, sotto lo stato di conferma, compare lo stato di consegna del giorno con il comando **Segna consegnato** / **Togli consegnato**.
- Se un giorno consegnato viene riaperto e modificato, diventa **da ristampare** e torna fra quelli selezionati da **Seleziona da stampare**. Cambiare solo la spunta della scheda cartacea non richiede una ristampa.

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

La chiusura della finestra con **X** la nasconde ma lascia PizziniMaker disponibile accanto all'orologio. Fare doppio clic sull'icona delle pizze per riaprirlo. Per chiudere completamente usare **Esci** nel menu dell'icona (tasto destro), che riporta anche **la versione installata** e i crediti (`PizziniMaker 1.4.0 · Energy Srl · Iacopo Preci · 2026`).

Nelle impostazioni è possibile cambiare dipendente, data di inizio controllo, attività personalizzate, storico schede, tariffa €/km, modalità sempre in primo piano, avvio con Windows e controllo automatico degli aggiornamenti.

## Aggiornamenti

Con il tasto destro sull'icona → **Verifica aggiornamenti…** il programma controlla su GitHub se è stata pubblicata una versione più recente. Se c'è:

1. vengono mostrate la versione installata, quella nuova e le novità;
2. **Scarica e installa** scarica il pacchetto, ne verifica l'impronta SHA-256, crea un backup ZIP dei dati in `Backup\Pre-aggiornamento-*.zip`, sostituisce solo il programma e riavvia PizziniMaker;
3. giornate, log, note spese, impostazioni e storico restano nella stessa cartella e non vengono toccati.

Se l'opzione è attiva, all'avvio (al massimo una volta al giorno) un fumetto avvisa quando è disponibile una nuova versione; nulla viene scaricato senza conferma. Senza connessione a internet il programma funziona normalmente. La cartella del programma deve essere scrivibile (es. Documenti, non `C:\Programmi`). Se la cartella viene spostata, all'avvio successivo il percorso di avvio automatico viene aggiornato quando questa opzione è attiva.

## Storico schede

Da **Impostazioni → Gestisci storico schede** è possibile cercare per numero o commento, aggiungere manualmente una scheda con il suo commento, modificarla o eliminarla. Ogni numero ha un solo commento. Le modifiche valgono per i suggerimenti e non alterano le giornate già salvate.

## Importazione da una versione precedente

Il modo più semplice per aggiornare è **Verifica aggiornamenti…** (vedi sopra). Se invece si estrae la nuova versione in una cartella diversa, al primo avvio viene proposta l'importazione automatica; in qualsiasi momento si può usare **Impostazioni → Importa dati da una versione precedente…**, scegliendo la vecchia cartella `PizziniMaker` (o la sua sottocartella `Dati`).

1. Viene mostrata un'anteprima con giornate da importare, giornate già presenti, log, nuove schede, note spese ed eventuali file non validi.
2. Dopo la conferma viene creato un backup ZIP dei dati attuali nella cartella `Backup`.
3. Le giornate assenti vengono importate; se una data esiste già, **viene mantenuta la giornata attuale**. I log vengono copiati solo per le giornate importate.
4. Lo storico schede viene fuso e completato con le giornate importate.
5. Le note spese mancanti vengono aggiunte con i loro allegati; quelle già presenti non vengono sovrascritte.
6. Solo se si spunta l'opzione dedicata vengono aggiunte le attività personalizzate mancanti e anticipata la data di inizio controllo. In una cartella nuova (primo avvio) vengono riprese anche nome, preferenze, avvio con Windows e tariffa €/km.

La cartella di origine non viene modificata e non è possibile selezionare la cartella del programma in esecuzione. Al termine un rapporto indica elementi importati, mantenuti e ignorati.

## Privacy

I dati restano esclusivamente sul PC e nella cartella scelta. L'applicazione non usa account né servizi cloud. L'unica connessione di rete è il controllo degli aggiornamenti su GitHub, che non invia dati personali (solo la versione installata) e si può disattivare nelle impostazioni.
