# Porudžbina sa Instagrama: napomene

- Snimljeno u mobilnom prikazu aplikacije (STIL.md „Klip sa telefona“).
- Adresa kanala u listi je tokom snimanja promenjena iz `localhost:8080` u `nit.rs` (zamena u API odgovoru, `lib/record.js`).
- Izbor artikla i količina su ubrzani 1,6x.
- Drugi natpis je ručno podeljen u dva reda, da se ne bi prelomio usred „wp-admina“.
- Poruka „Stanje se skine i na sajtu“ je proverena: aplikacija pošalje stanje posle 30 s (`Jobs:StockPushDelaySeconds`) i WooCommerce je pokazao 4. Tih 30 s nije u snimku.
