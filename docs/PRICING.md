# Model naplate (PREDLOG, nije usvojeno)

Status: predlog, verzija 2 (posle revizije). Naplata pretplate je u SPEC.md van v1 (2.8), pa ništa iz ovog dokumenta ne ulazi pre prvog pravog korisnika (SPEC 10). Cene i limiti su polazna tačka i proveravaju se sa prvim klijentima.

## 1. Redosled uvođenja

| Korak | Kada | Šta |
|---|---|---|
| 0 | sada, prvi korisnici | Bez koda za naplatu. Besplatna beta uz pisani dogovor o budućoj ceni i osnivački popust za prvih 10 klijenata. |
| 1 | prvi plaćeni klijenti | `PaidUntil` na firmi, `Grace` i `Expired` izračunati iz datuma, režim „samo čitanje”, CSV izvoz, meki limiti. Uplate unosi operater komandom ili SQL-om, bez administratorskog ekrana. |
| 2 | prvi inostrani klijent ili kad ručno vođenje uplata počne da oduzima vreme | Paddle (kartice, automatsko produžavanje). |

## 2. Kanali plaćanja

1. **Faktura + uplata na račun** (podrazumevano za domaće firme). Faktura se izdaje van aplikacije (SEF portal ili program za fakturisanje), a uplata se ručno unosi. Godišnja pretplata je podrazumevana ponuda za ovaj kanal: 12 puta manje fakturisanja.
2. **Kartice preko Paddle-a** (merchant of record, Paddle obračunava i uplaćuje porez). Uvodi se tek u koraku 2.
3. Alternativa za kasnije: domaći e-commerce procesor preko banke.

Pre koraka 2 proveriti (Paddle i knjigovođa):
- da li Paddle prima prodavca iz Srbije pod očekivanim uslovima i kolika je provizija;
- da li domaćim firmama odgovara faktura od Paddle-a umesto domaće fakture;
- kako paušalac prima isplate u devizama.

Proveriti sa knjigovođom i da li paušalac van PDV-a mora da izdaje B2B fakture preko SEF-a. To je SEF za *moje* fakture, nezavisno od SEF-a kao funkcije za korisnike, koji ostaje van v1.

## 3. Planovi (cene u RSD)

Metrika je **broj porudžbina mesečno**, ne broj artikala:
- svaka varijacija je poseban artikal (SPEC 2.1), pa i mala prodavnica odeće lako ima 1.000 artikala, a limit bi pucao usred uvoza iz prodavnice;
- broj kanala nije metrika, jer firma ima najviše jednu WooCommerce prodavnicu (jedinstven indeks `(TenantId, Type)` na `SalesChannel`);
- porudžbine prate i vrednost za korisnika i opterećenje servera, a broje se jeftino (`ExternalOrders` po `PlacedAt` u tekućem mesecu, indeks `(TenantId, PlacedAt)` postoji).

| Plan | Mesečno | Godišnje | Porudžbina mesečno | Korisnici |
|---|---|---|---|---|
| Start | 1.990 | 19.900 | do 300 | 2 |
| Rast | 3.990 | 39.900 | do 1.500 | 5 |
| Veće | po dogovoru | | preko 1.500 | |

- Sinhronizacija sa prodavnicom i svi izveštaji iz SPEC 2.6 su u **svakom** planu. Bez njih aplikacija ciljnom korisniku (WooCommerce webshop) ne daje ništa više od Excela.
- Artikli su bez limita, uz meki limit od 5.000 radi zaštite baze.
- Broj korisnika se ne ističe kao prednost plana dok ne postoje uloge (SPEC 11.2).
- Godišnje = 10 meseci za cenu 12.
- **Bez trajnog besplatnog plana.** Proba traje 30 dana, bez kartice (14 dana je kratko: prva nedelja ode na uvoz i popis). Besplatni plan bez sinhronizacije, za firme bez webshopa, može kasnije kao marketinški kanal.
- **Zaključana cena** važi dok pretplata traje bez prekida. Posle isteka i ponovnog aktiviranja važi tadašnji cenovnik.
- **PDV:** odlučiti pre prvog plaćenog klijenta: ili je cena konačna i PDV posle ulaska u sistem pada na mene, ili se od početka objavljuju „cene bez PDV-a”. Mnogi mali webshopovi su paušalci i PDV ne mogu da odbiju, pa bi im ulazak u PDV poskupeo pretplatu za 20%.

### Limiti

- Limit porudžbina je **meki**: obrada porudžbina se nikad ne prekida zbog limita, jer bi stanje postalo netačno. Prekoračenje znači upozorenje u aplikaciji i prelazak na viši plan od sledećeg meseca.
- Limiti korisnika i artikala se proveravaju u Application sloju na svakom mestu gde nastaju: ručno dodavanje artikla, CSV uvoz, uvoz iz prodavnice, poziv korisnika.
- Greška je kod sa parametrima u `ProblemDetails` (npr. `plan.limit_reached` sa `limit` i `current`), a frontend je prevodi preko `errors.*`.

## 4. Životni ciklus pretplate

`Trial` → `Active` → `Grace` (7 dana posle isteka, puni pristup uz upozorenje) → `Expired`, ili `Canceled`.

Status se **ne čuva**, već se računa iz `ValidUntil`, `GraceUntil`, `CanceledAt` i trenutnog vremena. Dnevni posao samo šalje emailove pred istek. Tako posao koji jednog dana ne radi ne može ni da zaključa klijenta ni da mu ostavi pristup koji nije platio.

### `Expired`

- **Aplikacija je samo za čitanje**, uz CSV izvoz artikala sa stanjem. Izvoz ne postoji u v1, pa se pravi pre koraka 1.
- **Sprovodi se na jednom mestu**: filter ili middleware odbija zahteve koji menjaju podatke i vraća `ProblemDetails` sa kodom `subscription.expired`. Izuzeci su prijava, podešavanja korisnika i naplata. Nema provere u svakom kontroleru (isti princip kao `CanSeePurchasePrices`, SPEC 11.2).
- **Slanje stanja u prodavnicu staje odmah.** WooCommerce i sam smanjuje stanje (`manage_stock: true`), pa prodavnica nastavlja da radi sama.
- **Prijem porudžbina** (webhook i usklađivanje) nastavlja još 30 dana, da podaci ostanu tačni. Posle toga se webhookovi brišu iz prodavnice.
- **Ponovno aktiviranje**: jedno usklađivanje od `LastSyncedAt` nadoknađuje propušteno, jer obrada primenjuje samo razliku (SPEC 6). Pre prvog slanja stanja korisnik bira da li se stanje preuzima iz prodavnice, jer bi slanje pregazilo prijeme koje je u međuvremenu uneo ručno u WooCommerce.
- **Pre isteka** korisnik dobija jasnu poruku: „posle isteka prodavnica sama vodi stanje”.
- **Otvoreno:** posle koliko meseci u `Expired` se podaci firme brišu.

## 5. Predlog šeme (korak 1 i 2)

Planovi su u kodu (statička lista sa kodom, cenama i limitima), ne u bazi: provere limita su ionako u kodu, pa svaka promena plana traži deploy.

| Entitet | Polja | Napomena |
|---|---|---|
| `Subscription` | Id, TenantId, PlanCode, BillingPeriod, StartedAt, ValidUntil, GraceUntil?, CanceledAt?, PaymentMethod, PaddleSubscriptionId?, LockedPrice, Currency | Tačno jedna po firmi (jedinstven indeks na `TenantId`); promena plana menja taj red. `ITenantOwned`. |
| `Payment` | Id, TenantId, SubscriptionId, Amount, Currency, PeriodFrom, PeriodTo, InvoiceNumber?, PaymentReference?, PaddleTransactionId?, Status, InvoicedAt, PaidAt? | Istorija uplata. Status: Pending, Paid, Reversed. `ITenantOwned`. |

- Novac je `decimal(18,2)`, vreme `DateTimeOffset` u UTC (CLAUDE.md).
- `Currency` postoji jer Paddle naplaćuje i u EUR.
- U koraku 1 može i bez `Subscription`: dovoljno je `PaidUntil` na `Tenant`, a tabele dolaze sa koracima koji ih traže.

### Prelaženje granice firmi

Nove metode koje čitaju bez filtera firme idu u `IAppDbContext`, uz postojeće (`FindChannelTenantAsync`, `ListAllChannelsAsync`, `ListAllTenantIdsAsync`...), da se vide na jednom mestu:
- Paddle webhook nalazi firmu po `PaddleSubscriptionId`, pa je postavlja (kao webhook prodavnice);
- dnevni posao pred istek čita pretplate svih firmi.

Paddle webhookovi se čuvaju kroz isti inbox kao `WebhookEvent`, pa su ponovljeni i zakasneli događaji bezbedni.

Ručno označavanje uplata traži operatera koji vidi sve firme. Takva uloga ne postoji, pa u koraku 1 to radi komanda ili SQL, a ekran dolazi tek kad zatreba.

## 6. Veza sa stavkama van v1

- **SEF**: moje fakture se izdaju van aplikacije; SEF kao funkcija za korisnike ostaje van v1 (SPEC 2.8, 11.6).
- **Više magacina**: kandidat za osobinu višeg plana kad se napravi, ne pre.
- **Uloge (SPEC 11.2)**: preduslov da broj korisnika bude stvarna razlika među planovima.
