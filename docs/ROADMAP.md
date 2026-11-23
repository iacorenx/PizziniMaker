# Roadmap — più dipendenti e segreteria

Obiettivo: ogni dipendente usa il proprio PizziniMaker (client) e la segreteria aziendale riceve giornate confermate, log e note spese su un'applicazione dedicata ("PizziniMaker Segreteria"), senza perdere il funzionamento offline del client.

## Principi

- **Il client resta autonomo**: funziona senza rete; la sincronizzazione è un'aggiunta, mai un requisito per compilare o confermare il giorno.
- **Fonte di verità per i dati del dipendente: il suo client.** La segreteria legge, approva e annota (es. stato rimborso), ma non modifica le ore del dipendente.
- **Nessun segreto nel repository pubblico**: indirizzi, chiavi o percorsi di rete stanno in un file di configurazione locale ignorato da Git.
- **Compatibilità**: stessi JSON di oggi; nuovi campi solo con valore predefinito.

## Basi già presenti (1.3.0)

- `AppSettings.EmployeeId`: GUID stabile del dipendente, creato dalla migrazione allo schema 4 e conservato nell'importazione su nuova cartella.
- `WorkDay.Revision`: contatore incrementato a ogni salvataggio, per capire quale copia è più recente.
- Note spese in file mensili separati con identificativo per voce (`ExpenseEntry.Id`): fusione senza conflitti.
- `PizziniMaker.Core` senza dipendenze dall'interfaccia: riutilizzabile dall'applicazione della segreteria.

## Fase 1 — Cartella condivisa sulla rete locale (consigliata, nessun internet)

La soluzione più semplice e robusta in un ufficio con rete locale e un PC della segreteria sempre presente.

```text
\\PC-SEGRETERIA\Pizzini\                 (cartella condivisa, permessi per dipendente)
├─ <EmployeeId>\
│  ├─ dipendente.json                    nome, ultima sincronizzazione, versione client
│  ├─ Dati\AAAA-MM-GG.json               solo giornate confermate
│  ├─ Log\AAAA-MM-GG.txt
│  └─ NoteSpese\AAAA-MM.json + Allegati\
└─ Segreteria\
   └─ <EmployeeId>\Rimborsi.json         stato rimborsi deciso dalla segreteria (letto dal client)
```

- Client: impostazione "Cartella della segreteria"; alla conferma del giorno (e all'avvio, per recuperare quanto non inviato) copia i file con le stesse scritture atomiche di oggi. Se la cartella non è raggiungibile mette in coda e riprova.
- Sicurezza: condivisione Windows con utenti di dominio o locali; ogni dipendente scrive solo nella propria cartella (ACL NTFS), la segreteria legge tutto. Nessuna porta aperta, nessun servizio da installare.
- Vantaggi: zero costi, dati sempre in azienda (GDPR semplice), funziona anche senza internet.
- Limiti: serve la rete aziendale (o VPN) per sincronizzare; chi lavora a lungo fuori sede invia al rientro.

## Fase 2 — Applicazione "PizziniMaker Segreteria"

Nuovo progetto WPF nella stessa soluzione, che riusa `PizziniMaker.Core`.

- Elenco dipendenti con stato del mese (giorni confermati, mancanti, straordinario, viaggio, trasferte).
- Calendario per dipendente, stampa/PDF/Word con lo stesso `SlipRenderer` (estratto in una libreria comune).
- Note spese: verifica degli allegati, approvazione, segna rimborsata (scrive `Rimborsi.json`, che il client mostra in sola lettura).
- Esportazioni mensili (CSV/Excel) per paghe e contabilità.
- Avvisi: giorni non inviati, client non aggiornati.

## Fase 3 (facoltativa) — Sincronizzazione via cloud

Da valutare solo se i dipendenti devono inviare dati da fuori ufficio senza VPN.

### Opzione A: Supabase (piano gratuito)

Caratteristiche del piano gratuito note al momento della stesura (da riverificare sul sito prima di scegliere):

- database Postgres da 500 MB, storage file da 1 GB (sufficiente per anni di giornate; gli allegati delle note spese sono la voce che cresce di più);
- **progetto messo in pausa dopo circa una settimana senza attività**: va riattivato dal pannello, quindi non adatto a periodi di inattività (ferie estive) senza un piano a pagamento;
- regione europea selezionabile (es. Francoforte), necessario per i dati dei dipendenti;
- nessun accordo di servizio garantito sul piano gratuito.

Requisiti di sicurezza se scelto:

- autenticazione per dipendente (e-mail e password o link magico); la segreteria con un ruolo dedicato;
- **Row Level Security** su ogni tabella: un dipendente legge e scrive solo le proprie righe;
- nel client solo la chiave pubblica `anon` (pubblica per definizione, protetta dalle regole RLS); la chiave `service_role` **mai** in client o repository;
- valutazione GDPR: informativa ai dipendenti, accordo sul trattamento dei dati (DPA) con il fornitore.

### Opzione B: piccolo server sul PC della segreteria

ASP.NET Core (API minimale) con SQLite, in esecuzione come servizio Windows sul PC della segreteria, raggiungibile in rete locale (HTTPS con certificato interno) ed eventualmente da fuori tramite VPN aziendale. Nessun fornitore esterno, ma richiede un PC sempre acceso e un minimo di manutenzione.

## Raccomandazione

1. Fase 1 (cartella condivisa) + Fase 2 (applicazione segreteria): coprono il caso d'uso senza costi e senza internet.
2. Valutare la Fase 3 solo dopo l'uso reale, se l'invio da fuori ufficio diventa necessario; in quel caso preferire l'opzione B con VPN se l'azienda ne ha già una, altrimenti Supabase con un piano che non vada in pausa.

## Prossimi passi tecnici (in ordine)

1. Estrarre `SlipRenderer` in una libreria condivisa (`PizziniMaker.Printing`).
2. Client: impostazione cartella della segreteria, coda di invio, indicatore "inviato" nel calendario.
3. Test di sincronizzazione su cartella temporanea (copie, conflitti per `Revision`, ripresa dopo errore).
4. Progetto `PizziniMaker.Segreteria` con lettura delle cartelle e riepiloghi.
5. Gestione rimborsi dalla segreteria e visualizzazione nel client.
