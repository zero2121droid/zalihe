# Arhitektura i način rada

Ovaj dokument objašnjava kako je projekat organizovan, zašto postoji svaki fajl i kako se dodaje nova funkcionalnost. Opisuje stanje posle koraka „cookie autentifikacija sa registracijom firme” i dopunjuje se posle svakog većeg koraka.

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

**`Users/Languages.cs`**: podržani jezici (`sr-Latn`, `en`), podrazumevani jezik i `IsSupported()` za proveru. Ovo je domensko pravilo, pa je ovde, a ne u Web-u.

### Zalihe.Application

**`Common/AppError.cs`**: jedna greška koja ide klijentu: `Code` (npr. `auth.email_taken`), `Field` (koje polje forme) i `Params` (npr. `{min: 8}`). Na ovom tipu počiva cela i18n priča: API šalje kod, a frontend ga prevodi preko `errors.auth.email_taken` i ubacuje parametre u rečenicu „Lozinka mora imati najmanje {{min}} znakova”. Ista greška radi na oba jezika i u mobilnoj aplikaciji.

### Zalihe.Infrastructure

**`Identity/User.cs`**: korisnik. Nasleđuje `IdentityUser<Guid>`, pa dobija email, hash lozinke, zaključavanje i ostalo što Identity nudi, i dodaje `TenantId` (kojoj firmi pripada) i `Language`. `<Guid>` znači da je ključ GUID, a ne string.

**`Persistence/AppDbContext.cs`**: veza sa bazom.
- Nasleđuje `IdentityDbContext`, pa automatski dobija Identity tabele (`AspNetUsers` i ostale).
- `DbSet<Tenant> Tenants`: tabela firmi.
- `OnModelCreating`: opisuje šemu: dužine kolona, strani ključ korisnik → firma i indeks na `TenantId`. `OnDelete(Restrict)` sprečava brisanje firme koja ima korisnike.
- Ovde dolaze **global query filteri** za `TenantId`, sa prvim entitetom koji sadrži podatke firme.

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

**`Controllers/HealthController.cs`**: `GET /api/health`. Brza provera da API radi, a kasnije i za monitoring u produkciji.

**`appsettings.Development.json`**: connection string za lokalni Docker Postgres. Učitava se samo u Development okruženju.

**`Properties/launchSettings.json`**: profili za `dotnet run`. Profil `http` pokreće API na portu 5131, i na njega pokazuje Vite proxy.

**`Zalihe.Web.http`**: fajl za ručno slanje zahteva iz VS Code-a („Send Request”).

### src/web (React)

Za sada je ovo Vite šablon, a jedina izmena je `vite.config.ts`: **proxy** `/api` → `http://localhost:5131`. U razvoju browser priča samo sa Vite-om (5173), a Vite prosleđuje API pozive backendu. Browser zato vidi jednu adresu, cookie radi bez CORS podešavanja, a produkcija izgleda isto (ASP.NET servira i React i API sa iste adrese).

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
2. **Infrastructure**: `DbSet` i konfiguracija u `AppDbContext` (za podatke firme i global query filter po `TenantId`), zatim `dotnet ef migrations add ...`. Posle migracije proveriti da filteri važe za nove entitete.
3. **Application**: servis sa metodama slučajeva korišćenja, a greške kao `AppError` sa kodom.
4. **Web**: DTO zahtevi i odgovori sa validacijom preko kodova, zatim tanak kontroler sa `[ProducesResponseType]` za svaki odgovor. Liste uvek imaju paginaciju, pretragu i filtriranje na serveru.
5. **Integracioni testovi**: srećan put, greške i izolacija firmi (firma A ne vidi podatke firme B).
6. **Frontend**: regenerisati Orval klijent, napraviti ekran i dodati prevode u oba JSON fajla (`sr-Latn` i `en`).
7. Pokrenuti `dotnet test` i frontend testove, pa commitovati.
