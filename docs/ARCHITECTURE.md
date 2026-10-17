# Arhitektura i način rada

Ovaj dokument objašnjava kako je projekat organizovan, zašto postoji svaki fajl i kako se dodaje nova funkcionalnost. Opisuje stanje posle koraka „Greške kanala” i dopunjuje se posle svakog većeg koraka.

Pravila projekta su u [`CLAUDE.md`](../CLAUDE.md), a specifikacija v1 u [`SPEC.md`](SPEC.md).

---

## 1. Način rada

**Mali koraci koji se mogu proveriti.** Svaki korak je jedna zaokružena celina koja radi i ima testove: prvo kostur, zatim prijava, pa frontend. Ako nešto pođe naopako, zna se tačno u kom koraku, a git istorija se čita kao dnevnik projekta. Zato su commitovi mali i imaju opis šta je urađeno i zašto.

**Pravila iz CLAUDE.md kao ugovor.** Pre svakog koraka se čitaju SPEC i CLAUDE.md i poštuju se ograničenja: bez novih paketa bez dogovora, `TenantId` na svim podacima firme, kodovi grešaka umesto poruka, API spreman za mobilnu aplikaciju. CLAUDE.md je „ustav” projekta. Kad je odluka jednom zapisana, ne donosi se ponovo svaki put.

**Proveri, pa tvrdi.** Nijedan korak nije gotov dok nije pokrenut: build, testovi, pravi HTTP poziv, pregled OpenAPI dokumenta. Kad nešto padne, prvo se istražuje uzrok, pa se tek onda popravlja.

**Testovi protiv prave baze.** Integracioni testovi podižu pravi Postgres u Dockeru. Lažna baza u memoriji ne proverava ograničenja, transakcije ni SQL, a baš tu nastaju greške koje su bitne.

**Najjednostavnije rešenje koje ne zatvara vrata.** Primer: Identity je podešen tako da kasnije prihvati bearer token za mobilnu aplikaciju, ali se sada koristi samo cookie. Buduća potreba se ne pravi unapred, samo se pazi da ne bude onemogućena.

---

## 2. Arhitektura: zašto četiri projekta

```
Zalihe.Web  ──►  Zalihe.Infrastructure  ──►  Zalihe.Application  ──►  Zalihe.Domain
(HTTP)           (baza, Identity, spolja)     (slučajevi korišćenja)    (pravila, entiteti)
```

Strelica znači „zna za” (ima referencu). Ključno pravilo: **unutrašnji slojevi ne znaju za spoljne.**

| Sloj | Šta sadrži | Zašto je odvojen |
|---|---|---|
| **Domain** | Entiteti (`Tenant`) i pravila (naziv firme ne sme biti prazan) | Nema nijednu zavisnost, ni EF Core ni ASP.NET. Najvažnija logika (obračun stanja, obrada porudžbina) može da se testira za milisekunde, bez baze. |
| **Application** | Slučajevi korišćenja, interfejsi (`ISalesChannel`), zajednički tipovi (`AppError`) | Opisuje *šta* aplikacija radi, ne *kako*. Jezgro radi sa `ISalesChannel` i ne zna da WooCommerce postoji. |
| **Infrastructure** | EF Core, Postgres, Identity, WooCommerce klijent, šifrovanje tajni, Hangfire, kasnije email | Sve što priča sa spoljnim svetom. Promena baze ili email servisa dira samo ovaj sloj. |
| **Web** | Kontroleri, filteri, konfiguracija, servira React | Tanak sloj koji HTTP zahtev pretvara u poziv servisa i rezultat u HTTP odgovor. |

Ovo je „Clean Architecture light”: slojevi postoje, ali nema MediatR-a, CQRS-a i sličnih šablona. Servis je obična klasa sa metodama koju kontroler direktno poziva.

**Kompromis:** `AccountService` je u Infrastructure, a ne u Application. Radi sa `UserManager<User>` iz Identity biblioteke, a `User` nasleđuje `IdentityUser`, koji je infrastrukturni tip. Da je u Application, taj sloj bi zavisio od Identity-ja. Poslovni servisi (artikli, zalihe) idu u Application.

---

### Izolacija firmi (multi-tenancy)

Sve firme dele jednu bazu. Najvažnije pravilo projekta je da **firma A nikad ne vidi podatke firme B**. To ne zavisi od toga da li je neko u upitu setio da napiše `where TenantId = ...`, već je ugrađeno u četiri sloja zaštite:

```
prijava ──► cookie sa claimom "tenant_id"
              │
              ▼
TenantMiddleware ──► TenantContext.Set(tenantId)          (jednom po zahtevu)
              │
              ▼
AppDbContext: global query filter  "TenantId == trenutna firma"
              │                    (EF ga dodaje u SVAKI upit za ITenantOwned entitete)
              ▼
SaveChanges: provera da novi podatak pripada trenutnoj firmi
```

1. **Claim u cookie-ju.** Pri prijavi `AppClaimsPrincipalFactory` upisuje `tenant_id` u cookie, pa se firma ne čita iz baze pri svakom zahtevu.
2. **`TenantContext`** čuva firmu za trajanje zahteva. Kasnije će ga webhookovi i pozadinski poslovi postavljati eksplicitno.
3. **Global query filter.** Svaki entitet koji implementira `ITenantOwned` automatski dobija filter, pa servis piše samo `db.Items.Where(i => i.Sku == sku)`, a SQL dobija i `AND "TenantId" = @tenant`.
4. **Bez firme nema podataka.** Upit bez postavljene firme baca izuzetak, umesto da vrati sve ili ništa. A `SaveChanges` odbija da upiše podatak sa tuđim `TenantId`.

Test `TenantFilterTests` pada čim neko doda entitet sa kolonom `TenantId` koji nema filter, a `ItemsTests` proverava da firma B ne vidi i ne može da otvori artikal firme A.

### Zalihe: kretanja i stanje

Najvažnije pravilo projekta: **stanje se nikad ne menja direktno, a uvek je jednako zbiru kretanja.**

```
POST /api/items/{id}/movements  (prijem, prodaja, povrat, korekcija na prebrojano)
  │
  ▼  StockService.RecordAsync
BEGIN TRANSACTION
  SELECT stanje artikla ... FOR UPDATE      ← drugo kretanje za isti artikal čeka ovde
  StockMovement.Receipt/Sale/Return/AdjustmentToCount   (pravila predznaka u domenu)
  StockLevel.Apply(kretanje)                (stanje += količina)
  SaveChanges                               (kretanje i stanje zajedno)
COMMIT
```

- **Kretanje se samo dodaje.** `StockMovement` nema metode za izmenu, a API nema endpoint za izmenu ni brisanje. Greška se ispravlja korekcijom.
- **Predznak čuva domen:** prijem i povrat su `+`, prodaja je `−` (korisnik upisuje pozitivnu količinu), a korekcija je razlika između prebrojanog i trenutnog stanja, koju računa API, ne korisnik.
- **Istovremeni unosi:** red sa stanjem se zaključava (`LockStockLevelAsync`, `SELECT … FOR UPDATE`), pa se kretanja za isti artikal izvršavaju jedno za drugim. `RowVersion` (Postgres `xmin`) je dodatna zaštita. Test `Record_TenConcurrentReceipts_NoneIsLost` to proverava.
- **Stanje sme u minus**, jer porudžbine sa sajta stižu bez obzira na stanje. Forma za ručnu prodaju samo upozori.
- **Status** (`StockStatusRules`): ≤ 0 je „Nema na stanju”, ≤ minimuma je „Ispod minimuma”, ostalo „Na stanju”. Isti uslovi se koriste kao SQL filter u listi.

### Porudžbine: primenjuje se samo razlika

Prodavnica za istu porudžbinu šalje mnogo događaja: duplirane, zakasnele, van redosleda. Zato se ne obrađuje svaki prelaz statusa („otkazana” → vrati robu), već se svaki put izračuna **koliko robe porudžbina treba da drži sada** i primeni samo razlika prema onome što već drži (SPEC 6).

```
OrderService.ProcessAsync(kanal, porudžbina iz prodavnice)
  │
BEGIN TRANSACTION
  SELECT porudžbina ... FOR UPDATE           ← drugi događaj za istu porudžbinu čeka ovde
  SELECT stavke porudžbine                   (posebnim upitom, posle zaključavanja)
  ExternalOrder.Apply(status, vreme, stavke) → spisak razlika po artiklu
      stariji događaj od poslednjeg?  → ništa
      za svaku stavku: željeno = aktivan status ? naručeno : 0
                       razlika = željeno − već primenjeno
  za svaku razliku (artikli uvek istim redom):
      zaključaj stanje artikla, Sale ili Return sa ExternalRef = broj porudžbine
  SaveChanges
COMMIT
```

- **Aktivni statusi** su `processing`, `on-hold` i `completed`. Sve ostalo (`pending`, `cancelled`, `refunded`, `failed`...) drži 0, pa otkazivanje samo vraća robu.
- **Stavka koje više nema** u porudžbini tretira se kao naručeno 0.
- **Nepovezan proizvod** (nema artikal u Zalihama) se čuva kao stavka bez artikla i ništa ne skida. Kad se proizvod poveže, isti događaj ponovo obrađen skida robu (`Apply` sa istim vremenom je dozvoljen).
- **Roba se vraća istom artiklu** sa kog je skinuta, čak i ako je proizvod u međuvremenu povezan sa drugim artiklom.
- **Dva događaja za novu porudžbinu u istom trenutku** (WooCommerce šalje `order.created` i odmah `order.updated`): porudžbina se upisuje sa `INSERT … ON CONFLICT DO NOTHING`, pa drugi upis sačeka prvu transakciju i ne uradi ništa, a zatim sačeka zaključavanje. Nijedan ne pada.

## 3. Folder struktura

```
zalihe/
├── CLAUDE.md                  pravila projekta
├── docs/
│   ├── SPEC.md                specifikacija v1
│   └── ARCHITECTURE.md        ovaj dokument
├── Zalihe.slnx                solution: spisak svih .NET projekata
├── dotnet-tools.json          lokalni .NET alati (dotnet-ef)
├── docker-compose.yml         Postgres, WordPress, MariaDB za razvoj
├── .gitignore                 šta git ne prati (bin/, obj/...)
├── src/
│   ├── Zalihe.Domain/
│   ├── Zalihe.Application/
│   ├── Zalihe.Infrastructure/
│   ├── Zalihe.Web/
│   └── web/                   React aplikacija
└── tests/
    ├── Zalihe.Domain.Tests/
    ├── Zalihe.Application.Tests/
    └── Zalihe.IntegrationTests/
```

**Unutar projekata, folderi su po oblasti, ne po tehničkoj vrsti.** Na primer `Web/Auth/` sadrži kontroler, zahteve, filter i podešavanje za prijavu, umesto da budu razbacani po `Controllers/`, `Models/` i `Filters/`. Kad se radi na prijavi, sve je na jednom mestu. Artikli dobijaju `Web/Items/`, `Domain/Items/` i tako dalje.

---

## 4. Fajl po fajl

### Koren projekta

**`Zalihe.slnx`**: novi XML format solution fajla (.NET 10). Samo nabraja projekte, da bi `dotnet build` i `dotnet test` u korenu obuhvatili sve, a VS Code (C# Dev Kit) znao šta da učita.

**`dotnet-tools.json`**: manifest lokalnih alata. U njemu je `dotnet-ef` (komanda za migracije) sa tačnom verzijom. Na drugom računaru `dotnet tool restore` instalira istu verziju. Alat nije globalni, pa svaki projekat ima svoju verziju.

**`docker-compose.yml`**: tri servisa za razvoj:
- `postgres`: baza aplikacije. Port je `5433:5432` (host:kontejner), jer je 5432 zauzet lokalno instaliranim PostgreSQL-om. `healthcheck` omogućava da `docker compose up --wait` sačeka dok baza stvarno ne primi konekcije.
- `mariadb` i `wordpress`: WordPress traži MySQL/MariaDB, i tu se instalira WooCommerce za testiranje integracije. `depends_on: condition: service_healthy` znači da WordPress ne kreće dok baza nije spremna.
- `wpcli`: WP-CLI za podešavanje test prodavnice; ne pokreće se sa `up` (profil `tools`), nego `docker compose run --rm wpcli bash /scripts/setup.sh`.
- `volumes`: imenovani volumeni čuvaju podatke kad se kontejneri ugase. `docker compose down` ih ne briše, `docker compose down -v` ih briše.

**`docker/woocommerce/setup.sh`**: pravi lokalnu WooCommerce prodavnicu (SPEC 9): instalira WordPress i WooCommerce, podesi RSD i srpske separatore, doda test proizvode (šest prostih, među njima jedan bez zaliha i jedan koji ne postoji u aplikaciji, i majicu sa četiri varijacije veličina × boja) i pri svakom pokretanju napravi nov API ključ za Zalihe i ispiše ga. Može da se pokrene ponovo, jer proizvode ne duplira.

**`docker/woocommerce/zalihe-local-https.php`**: samo za lokalni razvoj. WooCommerce prihvata API ključeve samo preko HTTPS-a, a test prodavnica je na `http://localhost:8080`, pa ovaj dodatak (mu-plugin) REST zahteve tretira kao HTTPS. Prave prodavnice su na HTTPS-u i ovo im ne treba.

**`docker/woocommerce/zalihe-local-webhooks.php`**: samo za lokalni razvoj. WordPress odbija da šalje zahteve na privatne adrese (`host.docker.internal`) i portove osim 80, 443 i 8080, a WooCommerce webhookove šalje preko WP-Cron-a, koji u ovom kontejneru ne radi (sajt misli da je na `localhost:8080`, a to unutar kontejnera ne postoji). Dodatak dozvoljava adresu i port 5131 i isporučuje webhook odmah, u istom zahtevu u kom je porudžbina sačuvana. Zato uvek učitava i REST putanje `wc/v3`: kupovina u prodavnici je Store API zahtev (`wc/store`), za koji WooCommerce učitava samo njegove putanje, a telo webhooka se pravi internim `wc/v3` pozivom. Bez toga webhook nosi grešku `rest_no_route` umesto porudžbine i aplikacija ga zanemari. Prave prodavnice ovo nemaju: webhook šalju iz pozadinskog zahteva, u kom su učitane sve putanje.

### Zalihe.Domain

**`Tenants/Tenant.cs`**: firma, koren svih podataka.
- Setteri su `private set`, pa spolja niko ne može da napiše `tenant.Name = ""`. Stanje se menja samo kroz konstruktor ili metode entiteta, a one čuvaju pravila.
- `Tenant(string name, DateTimeOffset createdAt)`: jedini javni način da se napravi firma. Odbija prazan naziv (`ThrowIfNullOrWhiteSpace`), skraćuje razmake (`Trim`) i odbija naziv duži od 200 znakova. Vreme prima kao parametar i ne zove sam `DateTime.Now`, pa u testu može da dobije tačno vreme.
- `Guid.CreateVersion7()`: GUID sa vremenskom oznakom. Rastu hronološki, pa su efikasniji kao ključ u bazi od nasumičnih GUID-ova.
- `private Tenant()`: prazan konstruktor samo za EF Core, koji njime pravi objekat pre nego što popuni polja iz baze.
- `NameMaxLength`: konstanta koju koriste i baza (dužina kolone) i validacija u API-ju, pa je broj 200 napisan samo na jednom mestu.

**`Tenants/ITenantOwned.cs`**: oznaka „ovo su podaci firme”. Ima samo `TenantId`. Svaki entitet koji ga implementira automatski dobija global query filter.

**`Items/Item.cs`**: artikal. Konstruktor i `Update()` čuvaju ista pravila (`Update()` prvo proveri sve vrednosti, pa tek onda menja, da odbijena izmena ne ostavi artikal napola izmenjen): naziv i šifra su obavezni i skraćuju se, prazni neobavezni tekstovi postaju `null`, minimalna zaliha i cene ne mogu biti negativne. Količina može imati najviše 3 decimale, a novac 2 (`HasAtMostDecimals()`), što odgovara kolonama `decimal(18,3)` i `decimal(18,2)`. Svaka WooCommerce varijacija je poseban artikal, a `GroupName` služi samo za grupisanje u prikazu. Artikal se **nikad ne briše**, jer mu istorija kretanja mora ostati: `Deactivate()` ga sklanja iz liste i izbora, a `Activate()` vraća.

**`Items/ItemChange.cs`**: dnevnik izmena artikla: napravljen, izmenjen (sa poljima staro → novo), deaktiviran, aktiviran. Kao i kretanja, zapisi se samo dodaju. `Item.Update()` vraća spisak polja koja su se stvarno promenila, sa vrednostima u neutralnom obliku (`"12.5"`, `"kom"`), a frontend ih formatira za jezik korisnika. Ako se ništa nije promenilo, zapisa nema.

**`Items/Unit.cs`**: jedinica mere kao `enum` (kom, kg, g, l, ml, m, pak). Fiksna lista omogućava da se količine u izveštajima sabiraju. U API-ju putuje kao kod (`"kom"`), a frontend ga prevodi (`items.units.kom`, na engleskom „pcs”).

**`Stock/StockMovement.cs`**: jedno kretanje zaliha. Fabričke metode `Receipt`, `Sale`, `Return`, `Adjustment` i `AdjustmentToCount` jedine prave kretanje i čuvaju predznak i broj decimala. `AdjustmentToCount` vraća `null` kad je prebrojano isto kao trenutno, jer nema šta da se upiše.

**`Stock/StockLevel.cs`**: trenutno stanje artikla, keš zbira kretanja. Menja se samo kroz `Apply(kretanje)`. `RowVersion` je token za istovremene izmene. Novi artikal dobija stanje 0 u istom čuvanju.

**`Stock/StockStatus.cs`**: status zaliha i `StockStatusRules.For(količina, minimum)`.

**`Users/Languages.cs`**: podržani jezici (`sr-Latn`, `en`), podrazumevani jezik i `IsSupported()` za proveru. Ovo je domensko pravilo, pa je ovde, a ne u Web-u.

**`Channels/SalesChannel.cs`**: povezana prodavnica (za sada samo WooCommerce, jedna po firmi). Ključevi i tajna za webhookove se čuvaju **samo šifrovani**; domen ih nikad ne vidi kao čist tekst. Status je `Connected` ili `Error`, uz kod poslednje greške (`LastErrorCode`, npr. `channel.unauthorized`) i vreme od kad prodavnica ne radi (`FailingSince`, od prvog neuspeha u nizu). Greške kanala (SPEC 7.6): `RecordFailure()` beleži neuspeh pozadinskog posla, a kanal prelazi u `Error` tek kad prodavnica ne radi bar `ErrorAfter` (30 minuta), jer jedan neuspeh je često prolazan (restart, spor hosting) i ne treba da uzbuni vlasnika. `MarkFailed()` je za proveru koju je korisnik sam pokrenuo, pa greška odmah važi. Svaki uspešan kontakt (`MarkConnected()`) briše grešku i vreme. Migracija `AddChannelFailingSince`. `NormalizeBaseUrl()` pretvara „zrno.rs/” u „https://zrno.rs”: dozvoljen je samo https, osim http-a na lokalnom računaru (test prodavnica), a adresa ne sme imati upit ni fragment.

**`Channels/ItemChannelMapping.cs`**: veza artikla sa proizvodom u prodavnici: `ExternalId` je ID prostog proizvoda ili varijacije, a `ParentExternalId` ID varijabilnog proizvoda (za varijacije). Konstruktor odbija artikal i prodavnicu različitih firmi. U bazi su dva jedinstvena indeksa: proizvod pripada jednom artiklu `(ChannelId, ExternalId)`, a artikal jednom proizvodu po prodavnici `(ChannelId, ItemId)`.

**Slanje stanja na vezi** (`ItemChannelMapping`): `StockChangedAt` je oznaka „stanje artikla se promenilo, a prodavnica ga još nema” (null kad je prodavnica ažurna), a `PushedStock` poslednje stanje poslato prodavnici. `ShopStock()` zaokružuje naniže na ceo broj, jer WooCommerce stanje je ceo broj: 2,5 kg se prikazuje kao 2, nikad kao više nego što ima. Migracija `AddStockPush`.

**`Orders/ExternalOrder.cs`**: porudžbina iz prodavnice, samo broj, status, vreme kad je naručena (`PlacedAt`, po prodavnici), vreme poslednje izmene i stavke, **bez podataka kupca**. `Apply(status, modifiedAt, stavke)` je cela logika iz SPEC 6 (vidi „Porudžbine: primenjuje se samo razlika”): vraća spisak `OrderStockChange` (artikal i promena stanja, negativna za prodaju), a za događaj stariji od poslednjeg vraća prazan spisak i ništa ne menja. Ista šifra proizvoda u više redova se sabira. `OrderLineSnapshot` je stavka onako kako je prodavnica šalje sada, sa artiklom sa kojim je proizvod povezan.

**`Orders/ExternalOrderLine.cs`**: stavka porudžbine. `OrderedQuantity` je koliko porudžbina sada traži, a `AppliedQuantity` koliko je već skinuto sa stanja, pa novi događaj primenjuje samo razliku. `ItemId` je `null` dok proizvod nije povezan; kad je roba jednom skinuta, artikal stavke se više ne menja. Stavke pravi samo porudžbina (`internal` konstruktor). `Name` je naziv proizvoda iz prodavnice (npr. „Duks sa kapuljačom - Plavi, L”), da bi se na ekranu porudžbina videlo šta je nepovezan proizvod; čuva se poslednji poslat, a predugačak se skraćuje.

### Zalihe.Application

**`Common/AppError.cs`**: jedna greška koja ide klijentu: `Code` (npr. `auth.email_taken`), `Field` (koje polje forme) i `Params` (npr. `{min: 8}`). Na ovom tipu počiva cela i18n priča: API šalje kod, a frontend ga prevodi preko `errors.auth.email_taken` i ubacuje parametre u rečenicu „Lozinka mora imati najmanje {{min}} znakova”. Ista greška radi na oba jezika i u mobilnoj aplikaciji.

**`Common/ITenantContext.cs`**: „za koju firmu radi ova operacija”. `TenantId` baca izuzetak ako firma nije postavljena.

**`Common/IAppDbContext.cs`**: pristup podacima za servise. Implementira ga `AppDbContext`, pa Application ne zna za Postgres. Zbog `DbSet<T>` ovaj sloj referencira paket `Microsoft.EntityFrameworkCore` (isti koji koristi Infrastructure).

**`Common/PagedResult.cs`**: jedna strana liste: `Items`, `TotalCount`, `Page`, `PageSize`. Svaka lista u API-ju se pagira na serveru (najviše 100 po strani).

**`Items/ItemService.cs`**: slučajevi korišćenja za artikle:
- `ListAsync()`: pretraga po nazivu, šifri ili barkodu (bez obzira na velika i mala slova), filter po kategoriji, sortiranje po nazivu, paginacija. Nijedan upit ne pominje `TenantId`, to radi filter.
- `GetAsync()`: jedan artikal ili `null` (artikal druge firme za ovaj servis „ne postoji”).
- `GetCategoriesAsync()`: kategorije koje firma već koristi, za predloge u formi.
- `CreateAsync()` i `UpdateAsync()`: isti `SaveItemCommand` i ista provera (`ValidateAsync`): decimale (`validation.too_many_decimals` sa `{max}`) i jedinstvenost šifre (`item.sku_duplicate`; pri izmeni se sam artikal ne računa). Neaktivni artikli i dalje „zauzimaju” svoju šifru.
- `UpdateAsync()` i `SetActiveAsync()` za artikal druge firme vraćaju „ne postoji”, jer ga global query filter i ne pronalazi.
- `ListAsync()` podrazumevano izostavlja neaktivne; `IncludeInactive` ih uključuje.
- `ToDto`: izraz koji EF prevodi u SQL, pa se iz baze čitaju samo potrebne kolone.

**`Common/ICurrentUser.cs`**: `ICurrentUser` (ko je uneo kretanje; Web ga čita iz cookie-ja) i `IUserDirectory` (imena korisnika za istoriju).

**`Stock/StockService.cs`**: `RecordAsync()` (transakcija, zaključavanje, kretanje i stanje zajedno; za korekciju je napomena obavezna), `GetHistoryAsync()` (najnovije prvo, sa imenom korisnika) i `GetSummaryAsync()` (koliko aktivnih artikala je ispod minimuma ili bez zaliha).

**`Items/ItemHistoryService.cs`**: istorija artikla: kretanja i izmene zajedno, najnovije prvo, sa filterom (`all`, `stock`, `changes`). Spajanje i paginacija rade se u bazi (`UNION ALL` samo nad ID-jevima i vremenom), a zatim se učitaju redovi te strane.

**`Dashboard/DashboardService.cs`**: pregled za Početnu u nekoliko upita: vrednost zaliha (aktivni artikli, pozitivno stanje × nabavna cena), broj aktivnih artikala, ispod minimuma i bez zaliha, pet artikala koje najhitnije treba naručiti (prvo bez zaliha, pa oni sa najmanjim delom minimuma) i šest poslednjih kretanja u firmi.

**`Imports/CsvFile.cs`** (`CsvParser`): čita CSV onako kako ga snimaju tabele u Srbiji i regionu: UTF-8 ili Windows-1250 (srpski Excel), sa `;`, `,` ili tabom (prepoznaje se iz prvog reda), sa navodnicima i prelomima reda u ćeliji. Brojevi redova odgovaraju Excel-u (zaglavlje je red 1). `UsesDecimalComma`: fajl sa `;` koristi decimalni zarez.

**`Imports/ItemImportRules.cs`**: čista pravila uvoza, bez baze: `SuggestMapping()` (prepoznaje kolone po nazivima, na srpskom i engleskom, bez obzira na kvačice), `ParseUnit()` („kom.”, „komada”, „pcs” → kom), `ParseDecimal()` i `ReadRow()` (red → podaci artikla ili greške po polju). **Brojevi:** nedvosmisleni oblici rade uvek („12,5”, „1.284,50”); jedini dvosmisleni oblik, jedan separator i tačno tri cifre („2.000”), prati konvenciju fajla, pa je u srpskom fajlu „2.000” dve hiljade.

**`Imports/ItemImportService.cs`**: tri koraka sa istim fajlom: `Analyze()` (kolone, primer redova, predlog mapiranja), `PreviewAsync()` (spremno, greške, preskočeno, uz razlog po redu) i `ImportAsync()` (ponovi istu proveru, pa upiše sve u jednom `SaveChanges`). Redovi sa greškom i redovi čija šifra već postoji se preskaču; postojeći artikli se nikad ne menjaju. Početno stanje je korekcija sa izvorom `Csv` i napomenom „Početno stanje”. Najviše 5000 redova; Excel fajl (`.xlsx`) se prepozna po prvim bajtovima i dobija jasnu poruku.

**Izmene u `ItemService`**: pri pravljenju, izmeni, deaktivaciji i aktivaciji upisuje i zapis u dnevnik izmena, u istoj transakciji (ponovna deaktivacija već neaktivnog artikla ne pravi zapis). lista i jedan artikal sada dolaze sa stanjem, statusom, vrednošću (stanje × nabavna cena) i prodajom u poslednjih 30 dana, u jednom upitu. Lista ima i filter po statusu.

**`Common/ICredentialProtector.cs`**: `Protect()` i `Unprotect()` za tajne. Application zna samo za interfejs, a šifrovanje radi Infrastructure.

**`Channels/ISalesChannel.cs`**: ugovor sa prodavnicom (SPEC 5): `CheckConnectionAsync()` i `FetchProductsAsync()`, koji vraća proizvode koji imaju stanje (`ExternalProduct`): proste proizvode i varijacije, a varijabilni proizvod je samo njihova grupa. Čitanje porudžbina i slanje stanja dolaze u sledećim koracima. `ISalesChannelFactory` pravi klijenta za adresu i ključeve kanala. Kad prodavnica odbije zahtev, klijent baca `SalesChannelException` sa kodom koji frontend prevodi (`channel.unreachable`, `channel.unauthorized`, `channel.not_woocommerce`, `channel.unexpected_response`).

**`Channels/ChannelService.cs`**: povezivanje prodavnice.
- `ConnectWooCommerceAsync()`: proveri adresu (`channel.url_invalid`) i oblik ključeva (`ck_…`, `cs_…`; `channel.keys_invalid`), proveri da firma već nema prodavnicu (`channel.already_connected`), a zatim **pozove prodavnicu pre čuvanja**. Ako ključevi ne rade, ništa se ne upisuje. Ključevi se čuvaju šifrovani kao JSON, uz nasumičnu tajnu od 32 bajta za buduće webhookove.
- `RecheckAsync()`: ponovo pozove prodavnicu sa sačuvanim ključevima i upiše status.
- `ReplaceCredentialsAsync()`: novi ključevi se čuvaju samo ako ih prodavnica prihvati.
- Sva tri koraka idu kroz `SetUpShopAsync()`: provera ključeva, pa `EnsureWebhooksAsync()` (prodavnica šalje porudžbine na adresu ovog kanala, potpisane tajnom kanala). Ključevi samo za čitanje prođu proveru, ali ne mogu da naprave webhook, pa daju `channel.unauthorized`. Zato „Proveri vezu” popravlja i webhook koji je prodavnica isključila posle neuspelih isporuka. Kanal se pri povezivanju pravi pre poziva prodavnice (webhook mora da zna njegov ID), a čuva se tek kad prodavnica prihvati.
- `SalesChannelDto` nema ključeve: API ih prima, ali ih nikad ne vraća.

**`Channels/ProductImportRules.cs`**: čista pravila uvoza proizvoda, bez baze. `Classify()` svaki proizvod svrsta u jedno od: već povezan (`Linked`), postoji artikal sa istom šifrom (`Match`), nov (`New`) ili preskočen (`Skipped`, uz kod: `channel.product_no_sku`, `channel.product_sku_too_long`, `channel.product_sku_duplicate` kad više proizvoda ima istu šifru, npr. varijacije koje nasleđuju šifru roditelja, i `channel.product_item_linked` kad je artikal sa tom šifrom već povezan sa drugim proizvodom). Šifra se poredi tačno (posle `Trim`), kao i jedinstvenost šifre u bazi. Tu su i pravila za vrednosti novog artikla: naziv se skraćuje na dužinu polja, cena na 2 decimale (negativna se odbacuje), stanje na 3 decimale.

**`Channels/ProductImportService.cs`**: uvoz proizvoda u dva koraka, kao CSV uvoz bez stanja na serveru:
- `PreviewAsync()`: pročita prodavnicu i vrati šta bi se desilo (povezivanje, novi proizvodi sa cenom i stanjem, preskočeni sa razlogom). Ništa ne upisuje.
- `ImportAsync()`: ponovo pročita prodavnicu, poveže sve proizvode čija šifra postoji i napravi samo nove proizvode koje je korisnik izabrao (`createExternalIds`). Novi artikal dobija naziv, šifru, kategoriju, grupu, prodajnu cenu i nabavnu cenu („Cost of goods sold”, SPEC 7.2) iz prodavnice, jedinicu `kom` i minimalnu zalihu 0, a stanje iz prodavnice postaje početno stanje (korekcija sa izvorom `WooCommerce` i napomenom „Početno stanje”). **Postojeći artikli se nikad ne menjaju**, samo se povežu. Sve ide u jednom `SaveChanges`.
- Najviše 5000 proizvoda (`channel.too_many_products`). Greška prodavnice se vraća kao kod, isto kao pri povezivanju.

**`Channels/SalesChannelFactoryExtensions.cs`**: `Create(channel, protector)` pravi klijenta za sačuvanu prodavnicu, a ključeve dešifruje samo za taj poziv.

**`ShopOrder` i `ShopOrderLine`** (u `Channels/ISalesChannel.cs`): porudžbina kako je pročita webhook ili usklađivanje: broj, status, vreme nastanka i izmene i stavke (ID proizvoda ili varijacije i količina). Ime nije `ExternalOrder`, da se ne bi mešalo sa entitetom.

**`Channels/ISalesChannel.cs`, dopune za webhookove**: `EnsureWebhooksAsync(adresa, tajna)` u ugovoru sa prodavnicom; `IShopOrderReader` čita porudžbinu iz tela webhooka u formatu prodavnice (implementacija je u Infrastructure, jer zna WooCommerce JSON); `WebhookSettings` je javna adresa aplikacije (`Webhooks:BaseUrl`) i od nje pravi adresu za isporuku `{BaseUrl}/webhooks/woo/{channelId}`.

**`Common/IBackgroundJobs.cs`**: zakazivanje pozadinskih poslova. Svaki posao dobija firmu kao parametar, jer nema prijavljenog korisnika (SPEC 3.3). Application ne zna za Hangfire.

**`Webhooks/WebhookService.cs`**:
- `ReceiveAsync()`: proveri potpis (`IsSignedBy`: base64(HMAC-SHA256(telo, tajna)), poređenje u konstantnom vremenu, pa vreme odgovora ne otkriva koliko je lažni potpis bio blizu). Teme osim `order.created` i `order.updated` i tela koja nisu porudžbina vraćaju `Ignored`, na šta kontroler odgovara 200 da prodavnica ne bi isključila webhook. Porudžbina se upiše kao `WebhookEvent` i zakaže se posao. **Čuva se samo pročitana porudžbina (`ShopOrder` kao JSON), nikad telo isporuke**, u kome su ime, adresa, telefon i email kupca.
- `ProcessAsync()`: pozove `OrderService` i označi događaj kao obrađen. Već obrađen događaj preskače.
- `DeleteExpiredAsync()`: briše događaje svih firmi starije od 30 dana (`Retention`, SPEC 8). Zove ga dnevni posao, bez postavljene firme.
- `RecordFailureAsync()`: upiše razlog greške na događaj.

**`Orders/OrderListService.cs`**: lista porudžbina za ekran „Porudžbine” (`ListAsync`): pretraga po broju (i sa `#`) ili nazivu proizvoda, filter po statusu prodavnice i „samo sa nepovezanim proizvodima”, najnovije prvo (`PlacedAt`), sa paginacijom. Svaka porudžbina nosi stavke koje sada traži (uklonjene se izostavljaju) i da li u svom statusu skida robu (`TakesStock`, iz `ExternalOrder.ActiveStatuses`). Stavka ima stanje (`OrderLineState`): `Linked` (skida sa artikla), `Unlinked` (proizvod nije povezan, korisnik treba da ga poveže) i `LinkedAfterOrder` (proizvod je povezan posle porudžbine, pa po pravilu iz `OrderService` ne skida robu; nije greška i ne broji se kao nepovezan). Zato se nepovezanost računa iz trenutnih veza (`ItemChannelMappings`), a ne samo iz `ItemId` stavke. Artikal stavke je ugnežđen objekat (`OrderLineItemDto`): sa poljem `Unit?` direktno na stavci, generator OpenAPI-ja bi `Unit` označio kao nullable u celom klijentu, pa i kod artikala.

**`Orders/OrderSyncService.cs`**: usklađivanje (SPEC 7.4). WooCommerce ne šalje ponovo webhook koji nije uspeo da isporuči, pa bi porudžbina iz vremena kad aplikacija nije radila zauvek ostala nepromenjena na stanju. `SyncAsync(channelId)` pita prodavnicu za porudžbine izmenjene od početka prethodnog usklađivanja minus 10 minuta (`Overlap`: prodavnica može imati sat koji kasni, a porudžbina sačuvana baš dok je prethodno usklađivanje čitalo bi se inače izgubila) i svaku propusti kroz `OrderService`. Ponovljena porudžbina ne menja ništa, pa preklapanje ne smeta. Prvo usklađivanje kreće od povezivanja: starije porudžbine su već u stanju preuzetom pri uvozu. Na kraju upiše početak kao `LastSyncedAt` (`SalesChannel.MarkSynced`, koji ne vraća vreme unazad ako stariji posao završi posle novijeg). Ako prodavnica ne odgovori, zabeleži neuspeh na kanalu (`RecordFailure`), baca `SalesChannelException` i `LastSyncedAt` ostaje, pa sledeće usklađivanje traži iste porudžbine. Kad prodavnica odgovori, kanal je ponovo `Connected`.

**`Channels/StockPushService.cs`**: slanje stanja u prodavnicu (SPEC 7.5): aplikacija je izvor istine, a prodavnica samo prikazuje. Tri koraka:
- `MarkChangedAsync(artikli)`: pre `SaveChanges`, u istoj transakciji kao kretanje, označi veze tih artikala (`MarkStockChanged`) i vrati prodavnice kojima treba slati. Zovu ga `StockService.RecordAsync` i `OrderService.ProcessAsync` (prodavnica je za porudžbinu već sama promenila stanje, ali slanje našeg ih drži istim i ispravi ako su se razišli). `ProductImportService` direktno označi veze sa **postojećim** artiklima, jer oni zadržavaju svoje stanje i prodavnica ga preuzima; novi artikli imaju stanje prodavnice, pa se ne šalju. CSV uvoz pravi samo nove, nepovezane artikle.
- `Schedule(prodavnice)`: **posle** commit-a zakaže posao, da bi posao video oznake.
- `PushAsync(channelId)`: jednim upitom pročita označene veze sa trenutnim stanjem, pošalje ih i skine oznake. Oznaka se skida uslovno (`ExecuteUpdateAsync ... WHERE StockChangedAt = pročitana vrednost`), pa promena koja stigne dok se šalje ostaje označena i ide sledeći put. Stanje koje se u celim jedinicama nije promenilo (2,5 → 2,3 kg) se ne šalje, samo se oznaka skine. Proizvod koji prodavnica odbije (npr. obrisan u prodavnici) ostaje označen. Ako prodavnica ne odgovori, sve oznake ostaju, a neuspeh se beleži na kanalu (`RecordFailure`); kad odgovori, kanal je ponovo `Connected` (odbijen proizvod nije kvar veze).

Zašto oznake, a ne slanje „onoga što se promenilo” iz samog posla: posao uvek šalje **trenutno** stanje, pa redosled i broj poslova nisu bitni. Dva posla jedan za drugim su bezopasna: drugi ne nađe ništa. Ništa se ne šalje za proizvode čije se stanje nije menjalo, pa povezivanje ne uključuje praćenje stanja u prodavnici samo od sebe za proizvode koje vlasnik nije dirao.

**`Orders/OrderService.cs`**: `ProcessAsync(channelId, porudžbina)` u jednoj transakciji zaključa porudžbinu, poveže stavke sa artiklima preko `ItemChannelMappings` te prodavnice (**samo ako je veza starija od porudžbine**: stanje artikla u trenutku povezivanja već ne sadrži tu porudžbinu, jer je preuzeto iz prodavnice pri uvozu ili ga je korisnik sam izbrojao; bez ovog pravila bi stara porudžbina koja se kasnije promeni, npr. „processing” → „completed”, skinula robu drugi put. Vreme nastanka je po satu prodavnice, a vreme veze po našem, pa porudžbina napravljena u minutima oko uvoza može da ispadne na pogrešnu stranu), pozove `ExternalOrder.Apply()` i za svaku razliku upiše `Sale` ili `Return` (izvor `WooCommerce`, `ExternalRef` = broj porudžbine, vreme = vreme izmene porudžbine, bez korisnika) i ažurira stanje. Stanja artikala zaključava uvek istim redom (po ID-ju), da se dve porudžbine sa istim artiklima ne bi zaglavile (deadlock). Vraća `ProcessOrderResult`: da li je događaj zanemaren kao stariji, broj kretanja i broj nepovezanih stavki. Firma mora biti postavljena pre poziva; tuđi kanal baca izuzetak. Pozivaju ga posao za webhook i usklađivanje.

### Zalihe.Infrastructure

**`Identity/User.cs`**: korisnik. Nasleđuje `IdentityUser<Guid>`, pa dobija email, hash lozinke, zaključavanje i ostalo što Identity nudi, i dodaje `TenantId` (kojoj firmi pripada) i `Language`. `<Guid>` znači da je ključ GUID, a ne string.

**`Persistence/AppDbContext.cs`**: veza sa bazom.
- Nasleđuje `IdentityDbContext`, pa automatski dobija Identity tabele (`AspNetUsers` i ostale).
- `DbSet<Tenant> Tenants`: tabela firmi.
- `OnModelCreating`: opisuje šemu: dužine kolona, strani ključ korisnik → firma i indeks na `TenantId`. `OnDelete(Restrict)` sprečava brisanje firme koja ima korisnike.
- Ovde su i **global query filteri** za `TenantId` (vidi „Izmene u `AppDbContext.cs`” niže).

**`Identity/UserDirectory.cs`**: imena korisnika za istoriju kretanja. Korisnici nemaju global query filter, pa je ovo jedino mesto koje ih izričito ograničava na trenutnu firmu.

**Dnevnik izmena u `AppDbContext.cs`**: tabela `ItemChanges`, a promenjena polja su jedan JSON dokument po zapisu (`jsonb`). Migracija `AddItemChanges` postojećim artiklima dodaje zapis „napravljen” sa njihovim datumom, bez korisnika.

**Kretanja u `AppDbContext.cs`**: tabele `StockMovements` (indeksi za istoriju po artiklu i prodaju po periodu) i `StockLevels` (ključ je `ItemId`, `RowVersion` mapiran na `xmin`), plus `LockStockLevelAsync()` sa `SELECT *, xmin … FOR UPDATE`. Migracija `AddStockMovements` postojećim artiklima dodaje stanje 0.

**`Tenancy/TenantContext.cs`**: implementacija `ITenantContext`, jedna po zahtevu (scoped). `Set()` odbija da promeni već postavljenu firmu.

**`Identity/AppClaims.cs`**: `AppClaimsPrincipalFactory` dodaje claim `tenant_id` u cookie pri prijavi.

**Izmene u `AppDbContext.cs`** (artikli i izolacija firmi):
- `Items` sa dužinama kolona, preciznošću decimala, jedinstvenim indeksom `(TenantId, Sku)` i indeksom `(TenantId, Name)` za sortiranje.
- `Unit` se čuva kao tekst (`"kom"`), da bi baza bila čitljiva i u SQL alatu.
- `ApplyTenantFilters()` prolazi kroz sve entitete i svakom koji implementira `ITenantOwned` dodaje `HasQueryFilter(e => e.TenantId == CurrentTenantId)`. EF `CurrentTenantId` čita pri svakom upitu, pa svaki zahtev vidi svoju firmu.
- `EnsureNewDataBelongsToCurrentTenant()` je poslednja odbrana: pri `SaveChanges` proverava da nijedan novi podatak nema tuđi `TenantId`.

**`Persistence/Migrations/`**: generisani fajlovi, ne pišu se ručno.
- `..._Initial.cs`: `Up()` pravi tabele, a `Down()` ih briše.
- `AppDbContextModelSnapshot.cs`: EF-ova slika trenutnog modela. Pri sledećoj migraciji EF poredi model sa ovom slikom i generiše samo razliku.

Nova migracija i primena:

```bash
dotnet ef migrations add NazivPromene -p src/Zalihe.Infrastructure -s src/Zalihe.Web -o Persistence/Migrations
dotnet ef database update -p src/Zalihe.Infrastructure -s src/Zalihe.Web
```

**`Identity/AccountService.cs`**: registracija.
- `RegisterCommand` i `RegistrationResult`: ulaz i izlaz. Rezultat nosi ili korisnika ili listu grešaka. Greške pri unosu su očekivane, pa se vraćaju kao rezultat, a ne bacaju kao izuzeci. Izuzetak je za ono što ne bi smelo da se desi.
- `RegisterAsync()`:
  1. proverava jezik;
  2. otvara **transakciju**;
  3. pravi i čuva `Tenant`;
  4. pravi korisnika preko `userManager.CreateAsync` (hešira lozinku i proverava pravila i jedinstvenost emaila);
  5. ako to ne uspe, vraća greške, a `await using` zatvara transakciju bez commita, pa se firma **poništava**;
  6. ako uspe, radi `CommitAsync`.

  Bez transakcije bi posle neuspele registracije ostajale firme bez korisnika. Ovo radi zato što `UserManager` koristi isti `DbContext` (isti scope), pa je u istoj transakciji.
- `GetTenantAsync()`: učitava firmu, koristi je `me` endpoint.
- `MapIdentityError()`: prevodi Identity kodove (`DuplicateEmail`, `PasswordTooShort`) u kodove aplikacije (`auth.email_taken`, `auth.password_too_short` sa `min`). `switch` izraz sa šablonima: poznati slučajevi eksplicitno, sve ostale greške lozinke kao `auth.password_*`, sve ostalo kao `auth.*`.
- `ToSnakeCase()` i `[GeneratedRegex]`: pretvara `PasswordRequiresDigit` u `password_requires_digit`. `GeneratedRegex` pravi regex pri kompajliranju, što je brže, i zato je klasa `partial`.

**`DependencyInjection.cs`**: `AddInfrastructure()` registruje sve iz ovog sloja u DI kontejner (`DbContext`, `AccountService`). Web samo pozove jednu metodu i ne mora da zna detalje.
- Connection string se čita **lenjo** (`(sp, options) => ...`), kad se `DbContext` prvi put zatraži, a ne pri pokretanju. Tako testovi stignu da podmetnu adresu svoje test baze.
- `AddScoped`: jedna instanca po HTTP zahtevu. To je pravilo za sve što koristi `DbContext`.

**`Security/DataProtectionCredentialProtector.cs`**: šifrovanje tajni preko ASP.NET Data Protection, sa sopstvenom namenom (`Zalihe.SalesChannels.Secrets.v1`), pa se ovi podaci ne mogu dešifrovati ključem za nešto drugo (npr. cookie). Ključevi za šifrovanje su lokalno u korisničkom profilu, a na serveru u folderu iz podešavanja `DataProtection:KeysPath`. **Ako se ti ključevi izgube, sačuvani API ključevi prodavnica više ne mogu da se pročitaju**, pa folder na serveru mora da ima rezervnu kopiju.

**`Channels/WooCommerce/WooCommerceClient.cs`**: WooCommerce REST API (`/wp-json/wc/v3`). Ključevi idu kao Basic autentifikacija. Provera veze traži jedan proizvod i odgovore prevodi u kodove: nema mreže ili istekne vreme → `unreachable`, 401/403 → `unauthorized`, 404 → `not_woocommerce`, a sve ostalo, uključujući HTML stranicu sigurnosnog dodatka umesto JSON-a, → `unexpected_response`. `SalesChannelFactory` u istom fajlu pravi klijenta preko imenovanog `HttpClient`-a „WooCommerce” (20 s, `User-Agent: Zalihe/1.0`).
- `FetchProductsAsync()` čita `/products` po 100 (`per_page=100&page=N`, dok strana nije puna), a za svaki varijabilni proizvod i `/products/{id}/variations`. `_fields` traži samo potrebna polja. Naziv varijacije je naziv proizvoda sa opcijama („Majica basic – M, Siva”), a grupa je naziv proizvoda. Grupisani i spoljni proizvodi nemaju sopstveno stanje i preskaču se. Cena je `regular_price` (tekst, npr. `"1250.00"`), a ako je nema, `price`. `stock_quantity` je `null` kad prodavnica ne prati stanje. Kategorija je prva kategorija proizvoda, osim WooCommerce-ove podrazumevane „Uncategorized” (slug `uncategorized`). HTML entiteti u nazivima se dekodiraju (`&amp;` → `&`). Polje neočekivanog tipa daje `unexpected_response`, nikad 500.

**Uvoz proizvoda u `AppDbContext.cs`**: tabela `ItemChannelMappings` sa jedinstvenim indeksima iz `ItemChannelMapping`, migracija `AddItemChannelMappings`.

**`Channels/WooCommerce/WooCommerceClient.cs`, webhookovi**: `EnsureWebhooksAsync()` pročita postojeće webhookove (`GET /webhooks`), pa za `order.created` i `order.updated` napravi webhook koji nedostaje (`POST`) ili ažurira postojeći sa istom adresom (`PUT`, status `active` i tajna). Tuđi webhookovi (druga adresa) se ne diraju. Pri pravljenju webhooka WooCommerce odmah pošalje „ping” na adresu.

**`Channels/WooCommerce/WooCommerceOrderReader.cs`**: čita porudžbinu iz WooCommerce JSON-a: `id`, `status`, `date_created_gmt` i `date_modified_gmt` (UTC bez oznake zone) i stavke. Proizvod stavke je `variation_id` ako postoji, inače `product_id`; stavka obrisanog proizvoda (`product_id: 0`) se preskače. Polja kupca se ne čitaju uopšte. Koristi ga i usklađivanje.

**`Channels/WooCommerce/WooCommerceClient.cs`, usklađivanje**: `FetchOrdersModifiedSinceAsync(od)` čita `GET /orders?modified_after=…&dates_are_gmt=true&status=any&orderby=modified&order=asc`, stranu po stranu. `status=any` su svi statusi osim kante. `_fields=id,status,date_created_gmt,date_modified_gmt,line_items` znači da prodavnica **ni ne pošalje** podatke kupca (provereno na lokalnoj prodavnici preko HTTP-a; interni WordPress poziv `_fields` zanemaruje).

**`Channels/WooCommerce/WooCommerceClient.cs`, slanje stanja**: `UpdateStockAsync()` šalje proste proizvode na `POST /products/batch`, a varijacije na `POST /products/{roditelj}/variations/batch`, najviše 100 po zahtevu, sa `manage_stock: true` i `stock_quantity`. WooCommerce odgovara za svaki proizvod posebno: prihvaćen ima ID, a odbijen polje `error` (npr. `woocommerce_rest_product_variation_invalid_id`), pa metoda vraća samo prihvaćene ID-jeve. Oblik odgovora je proveren na lokalnoj prodavnici.

**`Jobs/HangfireBackgroundJobs.cs`**: `HangfireBackgroundJobs` zakazuje poslove, a `WebhookEventJob` postavi firmu i obradi događaj. Hangfire svaki posao pokreće u sopstvenom DI scope-u. Ako obrada padne, razlog se upiše na događaj iz **novog** scope-a (stari može da drži napola urađene izmene), a izuzetak se baci ponovo, pa Hangfire pokuša kasnije, sa sve dužim pauzama (podrazumevano 10 pokušaja).

**`Jobs/HangfireBackgroundJobs.cs`, slanje stanja**: `PushStock(firma, kanal)` zakaže `StockPushJob` posle `Jobs:StockPushDelaySeconds` (30 s u `appsettings.json`). Svaka promena zakaže svoj posao; prvi pošalje sve što se do tada promenilo (više stavki porudžbine ili niz brzih prijema ide u istom zahtevu), a ostali nađu malo ili ništa. Prazna vrednost isključuje slanje: integracioni testovi tako rade, jer bi inače svaka promena stanja u svakom testu u pozadini zvala lažnu prodavnicu usred drugih testova. Posao ima 5 ponovnih pokušaja.

**`Jobs/RecurringJobs.cs`**: poslovi po rasporedu, koje `Program.cs` upiše u Hangfire pri pokretanju (`ScheduleRecurringJobs`). Raspored je cron izraz iz `Jobs` u `appsettings.json`: `OrderSyncCron` (svakih 15 minuta) i `WebhookCleanupCron` (svakog dana u 3:30 UTC). Prazna vrednost briše posao; integracioni testovi tako isključe oba, da se usred testa ne bi pokrenulo usklađivanje svih test prodavnica. `OrderSyncJob.ScheduleAllAsync` pročita prodavnice svih firmi (`ListAllChannelsAsync`) i za svaku zakaže poseban posao `SyncChannelAsync(firma, kanal)`, pa spora ili pokvarena prodavnica ne zadržava ostale. Oba nemaju ponovne pokušaje (`AutomaticRetry(Attempts = 0)`): ponovni pokušaj je sledeće usklađivanje, koje kreće od poslednjeg uspešnog. Kad usklađivanje upiše kretanja, to se loguje, jer znači da su webhookovi nešto propustili. `WebhookCleanupJob` briše stare događaje.

**Hangfire u `DependencyInjection.cs`**: poslovi se čuvaju u istoj bazi, u šemi `hangfire`, koju Hangfire sam napravi. `EnableLongPolling` (Postgres LISTEN/NOTIFY) pokreće nov posao odmah, umesto na sledećoj proveri (podrazumevano na 15 s). Server za poslove radi u istom procesu kao API. `WebhookSettings` se čita iz `Webhooks:BaseUrl`; bez te vrednosti povezivanje prodavnice namerno pada (500), da se podešavanje u produkciji ne bi prećutno zaboravilo.

**Webhookovi u `AppDbContext.cs`**: tabela `WebhookEvents` (`Payload` je `jsonb`, indeks po `ReceivedAt` za brisanje posle 30 dana), migracija `AddWebhookEvents`. Samo tri metode prelaze granicu firmi, i sve su u `IAppDbContext`, da se vide na jednom mestu. `FindChannelTenantAsync()`: webhook zna samo ID prodavnice, pa se firma čita iz kanala bez filtera, i odmah zatim postavlja. `ListAllChannelsAsync()`: usklađivanje zakazuje posao za svaku prodavnicu. `DeleteWebhookEventsReceivedBeforeAsync()`: brisanje starih događaja (`ExecuteDeleteAsync`, jedan `DELETE` bez učitavanja).

**Porudžbine u `AppDbContext.cs`**: tabele `ExternalOrders` (jedinstven indeks `(ChannelId, ExternalId)`) i `ExternalOrderLines` (jedinstven indeks `(ExternalOrderId, ExternalProductId)`), migracija `AddExternalOrders`. ID stavke je `ValueGeneratedNever`: stavku pravi domen sa već postavljenim ID-jem, pa EF novu stavku na učitanoj porudžbini upiše kao novi red, a ne kao izmenu. `LockOrAddExternalOrderAsync()` prvo upiše porudžbinu ako je nema (`INSERT … ON CONFLICT DO NOTHING`, uz ručnu proveru firme, jer sirov SQL zaobilazi proveru u `SaveChanges`), zatim zaključa red porudžbine (`FOR UPDATE`) i posebnim upitom učita stavke. Kad bi stavke bile u istom upitu, upit koji je čekao na zaključavanje bi ih čitao onakve kakve su bile pre čekanja i primenio istu razliku dvaput. Tu grešku je otkrio test `Process_ConcurrentEventsForStoredOrder_AppliesTheDifferenceOnce`. Migracija `AddOrderListDetails` dodaje `PlacedAt` (postojećim porudžbinama upisuje vreme kad su stigle, jer pravo vreme nije bilo sačuvano), naziv stavke i indeks `(TenantId, PlacedAt)` za listu.

**Izmene u `AppDbContext.cs` i `DependencyInjection.cs`**: tabela `SalesChannels` (tip i status kao tekst, jedinstven indeks `(TenantId, Type)`), migracija `AddSalesChannels`; registracija Data Protection-a, imenovanog HTTP klijenta i servisa. Infrastructure sada referencira `Microsoft.AspNetCore.App` (deo .NET-a, ne NuGet paket), zbog Data Protection-a.

### Zalihe.Web

**`Program.cs`**: ulazna tačka, u dva dela:
1. **Registracija servisa** (`builder.Services...`): `TimeProvider` (sat koji se u testovima može zameniti), infrastruktura, Identity, `ProblemDetails` sa kodovima, kontroleri sa antiforgery filterom i formatom validacionih grešaka, OpenAPI.
2. **Pipeline** (`app.Use...`): redosled middleware-a kroz koje prolazi svaki zahtev. **Redosled je bitan:** `UseExceptionHandler` je prvi da uhvati greške iz svega posle njega, `UseAuthentication` mora pre `UseAuthorization` (prvo „ko si”, pa „smeš li”), a `MapControllers` je na kraju.

**`Auth/IdentitySetup.cs`**: `AddAppIdentity()`:
- `AddIdentityApiEndpoints<User>`: registruje Identity sa kombinovanom šemom `BearerAndApplicationScheme`. Zahtev može da se autentifikuje bearer tokenom (mobilna aplikacija kasnije) ili cookie-jem (web sada). Namerno se **ne** zove `MapIdentityApi()`, jer projekat ima sopstvene endpointe sa kodovima grešaka.
- Pravila lozinke: najmanje 8 znakova, bez obavezne složenosti (preporuka NIST-a).
- `ConfigureApplicationCookie`: ime cookie-ja, trajanje 14 dana i `SlidingExpiration` (produžava se dok je korisnik aktivan). `SameSite=Lax` znači da browser ne šalje cookie uz većinu zahteva sa drugih sajtova.
- `AddAntiforgery`: header u kom frontend šalje token.

**`Auth/AuthContracts.cs`**: oblici zahteva i odgovora (DTO). Oni su **odvojeni od entiteta**: klijent nikad ne dobija `User` direktno, jer bi tako procurio hash lozinke. Atributi `[Required(ErrorMessage = "validation.required")]` imaju **kod umesto poruke**, i taj trik omogućava da ugrađena validacija daje kodove aplikacije.

**`Auth/AuthController.cs`**: četiri endpointa. Svaka metoda samo pozove servis ili Identity i pretvori rezultat u HTTP odgovor:
- `Register`: poziva `AccountService`, pa `SignInAsync` (postavlja cookie). Vraća `204 No Content`, jer frontend posle toga ionako zove `me`.
- `Login`: `PasswordSignInAsync` proverava lozinku, postavlja cookie i broji neuspešne pokušaje (`lockoutOnFailure: true`, zaključavanje posle 5 pokušaja). Greška za pogrešan email i pogrešnu lozinku je ista (`auth.invalid_credentials`), da napadač ne može da otkrije koji emailovi postoje.
- `Logout`: briše cookie. Ima `[Authorize]` i traži antiforgery token.
- `Me`: vraća ko je prijavljen i izdaje antiforgery token.
- `[ProducesResponseType]` atributi opisuju svaki mogući odgovor u OpenAPI-ju, pa Orval generiše tačne tipove, uključujući greške.
- `Name = "Login"` u `[HttpPost("login", Name = "Login")]` postaje `operationId` u OpenAPI-ju, a od njega Orval pravi ime hooka (`useLogin`). Svaki novi endpoint treba da ima ime.

**`Auth/CookieAntiforgeryFilter.cs`**: zaštita od **CSRF** napada. Problem: ako je korisnik prijavljen, a zlonamerni sajt pošalje formu na API, browser sam doda cookie. Rešenje:
- `IssueToken()`: `me` postavlja cookie `XSRF-TOKEN` koji JavaScript **može da pročita** (`HttpOnly = false`). Tuđi sajt ne može da pročita cookie-je druge adrese.
- Frontend uz svaki POST, PUT ili DELETE kopira taj token u header `X-XSRF-TOKEN`.
- `OnAuthorizationAsync()`: za zahteve koji menjaju podatke proverava da li je korisnik prijavljen **cookie-jem**, i ako jeste, traži ispravan token. GET zahtevi i bearer token se ne proveravaju, jer ih browser ne šalje automatski. Tako je propisano u CLAUDE.md.
- Implementira `IAsyncAuthorizationFilter`, pa se izvršava pre akcije kontrolera. Registrovan je globalno u `Program.cs`, pa važi za svaki budući endpoint i ne može da se zaboravi.
- Prijava i registracija nemaju ovu proveru, jer korisnik tada još nije prijavljen (moguć je „login CSRF”, retka i niskorizična vrsta napada).

**`Errors/ApiProblemDetails.cs`**: format greške. Proširuje standardni `ProblemDetails` (RFC 9457) poljima `Code` i `Errors`. Postoji kao klasa da bi OpenAPI i Orval znali tačan oblik.

**`Errors/ApiProblems.cs`**: sve oko pravljenja grešaka:
- `Result()`: pravi odgovor sa greškom (status, kod, polja i `traceId` za pretragu logova) i tipom `application/problem+json`.
- `FromInvalidModelState()`: kad padne ugrađena validacija (`[Required]`, `[EmailAddress]`), pretvara je u format aplikacije sa kodom po polju. `ToCode()` prepoznaje da li je poruka kod (ima tačku, nema razmaka) ili tekst frameworka (npr. neispravan JSON), koji postaje `validation.invalid`. `ToFieldName()` pretvara `CompanyName` u `companyName`, kako se polje zove u JSON-u.
- `AddDefaultCode()`: greške koje pravi sam framework (401 kad korisnik nije prijavljen, 404, 500 kod neočekivanog izuzetka) takođe dobijaju kod. Frontend tako **uvek** ima kod za prevod.

Primer odgovora sa greškom:

```json
{
  "status": 400,
  "title": "Bad Request",
  "code": "validation.failed",
  "errors": [
    { "field": "password", "code": "auth.password_too_short", "params": { "min": 8 } }
  ],
  "traceId": "00-..."
}
```

**`Tenancy/TenantMiddleware.cs`**: posle autentifikacije čita claim `tenant_id` i postavlja `TenantContext`. Za sesije napravljene pre nego što je claim postojao, firmu jednom čita iz baze.

**`Items/ItemContracts.cs`**: `ItemRequest` (isti za dodavanje i izmenu) sa validacijom preko kodova. Neobavezna polja imaju podrazumevanu vrednost `null`, pa ih OpenAPI (i TypeScript) ne traži. `[Range]` granice se čitaju sa `ParseLimitsInInvariantCulture = true`: bez toga, na računaru sa srpskim podešavanjima (decimalni zarez) parsiranje `"9999999999999999.99"` baca izuzetak i svako pravljenje artikla vraća 500. Tu grešku čuva test `ItemContractsTests`.

**`Items/ItemsController.cs`**: `GET /api/items` (lista sa `search`, `category`, `includeInactive`, `page`, `pageSize`), `GET /api/items/{id}`, `GET /api/items/categories`, `POST /api/items` (vraća `201 Created`), `PUT /api/items/{id}`, `POST /api/items/{id}/deactivate` i `POST /api/items/{id}/activate`. Deaktivacija je zasebna akcija, a ne polje u izmeni, jer je to drugačija namera korisnika. Ceo kontroler ima `[Authorize]`.

**JSON podešavanja u `Program.cs`**: enumi putuju kao kodovi (`"kom"`), a brojevi moraju biti JSON brojevi (`NumberHandling.Strict`). Bez toga bi OpenAPI opisivao brojeve kao „broj ili tekst”, pa bi TypeScript tipovi bili `number | string`. Ista podešavanja važe i za generisanje OpenAPI dokumenta (`ConfigureHttpJsonOptions`).

**`Dashboard/DashboardController.cs`**: `GET /api/dashboard`.

**`Imports/ImportsController.cs`**: `POST /api/imports/items/analyze`, `/preview` i `/api/imports/items`, sve kao `multipart/form-data` (fajl + broj kolone za svako polje, `ItemImportForm`). Fajl se šalje u svakom koraku, pa server ne čuva stanje između koraka. Najveći fajl je 5 MB.

**`Stock/StockController.cs`**: `POST /api/items/{id}/movements` i `GET /api/stock/summary`. Istorija je u `GET /api/items/{id}/history` (`ItemsController`), zajedno sa izmenama artikla. Namerno nema endpointa za izmenu ili brisanje kretanja.

**`Auth/HttpCurrentUser.cs`**: `ICurrentUser` iz claima prijavljenog korisnika.

**`Controllers/HealthController.cs`**: `GET /api/health`. Brza provera da API radi, a kasnije i za monitoring u produkciji.

**`appsettings.Development.json`**: connection string za lokalni Docker Postgres. Učitava se samo u Development okruženju.

**`Properties/launchSettings.json`**: profili za `dotnet run`. Profil `http` pokreće API na portu 5131, i na njega pokazuje Vite proxy.

**`Zalihe.Web.http`**: fajl za ručno slanje zahteva iz VS Code-a („Send Request”).

**`Webhooks/WooCommerceWebhooksController.cs`**: `POST /webhooks/woo/{channelId}`, poziva ga prodavnica, ne korisnik: `[AllowAnonymous]`, bez antiforgery tokena (zahtev nema cookie) i van OpenAPI dokumenta (`IgnoreApi`). Redosled: ping (formular `webhook_id=…` bez potpisa) → 200 odmah, čak i pre nego što je kanal sačuvan; nepoznat kanal → 404; firma iz kanala → `TenantContext`; pogrešan potpis → 401; sve ostalo → 200. Telo se čita kao bajtovi, jer se potpis računa nad tačnim bajtovima, najviše 1 MB. MVC sam pročita telo formulara, pa se ping prepoznaje preko `Request.Form`, a ne iz tela.

**`appsettings.Development.json`, `Webhooks:BaseUrl`**: `http://host.docker.internal:5131`, adresa računara kako je vidi WordPress kontejner. U produkciji je to javna HTTPS adresa aplikacije.

**`Orders/OrdersController.cs`**: `GET /api/orders` (`search`, `status`, `unlinkedOnly`, `page`, `pageSize`). Samo čitanje; porudžbine menjaju samo prodavnica (webhook i usklađivanje).

**`Channels/ChannelsController.cs`**: `GET /api/channels`, `POST /api/channels/woocommerce` (201), `POST /api/channels/{id}/check` i `PUT /api/channels/{id}/credentials`, `GET /api/channels/{id}/products/preview` i `POST /api/channels/{id}/products/import` (telo: `createExternalIds`). Tuđi kanal vraća 404, kao i kod artikala. Pregled je GET jer ništa ne menja; greška prodavnice je 400 sa kodom.

### src/web (React)

```
src/web/
├── openapi.json            OpenAPI dokument API-ja (commituje se, iz njega Orval generiše klijent)
├── orval.config.ts         podešavanje generisanja klijenta
├── scripts/fetch-openapi.mjs  preuzima openapi.json iz pokrenutog API-ja
├── vite.config.ts          Vite, proxy /api → backend, podešavanje Vitest-a
└── src/
    ├── main.tsx            ulazna tačka: fontovi, Mantine stilovi, i18n, ruter
    ├── AppProviders.tsx    Mantine tema + TanStack Query (koriste ga i testovi)
    ├── queryClient.ts      podrazumevana podešavanja za upite (greške 4xx se ne ponavljaju)
    ├── router.tsx          sve rute aplikacije
    ├── theme.ts            Mantine tema i dizajn tokeni iz DESIGN.md
    ├── i18n.ts             podešavanje react-i18next, izbor jezika
    ├── locales/            sr-Latn.json i en.json
    ├── api/
    │   ├── http.ts         fetch koji koristi generisani klijent
    │   ├── errors.ts       ApiError i prevođenje kodova grešaka
    │   └── generated/      GENERISANO, ne menja se ručno
    ├── auth/               useCurrentUser, zaštita ruta
    ├── layout/             AuthLayout (prijava) i AppLayout (bočna navigacija)
    ├── pages/              ekrani: LoginPage, RegisterPage, HomePage
    ├── components/         male zajedničke komponente (Logo, Eyebrow, LanguageSwitch)
    ├── lib/format.ts       formatiranje brojeva i datuma preko Intl
    └── test/               podešavanje testova i pomoćne funkcije
```

**Kako frontend priča sa API-jem.** Klijent se nikad ne piše ručno:
1. Backend opisuje endpointe (`[ProducesResponseType]`, `Name = ...`).
2. `npm run api` preuzme `openapi.json` iz pokrenutog API-ja i pokrene Orval.
3. Orval u `src/api/generated/` napravi tipove (`CurrentUserResponse`, `RegisterRequest`...) i hookove (`useGetCurrentUser`, `useLogin`...).
4. Komponente koriste hookove. Ako se API promeni, TypeScript odmah pokaže gde frontend više ne odgovara.

`openapi.json` se commituje, pa se u git diff-u vidi svaka promena API-ja.

**`vite.config.ts`**: **proxy** `/api` → `http://localhost:5131` (promenljiva `API_URL` ga preusmerava, npr. `API_URL=http://localhost:5199 npm run dev`). U razvoju browser priča samo sa Vite-om (5173), a Vite prosleđuje API pozive backendu. Browser zato vidi jednu adresu, cookie radi bez CORS podešavanja, a produkcija izgleda isto (ASP.NET servira i React i API sa iste adrese). Tu je i podešavanje Vitest-a (jsdom, `src/test/setup.ts`).

**`api/http.ts`**: `customFetch()` je jedina funkcija kroz koju idu svi API pozivi (Orval je poziva iz generisanog koda):
- šalje cookie (`credentials: 'same-origin'`);
- za POST, PUT i DELETE čita cookie `XSRF-TOKEN` i šalje ga u headeru `X-XSRF-TOKEN` (antiforgery);
- za odgovor koji nije 2xx baca `ApiError`, pa TanStack Query zna da je zahtev pao.

**`api/errors.ts`**: greške API-ja na frontendu:
- `ApiError`: status, kod i lista grešaka po polju, pročitani iz `ProblemDetails`.
- `formErrorMessage()`: poruka za formu u celini (npr. „Pogrešan email ili lozinka.”). Nepoznat kod daje opštu poruku, a greška mreže „Server nije dostupan”.
- `fieldErrorMessages()`: poruke po polju (`{ email: "Nalog sa ovom email adresom već postoji." }`), sa parametrima (`{{min}}`). Forma ih samo prosledi u `error` prop polja.

**`theme.ts`**: jedino mesto sa hex vrednostima boja:
- dva seta tokena iz DESIGN.md, `dark` i `light`, istog oblika (`Tokens`). `schemeVariables()` od njih pravi CSS promenljive (`--z-bg`, `--z-surface`, `--z-accent-text`, `--z-status-low`...), a `cssVariablesResolver` ih postavlja za svaku temu;
- Mantine-ove promenljive (`--mantine-color-body`, `--mantine-color-dimmed`, `--mantine-color-disabled`...) usmerene su na te tokene, pa i ugrađene komponente prate temu umesto Mantine-ove hladne sive;
- paleta `olive` (primarna boja oko akcenta) i `dark` (Mantine iz nje izvodi podrazumevane boje tamne teme);
- fontovi, veličine, zaobljenja i razmaci, plus podrazumevani izgled za `Button` (40 px, tekst `on-accent` na maslinastoj), `Input`, `Modal`, `SegmentedControl`, `Table`, `Badge`...

Komponente koriste `var(--z-...)` ili Mantine propove (`c="dimmed"`), nikad hex. Kad Mantine stil nije dovoljan, koristi se CSS modul (npr. `AppLayout.module.css`), ali i tamo samo sa tokenima. Za tekst u akcent boji uvek se koristi `--z-accent-text`, a ne `--z-accent`, jer svetla maslinasta nije čitljiva na beloj pozadini.

**`colorScheme.ts` i `components/ThemeToggle.tsx`**: izbor teme. Tamna je podrazumevana, a izbor se čuva u browseru (`localStorage`, ključ `zalihe.colorScheme`), ne na nalogu, pa telefon i računar mogu imati različite teme. Prekidač stoji u bočnoj navigaciji i na stranici za prijavu. `index.html` ima mali skript koji pročita isti ključ pre nego što se React učita, da stranica ne bi bljesnula pogrešnom temom.

**`global.css`**: ono malo stilova koje Mantine tema ne može da izrazi: vidljiv fokus (`.z-focus`, okvir od 2 px u `accent-text` boji, preko `theme.focusClassName`), hover za linkove i širina modala (`--z-modal-width`: 760 px, na ekranima širim od 1920 px 880 px), koju tema koristi kao podrazumevanu veličinu svakog modala.

**`i18n.ts`**: učitava oba prevoda i bira jezik: pre prijave iz browsera (`localStorage`), a posle prijave iz `User.Language` (to radi `useCurrentUser`). `setLanguage()` menja jezik i pamti izbor. Pri promeni jezika ažurira se i `<html lang>`, što je bitno za čitače ekrana.

**`lib/format.ts`**: brojevi i datumi isključivo preko `Intl`:
- `formatNumber()`: `1.284.350` i `12,5` na srpskom, `1,284,350` i `12.5` na engleskom;
- `formatSignedQuantity()`: `+24` i `−2`, sa pravim znakom minus (`−`, ne crtica);
- `formatMoney()`, `formatLongDate()` („četvrtak, 1. oktobar”);
- `parseDecimal()`: unos korisnika prihvata i `12,5` i `12.5`. Kad postoje oba separatora, poslednji je decimalni (`1.284,5`).

**`auth/useCurrentUser.ts`**: jedan izvor istine o tome ko je prijavljen: poziva `GET /api/auth/me` preko generisanog hooka. Odgovor 401 znači da korisnik nije prijavljen (`isAnonymous`), i taj upit se ne ponavlja. Isti poziv izdaje i antiforgery token.

**`auth/RouteGuards.tsx`**:
- `RequireAuth`: deo aplikacije za prijavljene. Dok se proverava prikazuje loader, a neprijavljenog šalje na `/login` i pamti gde je hteo da ide.
- `PublicOnly`: prijavljenog korisnika sa `/login` i `/register` šalje u aplikaciju.

**`router.tsx`**: rute su ugnežđene: guard → layout → stranica. Tako svaka nova stranica za prijavljene automatski dobija zaštitu i bočnu navigaciju, samo se doda u listu. Ruta može da nosi i širinu sadržaja: `handle: { width: 'wide' }` za ekrane sa tabelama i pregledima, sada sve stranice aplikacije (do 1920 px), a bez toga važi `normal` (do 1180 px), predviđeno za stranice sa tekstom i formama.

**`layout/pageWidth.ts`**: `usePageWidth()` čita širinu iz najdublje rute, a `AppLayout` je primeni kao `data-width` na glavni sadržaj. Raspored je fluidan do te granice, a veličina teksta se ne menja sa širinom ekrana (DESIGN.md).

**`layout/AppLayout.tsx`**: okvir po maketi: bočna navigacija od 232 px, logo, stavke, firma i korisnik dole, dugme za odjavu. Na telefonu se navigacija sklapa iza dugmeta „meni”. U `navItems` su samo ekrani koji postoje.

**`pages/`**: forme drže stanje u `useState`, šalju ga generisanim hookom (`useLogin`, `useRegister`) i greške prikazuju preko `fieldErrorMessages` i `formErrorMessage`. Validaciju radi API, a frontend je samo prikazuje, kako traži pravilo „sva poslovna logika je u API-ju”. Posle uspešne prijave invalidira se upit `me`: tako se učita korisnik i novi antiforgery token, pa se ide dalje.

**`components/DecimalInput.tsx`**: polje za decimale. Čuva tačno ono što je korisnik otkucao (`12,5` ili `12.5`), a tek pri slanju ga `readDecimal()` iz `lib/format.ts` pretvara u broj. Ako unos nije broj, forma prikaže „Unesi broj” i ne šalje zahtev. To je jedina provera na frontendu, jer se tiče formata unosa, a ne poslovnih pravila.

**`pages/items/ItemsPage.tsx`**: ekran „Artikli” po maketi: naslov sa brojem artikala, pretraga, tabela (artikal sa kategorijom, šifra, minimalna zaliha sa jedinicom mere) i paginacija „1–20 od N”. Pretraga i strana su u adresi (`/items?q=etiop&page=2`), pa ih osvežavanje i dugme „nazad” čuvaju. Pretraga se šalje 300 ms posle poslednjeg slova (`useDebouncedValue`), a `keepPreviousData` drži staru listu dok stiže nova, pa tabela ne treperi. Klik na red otvara izmenu; za tastaturu je naziv artikla pravo dugme. Prekidač „Prikaži neaktivne” (`?inactive=1`) uključuje neaktivne artikle, koji imaju oznaku „Neaktivan” (tekst, ne samo boja). Kolone Stanje, Status, Prodaja 30d i Vrednost dolaze sa kretanjima zaliha.

**`pages/HomePage.tsx`**: Početna po maketi: traka sa četiri brojke, „Treba naručiti” (traka pokazuje koliki deo minimuma je ostao) i „Poslednja kretanja” (`formatRecentTime()`: danas vreme, „juče”, ranije datum). Nova firma vidi poziv da doda ili uveze artikle. Dugmad „Prijem robe” i „Prodaja van sajta” i kanali prodaje sa makete dolaze sa tim ekranima.

**`pages/items/ImportPage.tsx`**: uvoz u tri koraka (`Stepper`): fajl (uz uputstvo i primer fajla napravljen u browseru), kolone (predlog iz API-ja je unapred izabran, uz pregled prvih redova) i pregled (spremno, greške, preskočeno, problemi po redu i dugme „Uvezi N artikala”, sa ispravnom srpskom množinom).

**`pages/items/ItemDetailPage.tsx`**: stranica artikla (`/items/{id}`): traka sa stanjem, minimumom, prodajom za 30 dana i vrednošću, dugmad Izmeni / Prijem / Prodaja / Povrat / Korekcija i istorija (kretanja i izmene artikla, sa filterima Sve / Količina / Izmene artikla). `formatChangeValue()` prikazuje zabeležene vrednosti u formatu jezika, sa jedinicom i valutom. Klik na artikal u listi vodi ovde.

**`pages/items/MovementModal.tsx`**: unos kretanja. Ispod količine prikazuje stanje posle unosa ili razliku kod korekcije, i upozorenje kad stanje ide u minus. To je samo prikaz: stvarnu promenu računa API.

**`components/stock.tsx`**: `StockStatusLabel` (tačka + tekst, nikad samo boja), `StockQuantity` (broj sa jedinicom, obojen za niske zalihe) i `MovementQuantity` (`+24` / `−2`).

**`pages/items/ItemModal.tsx`**: jedna forma za dodavanje i izmenu. Bez `item` pravi novi artikal, a sa `item` popuni polja (decimale u formatu jezika, npr. `12,5`, preko `formatDecimalInput()`), šalje `PUT` i nudi „Deaktiviraj” ili „Aktiviraj”. Roditelj joj daje `key` po artiklu, pa forma pri svakom otvaranju kreće od podataka tog artikla. Jedinice mere dolaze iz generisane konstante `Unit`, dakle iz istog izvora kao na backendu. Kategorija nudi predloge iz `GET /api/items/categories`. Posle uspešnog čuvanja invalidiraju se lista i kategorije, pa se novi artikal odmah vidi.

**`layout/ChannelAlert.tsx`**: traka iznad svakog ekrana (osim „Kanali prodaje”, gde to piše na kartici) za svaku prodavnicu u grešci: od kada ne radi, prevedeni razlog, šta to znači (stanje ne stiže u prodavnicu, pa ona može da proda ono čega nema) i dugme „Otvori kanale”. Lista kanala se osvežava na 5 minuta (SPEC 11.1: u v1 umesto SignalR-a). Ovo je obaveštenje vlasnika iz SPEC 7.6; email dolazi sa upozorenjima (SPEC 2.5), kad se pravi slanje emailova.

**`pages/channels/ChannelsPage.tsx`**: „Kanali prodaje” (`/channels`). Bez povezane prodavnice prikazuje formu (adresa, consumer key, consumer secret) i uputstvo gde se ključevi prave u WooCommerce-u. Sa prodavnicom prikazuje karticu: adresu, status (tačka i tekst, nikad samo boja), prevedeni razlog greške, od kada veza ne radi i dugmad „Proveri vezu” i „Promeni ključeve”. Rezultat provere se upiše direktno u keš liste (`setQueryData`), bez novog zahteva. `KeyFields.tsx` su polja za ključeve, zajednička za formu i `ReplaceKeysModal.tsx`, i skidaju razmake koji se često nalepe pri kopiranju.

**`pages/orders/OrdersPage.tsx`**: „Porudžbine” (`/orders`): naslov sa brojem i napomenom da se podaci kupaca ne preuzimaju, pretraga, pilule „Sve” i „Nepovezani proizvodi” (`?unlinked=1`), izbor statusa (`?status=`), tabela (broj, datum, status i da li skida sa stanja, prva dva proizvoda sa količinom, „još N” i broj nepovezanih) i paginacija. Adresa prodavnice se prikazuje samo kad su na strani porudžbine više prodavnica. Klik na red otvara `OrderModal.tsx`: proizvod u prodavnici, artikal u Zalihama (link) ili „Nije povezan”, naručeno i skinuto sa stanja, i objašnjenje kad porudžbina ne skida robu (status, nepovezan proizvod sa dugmetom „Poveži proizvode” na uvoz iz prodavnice, ili proizvod povezan posle porudžbine). Prozor koristi podatke iz reda liste, bez posebnog zahteva. `orderText.ts`: prevod statusa (nepoznat status iz dodatka prodavnice se prikazuje kao kod), naziv stavke i adresa bez `https://`.

**`pages/channels/ProductImportPage.tsx`**: „Uvoz proizvoda iz prodavnice” (`/channels/{id}/import`, dugme „Uvezi proizvode” na kartici kanala). Traka sa četiri brojke (novi, postoje u Zalihama, već povezano, preskočeno), tabela novih proizvoda sa izborom (svi su izabrani, korisnik isključuje ono što ne želi; zaglavlje ima „izaberi sve”), tabela povezivanja (link na postojeći artikal) i preskočeni sa prevedenim razlogom. Pregled se ne učitava ponovo pri povratku na prozor (`staleTime: Infinity`, `refetchOnWindowFocus: false`), jer je čitanje prodavnice sporo i opterećuje je. Posle uvoza se osvežavaju artikli, brojke i Početna, a pregled se briše iz keša.

**Frontend testovi** (`*.test.ts(x)`, Vitest + React Testing Library):
- `locales.test.ts`: oba jezika imaju isti skup ključeva i nijedan prevod nije prazan;
- `format.test.ts`: formatiranje i unos brojeva;
- `errors.test.ts`: prevođenje kodova i parametara;
- `LoginPage.test.tsx`, `RegisterPage.test.tsx`: forma pošalje prave podatke i prikaže greške API-ja;
- `RouteGuards.test.tsx`: neprijavljeni idu na prijavu, prijavljeni vide aplikaciju;
- `MovementModal.test.tsx`, `ItemDetailPage.test.tsx`: unos kretanja (decimale, upozorenje za minus, razlika kod korekcije, greške), prikaz stanja i istorije;
- `HomePage.test.tsx`: brojke, artikli za naručivanje, poslednja kretanja i prazna Početna;
- `ImportPage.test.tsx`: ceo tok uvoza (predložene kolone, poslata polja, prevedeni problemi, uvoz), Excel fajl, onemogućena provera bez obaveznih kolona;
- `ProductImportPage.test.tsx`: brojke, novi, povezivanje i preskočeni sa razlogom; isključen proizvod se ne šalje; „izaberi sve”; greška prodavnice sa „Pokušaj ponovo”; sve već povezano;
- `OrdersPage.test.tsx`: statusi, proizvodi i „još N”, broj nepovezanih, filter i pretraga idu na server, prozor sa stavkama i linkom na povezivanje, objašnjenje za proizvod povezan posle porudžbine;
- `ChannelsPage.test.tsx`: povezivanje (poslata polja, kartica posle uspeha), prevedena greška prodavnice i greška ispod polja za adresu, provera veze koja otkrije opozvane ključeve, zamena ključeva;
- `ThemeToggle.test.tsx`: tamna tema na početku, klik prebacuje na svetlu i pamti izbor;
- `ItemsPage.test.tsx`, `ItemModal.test.tsx`: izmena (popunjena polja, `PUT`), deaktivacija, oznaka i filter neaktivnih, redovi i paginacija, prazno stanje, pretraga ide na server, decimale sa zarezom se šalju kao brojevi, greška „šifra već postoji” stoji ispod polja.

Testovi lažiraju `fetch` (`mockFetch` odgovara redom poziva, a `mockApi` prema adresi, kad ekran šalje više zahteva odjednom; oba su u `test/render.tsx`), pa ne zahtevaju pokrenut backend, i renderuju sa pravim providerima (`renderRoutes`).

### Testovi

**`Zalihe.Domain.Tests/Tenants/TenantTests.cs`**: unit testovi pravila entiteta, bez baze, za milisekunde. `[Fact]` je jedan slučaj, a `[Theory]` sa `[InlineData]` je isti test sa više ulaza (prazan string, samo razmaci).

**`Zalihe.IntegrationTests/Infrastructure/ZaliheApiFactory.cs`**: pokreće **ceo API u memoriji** (`WebApplicationFactory<Program>`), povezan sa **pravim Postgres-om u Dockeru** (Testcontainers).
- `InitializeAsync()`: podigne kontejner i primeni migracije.
- `ConfigureWebHost()`: podmetne connection string test baze i lažnu WooCommerce prodavnicu (`Shop`).
- `DisposeAsync()`: ugasi i obriše kontejner.
- `ApiCollection` (`ICollectionFixture`): **svi testovi dele jedan kontejner**, pa se ne podiže baza za svaki test. Svaki test koristi jedinstven email (`UniqueEmail()`), pa se međusobno ne ometaju.

**`Infrastructure/HttpClientExtensions.cs`**: pomoćne metode da testovi budu kratki i čitljivi:
- `RegisterAsync()`: registruje i prijavi korisnika.
- `GetAntiforgeryTokenAsync()`: pozove `me` i izvuče token iz cookie-ja, kao što radi frontend.
- `ReadProblemAsync()`: proveri da je odgovor `problem+json` i pročita ga.

**`Auth/AuthTests.cs`**: 11 testova koji pokrivaju registraciju (uspeh, podrazumevani jezik, zauzet email, kratka lozinka, prazna polja, nepodržan jezik), prijavu (uspeh, pogrešna lozinka), 401 umesto preusmeravanja i odjavu sa tokenom i bez njega. Svaki test prati **Arrange / Act / Assert** i naziv `Metoda_Situacija_OcekivaniRezultat`, pa se iz samog naziva vidi šta se pokvarilo kad test padne.

---

**`Zalihe.Domain.Tests/Items/ItemTests.cs`**: pravila artikla (skraćivanje teksta, obavezna polja, negativne vrednosti, broj decimala).

**`Zalihe.IntegrationTests/Items/ItemsTests.cs`**: pravljenje, lista, pretraga, paginacija i greške, a pre svega **izolacija firmi**: ista šifra je dozvoljena u različitim firmama, lista, pretraga i kategorije vide samo svoju firmu, a tuđi artikal vraća 404. Pomoćna metoda `CreateSignedInClientAsync()` pravi prijavljenog klijenta koji šalje antiforgery header kao frontend.

**`Zalihe.IntegrationTests/Items/ItemUpdateTests.cs`**: izmena (sopstvena šifra je dozvoljena, tuđa nije), deaktivacija i aktivacija, filter neaktivnih, i izolacija: firma B ne može da izmeni ni deaktivira artikal firme A.

**`Zalihe.IntegrationTests/Items/ItemContractsTests.cs`**: test koji reprodukuje grešku sa `[Range]` i srpskim podešavanjima. Atribut čita direktno, jer test server ne prenosi jezička podešavanja u obradu zahteva, pa bi test preko HTTP-a prolazio i sa greškom.

**`Zalihe.Domain.Tests/Stock/`**: predznaci kretanja, korekcija na prebrojano, stanje kao zbir kretanja i pravila statusa.

**`Zalihe.IntegrationTests/Stock/StockTests.cs`**: prijem, prodaja u minus, korekcija, greške, 10 istovremenih prijema, stanje jednako zbiru kretanja u bazi, istorija, filter po statusu, prodaja 30 dana i vrednost, i izolacija: firma B ne može da upiše kretanje ni vidi istoriju artikla firme A.

**`Zalihe.IntegrationTests/Items/ItemHistoryTests.cs`**: zapis „napravljen” sa korisnikom, izmena sa starim i novim vrednostima, nema zapisa kad se ništa ne promeni, deaktivacija i aktivacija, spajanje i filteri istorije, izolacija firmi.

**`Zalihe.Application.Tests/Imports/`**: prvi testovi u ovom projektu, bez baze: čitanje CSV-a (`;`, zarez, Windows-1250, BOM, navodnici, prazni redovi), prepoznavanje kolona, jedinice, brojevi (uključujući „2.000” u oba formata) i provera reda.

**`Zalihe.IntegrationTests/Imports/ItemImportTests.cs`**: analiza, pregled, uvoz sa početnim stanjem, preskakanje postojećih šifri, Windows-1250 fajl, loši fajlovi, antiforgery i izolacija: šifra druge firme se ne računa kao postojeća.

**`Zalihe.IntegrationTests/Dashboard/DashboardTests.cs`**: vrednost i brojke (neaktivni se ne računaju), redosled hitnosti, poslednja kretanja, prazna firma i izolacija firmi.

**`Zalihe.IntegrationTests/Tenancy/TenantFilterTests.cs`**: svaki entitet sa kolonom `TenantId` mora da implementira `ITenantOwned` i da ima filter, a upit bez postavljene firme mora da baci izuzetak.

**`Zalihe.Domain.Tests/Channels/SalesChannelTests.cs`**: normalizacija adrese prodavnice i prelazi statusa.

**`Zalihe.Domain.Tests/Channels/ItemChannelMappingTests.cs`** i **`Zalihe.Application.Tests/Channels/ProductImportRulesTests.cs`**: veza artikla i proizvoda, i razvrstavanje proizvoda (šifra postoji, nova, već povezan, bez šifre, predugačka, ista šifra dvaput, artikal već povezan sa drugim proizvodom).

**`Zalihe.IntegrationTests/Channels/ProductImportTests.cs`**: lažna prodavnica odgovara po adresi (proizvodi, varijacije, strane). Pregled razvrstava proizvode i ništa ne upisuje; uvoz pravi izabrane artikle sa stanjem iz prodavnice i povezuje postojeće bez izmene; ponovni pregled i ponovni uvoz ne dupliraju ništa; čitanje više strana; greška prodavnice ne upisuje ništa; izolacija firmi (šifra druge firme se ne računa, tuđi kanal je 404); varijacija čuva ID roditelja. Test sa `stock_quantity: null` je otkrio grešku pre prvog pokretanja na pravoj prodavnici.

**`Zalihe.Domain.Tests/Orders/ExternalOrderTests.cs`**: svih sedam slučajeva iz SPEC 6 (nova porudžbina, duplirani događaj, otkazivanje, ponovno aktiviranje, izmena količine, uklonjena stavka, događaji van redosleda), plus status koji još ne skida robu, ista šifra u dva reda, nepovezan proizvod, proizvod povezan kasnije i vraćanje robe istom artiklu posle promene veze.

**`Zalihe.IntegrationTests/Orders/OrderProcessingTests.cs`**: `OrderService` protiv prave baze, pozvan kao što će ga zvati pozadinski posao (sopstveni scope, firma postavljena eksplicitno). Prodaja i stanje, duplirani događaj, otkazivanje sa povratom, dodata stavka i izmenjena količina, stariji događaj, nepovezana stavka, porudžbina napravljena pre povezivanja ne skida robu ni kad se kasnije promeni, stanje jednako zbiru kretanja posle niza događaja, pet istovremenih događaja za istu porudžbinu, pet istovremenih događaja za novu porudžbinu (roba skinuta jednom, bez greške) i izolacija: kanal druge firme se odbija. Prodavnicu sa dva povezana proizvoda pravi `Infrastructure/LinkedShop.cs`, zajednički za ove testove i testove webhookova.

**`Zalihe.IntegrationTests/Orders/OrderSyncTests.cs`**: usklađivanje protiv prave baze i lažne prodavnice. Propuštena porudžbina skida robu i pamti vreme usklađivanja, upit traži samo potrebna polja i kreće od povezivanja, porudžbina koju je webhook već primenio ne skida robu ponovo, propušteno otkazivanje vraća robu, drugo usklađivanje kreće od početka prvog minus preklapanje, odbijeni ključevi bacaju grešku i ne pomeraju vreme, prodavnica druge firme se ne dira (izolacija). Zakazivanje za prodavnice više firmi, sa lažnim `IBackgroundJobClient` (NSubstitute), da posao ne bi stvarno krenuo. Brisanje događaja starijih od 30 dana za sve firme.

**`Zalihe.IntegrationTests/Orders/OrdersTests.cs`**: lista porudžbina: najnovije prvo, stavke sa artiklom, jedinicom, naručenim i skinutim, nepovezana stavka, odgovor bez ijednog polja o kupcu; uklonjena stavka se ne prikazuje; porudžbina pre povezivanja nije „nepovezana”; filteri, pretraga (broj, `#broj`, naziv bez obzira na velika slova), paginacija i previše velika strana (400); tuđe porudžbine se ne vide (izolacija); neprijavljen korisnik dobija 401; usklađivanje sačuva vreme naručivanja i nazive proizvoda.

**`Zalihe.IntegrationTests/Channels/StockPushTests.cs`**: kretanje i porudžbina označe samo svoje proizvode; slanje ide na pravu adresu za prost proizvod i za varijaciju, sa `manage_stock` i celim stanjem, i skida oznake; stanje vraćeno na poslato se ne šalje; odbijen proizvod ostaje označen; nedostupna prodavnica baca grešku i ostavlja oznake; prodaja zabeležena **dok prodavnica odgovara** ostaje označena; prodavnica druge firme se ne dira. U `ProductImportTests`: pri uvozu se označava samo postojeći artikal, ne novi. **`ItemChannelMappingTests`**: zaokruživanje naniže, i za negativno stanje.

**`Zalihe.IntegrationTests/Webhooks/WebhooksTests.cs`**: ceo put webhooka, sa pravim Hangfire serverom u test hostu (test čeka dok se događaj ne obradi): stanje pada u pozadini, u bazi nema podataka kupca, otkazivanje vraća robu, ista isporuka dvaput skida robu jednom, pogrešan ili nedostajući potpis i telo izmenjeno posle potpisivanja daju 401, tajna druge firme daje 401 (izolacija), ping daje 200 i za nepoznat kanal, nepoznat kanal 404, druge teme i nepotpune porudžbine 200 bez upisa.

**`Zalihe.Application.Tests/Webhooks/WebhookSignatureTests.cs`**: provera potpisa protiv vrednosti izračunate nezavisno (`openssl dgst -sha256 -hmac`), izmenjeno telo, druga tajna, loš base64. **`Zalihe.Domain.Tests/Channels/WebhookEventTests.cs`**: obrada briše grešku, predugačka greška se skraćuje.

**`Zalihe.IntegrationTests/Channels/ChannelsTests.cs`**: povezivanje (Basic autentifikacija sa pravim ključevima), ključevi u bazi nisu čitljivi, svaki odgovor prodavnice daje svoj kod i ništa se ne čuva, loš unos se odbija pre poziva prodavnice, druga prodavnica, opozvani ključevi pa zamena, i izolacija firmi. **Prava prodavnica se nikad ne poziva**: `Infrastructure/FakeShopHandler.cs` zamenjuje mrežu za HTTP klijent „WooCommerce”, a svaki test određuje kako lažna prodavnica odgovara (`RespondWith`) i proverava šta joj je poslato (`Requests`, a tela u `Bodies`, pročitana pri slanju, jer klijent posle zatvori zahtev). Registracija webhookova: dva webhooka sa adresom i tajnom kanala, postojeći se ažuriraju umesto da se dupliraju (tuđi se ne diraju), a ključevi samo za čitanje daju `channel.unauthorized` i ništa se ne čuva.

## 5. Jedan zahtev kroz ceo sistem: registracija

```
Browser: POST /api/auth/register {companyName, email, password}
  │
  ▼  Program.cs pipeline
UseExceptionHandler → UseAuthentication (nema cookie-ja, anoniman) → UseAuthorization
  │
  ▼  MVC
[ApiController] deserializuje JSON u RegisterRequest i proveri [Required] i [EmailAddress]
  │   └─ pad validacije → ApiProblems.FromInvalidModelState → 400 {code: "validation.failed", errors: [...]}
  ▼
CookieAntiforgeryFilter: anoniman zahtev, pa bez provere
  │
  ▼
AuthController.Register → AccountService.RegisterAsync
  │   BEGIN TRANSACTION
  │   INSERT Tenants
  │   UserManager.CreateAsync → hash lozinke, INSERT AspNetUsers
  │   └─ greška → MapIdentityError → transakcija se poništava → 400 sa kodovima
  │   COMMIT
  ▼
SignInManager.SignInAsync → Set-Cookie: zalihe.auth=...
  │
  ▼
204 No Content
```

### Povezivanje prodavnice: zahtev koji izlazi iz aplikacije

Za razliku od registracije, ovde aplikacija sama zove spoljni sistem (prodavnicu), i to **pre** nego što išta sačuva.

```
Browser: POST /api/channels/woocommerce {baseUrl, consumerKey, consumerSecret}
  │   (cookie + X-XSRF-TOKEN header, jer je zahtev autentifikovan cookie-jem)
  ▼
TenantMiddleware: tenant_id iz cookie-ja → TenantContext
  │
  ▼
ChannelsController.ConnectWooCommerce → ChannelService.ConnectWooCommerceAsync
  │   SalesChannel.NormalizeBaseUrl("zrno.rs/") → "https://zrno.rs"   └─ null → 400 channel.url_invalid
  │   ključevi počinju sa ck_ / cs_?                                     └─ ne → 400 channel.keys_invalid
  │   firma već ima WooCommerce? (global filter: samo svoja firma)      └─ da → 400 channel.already_connected
  ▼
ISalesChannelFactory.Create(...) → WooCommerceClient.CheckConnectionAsync
  │   GET https://zrno.rs/wp-json/wc/v3/products?per_page=1   (Basic ck:cs, 20 s timeout)
  │   └─ mreža / 401-403 / 404 / ne-JSON → SalesChannelException(kod) → 400 sa tim kodom, ništa nije sačuvano
  ▼
ICredentialProtector.Protect(JSON ključeva)   (Data Protection, namena Zalihe.SalesChannels.Secrets.v1)
nasumična tajna za webhookove → Protect
  │
  ▼
INSERT SalesChannels (EncryptedCredentials, EncryptedWebhookSecret, Status = Connected)
  │   EnsureNewDataBelongsToCurrentTenant: TenantId mora biti trenutna firma
  ▼
201 Created {id, type, baseUrl, status, ...}   ← bez ključeva
```

Zašto ovakav redosled:
- **Provera pre čuvanja**: korisnik odmah saznaje da ključevi ne rade, a u bazi nikad nema kanala koji nije radio ni jednom.
- **Kodovi umesto poruka**: `channel.unauthorized` frontend prevodi u rečenicu koja kaže šta da proveri. Ista greška kasnije stiže i iz pozadinskih poslova (`LastErrorCode`), pa se prikazuje istim prevodom.
- **Application ne zna za HTTP ni za šifrovanje**: zna samo za `ISalesChannelFactory` i `ICredentialProtector`. U testovima se zato menja samo mreža (`FakeShopHandler`), a sve ostalo radi kao u produkciji.

---

### Webhook: od porudžbine u prodavnici do stanja

```
Kupac naruči → WooCommerce: POST {Webhooks:BaseUrl}/webhooks/woo/{channelId}
  │   X-WC-Webhook-Topic: order.created
  │   X-WC-Webhook-Signature: base64(HMAC-SHA256(telo, tajna kanala))
  ▼
WooCommerceWebhooksController   (bez prijave i antiforgery tokena)
  │   ping? → 200
  │   FindChannelTenantAsync(channelId) → TenantContext.Set(firma)    └─ nema kanala → 404
  ▼
WebhookService.ReceiveAsync
  │   potpis ispravan?                                                └─ ne → 401
  │   order.created / order.updated i telo je porudžbina?            └─ ne → 200, ništa se ne čuva
  │   INSERT WebhookEvents (samo broj, status, vreme i stavke)
  │   IBackgroundJobs.ProcessWebhookEvent(firma, događaj)  → red u šemi hangfire + NOTIFY
  ▼
200 OK   (prodavnica dobija odgovor odmah)

… u pozadini, Hangfire server u istom procesu:
WebhookEventJob.RunAsync(firma, događaj)
  │   TenantContext.Set(firma)
  ▼
WebhookService.ProcessAsync → OrderService.ProcessAsync   (vidi „Porudžbine: primenjuje se samo razlika”)
  │   └─ izuzetak → razlog na događaj, Hangfire pokuša ponovo kasnije
  ▼
WebhookEvent.ProcessedAt = sada
```

Ako webhook ne stigne (server ne radi, prodavnica isključi webhook), porudžbinu najkasnije za 15 minuta pokupi usklađivanje: `OrderSyncJob` → `OrderSyncService` → isti `OrderService.ProcessAsync`.

## 6. Kako se dodaje nova funkcionalnost

Redosled koji važi za svaku funkcionalnost (npr. artikle):

1. **Domain**: entitet sa privatnim setterima i konstruktorom koji čuva pravila, plus unit testovi za ta pravila.
2. **Infrastructure**: `DbSet` i konfiguracija u `AppDbContext`, zatim `dotnet ef migrations add ...`. Entitet sa podacima firme samo implementira `ITenantOwned`, a filter se dodaje sam. `TenantFilterTests` potvrđuje da je to urađeno. U `IAppDbContext` dodati `DbSet` koji servis koristi.
3. **Application**: servis sa metodama slučajeva korišćenja, a greške kao `AppError` sa kodom.
4. **Web**: DTO zahtevi i odgovori sa validacijom preko kodova, zatim tanak kontroler sa `[ProducesResponseType]` za svaki odgovor. Liste uvek imaju paginaciju, pretragu i filtriranje na serveru.
5. **Integracioni testovi**: srećan put, greške i izolacija firmi (firma A ne vidi podatke firme B).
6. **Frontend**: pokrenuti API i `npm run api` (regeneriše klijent), dodati stranicu u `router.tsx` i stavku u `navItems`, prevode u oba JSON fajla (`sr-Latn` i `en`), a za forme i formatiranje testove. Pre rada na UI-ju pročitati DESIGN.md i uporediti ekran sa maketom.
7. Pokrenuti `dotnet test` i frontend testove, pa commitovati.
