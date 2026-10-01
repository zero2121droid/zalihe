# Arhitektura i način rada

Ovaj dokument objašnjava kako je projekat organizovan, zašto postoji svaki fajl i kako se dodaje nova funkcionalnost. Opisuje stanje posle koraka „Početna strana” i dopunjuje se posle svakog većeg koraka.

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
| **Infrastructure** | EF Core, Postgres, Identity, kasnije WooCommerce klijent, email i Hangfire | Sve što priča sa spoljnim svetom. Promena baze ili email servisa dira samo ovaj sloj. |
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
- `volumes`: imenovani volumeni čuvaju podatke kad se kontejneri ugase. `docker compose down` ih ne briše, `docker compose down -v` ih briše.

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

### Zalihe.Infrastructure

**`Identity/User.cs`**: korisnik. Nasleđuje `IdentityUser<Guid>`, pa dobija email, hash lozinke, zaključavanje i ostalo što Identity nudi, i dodaje `TenantId` (kojoj firmi pripada) i `Language`. `<Guid>` znači da je ključ GUID, a ne string.

**`Persistence/AppDbContext.cs`**: veza sa bazom.
- Nasleđuje `IdentityDbContext`, pa automatski dobija Identity tabele (`AspNetUsers` i ostale).
- `DbSet<Tenant> Tenants`: tabela firmi.
- `OnModelCreating`: opisuje šemu: dužine kolona, strani ključ korisnik → firma i indeks na `TenantId`. `OnDelete(Restrict)` sprečava brisanje firme koja ima korisnike.
- Ovde dolaze **global query filteri** za `TenantId`, sa prvim entitetom koji sadrži podatke firme.

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

**Frontend testovi** (`*.test.ts(x)`, Vitest + React Testing Library):
- `locales.test.ts`: oba jezika imaju isti skup ključeva i nijedan prevod nije prazan;
- `format.test.ts`: formatiranje i unos brojeva;
- `errors.test.ts`: prevođenje kodova i parametara;
- `LoginPage.test.tsx`, `RegisterPage.test.tsx`: forma pošalje prave podatke i prikaže greške API-ja;
- `RouteGuards.test.tsx`: neprijavljeni idu na prijavu, prijavljeni vide aplikaciju;
- `MovementModal.test.tsx`, `ItemDetailPage.test.tsx`: unos kretanja (decimale, upozorenje za minus, razlika kod korekcije, greške), prikaz stanja i istorije;
- `HomePage.test.tsx`: brojke, artikli za naručivanje, poslednja kretanja i prazna Početna;
- `ImportPage.test.tsx`: ceo tok uvoza (predložene kolone, poslata polja, prevedeni problemi, uvoz), Excel fajl, onemogućena provera bez obaveznih kolona;
- `ThemeToggle.test.tsx`: tamna tema na početku, klik prebacuje na svetlu i pamti izbor;
- `ItemsPage.test.tsx`, `ItemModal.test.tsx`: izmena (popunjena polja, `PUT`), deaktivacija, oznaka i filter neaktivnih, redovi i paginacija, prazno stanje, pretraga ide na server, decimale sa zarezom se šalju kao brojevi, greška „šifra već postoji” stoji ispod polja.

Testovi lažiraju `fetch` (`mockFetch` odgovara redom poziva, a `mockApi` prema adresi, kad ekran šalje više zahteva odjednom; oba su u `test/render.tsx`), pa ne zahtevaju pokrenut backend, i renderuju sa pravim providerima (`renderRoutes`).

### Testovi

**`Zalihe.Domain.Tests/Tenants/TenantTests.cs`**: unit testovi pravila entiteta, bez baze, za milisekunde. `[Fact]` je jedan slučaj, a `[Theory]` sa `[InlineData]` je isti test sa više ulaza (prazan string, samo razmaci).

**`Zalihe.IntegrationTests/Infrastructure/ZaliheApiFactory.cs`**: pokreće **ceo API u memoriji** (`WebApplicationFactory<Program>`), povezan sa **pravim Postgres-om u Dockeru** (Testcontainers).
- `InitializeAsync()`: podigne kontejner i primeni migracije.
- `ConfigureWebHost()`: podmetne connection string test baze.
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

---

## 6. Kako se dodaje nova funkcionalnost

Redosled koji važi za svaku funkcionalnost (npr. artikle):

1. **Domain**: entitet sa privatnim setterima i konstruktorom koji čuva pravila, plus unit testovi za ta pravila.
2. **Infrastructure**: `DbSet` i konfiguracija u `AppDbContext`, zatim `dotnet ef migrations add ...`. Entitet sa podacima firme samo implementira `ITenantOwned`, a filter se dodaje sam. `TenantFilterTests` potvrđuje da je to urađeno. U `IAppDbContext` dodati `DbSet` koji servis koristi.
3. **Application**: servis sa metodama slučajeva korišćenja, a greške kao `AppError` sa kodom.
4. **Web**: DTO zahtevi i odgovori sa validacijom preko kodova, zatim tanak kontroler sa `[ProducesResponseType]` za svaki odgovor. Liste uvek imaju paginaciju, pretragu i filtriranje na serveru.
5. **Integracioni testovi**: srećan put, greške i izolacija firmi (firma A ne vidi podatke firme B).
6. **Frontend**: pokrenuti API i `npm run api` (regeneriše klijent), dodati stranicu u `router.tsx` i stavku u `navItems`, prevode u oba JSON fajla (`sr-Latn` i `en`), a za forme i formatiranje testove. Pre rada na UI-ju pročitati DESIGN.md i uporediti ekran sa maketom.
7. Pokrenuti `dotnet test` i frontend testove, pa commitovati.
