# CLAUDE.md

Radni naziv projekta: **Zalihe** (menja se kasnije).

SaaS aplikacija za praćenje zaliha za male firme, sa prvim fokusom na WooCommerce webshopove u Srbiji i regionu. Autor radi sam, 10-20h nedeljno. Kompletna specifikacija je u `docs/SPEC.md` i treba je pročitati pre većih izmena.

## Stack

- Backend: ASP.NET Core (.NET 10), PostgreSQL, EF Core, Hangfire (Postgres storage)
- Frontend: React + TypeScript + Vite, React Router, TanStack Query, Mantine
- Autentifikacija: ASP.NET Core Identity, cookie (frontend i API na istoj adresi, bez JWT-a)
- API klijent za frontend se generiše iz OpenAPI specifikacije, ne piše se ručno
- Lokalni razvoj: Docker Compose (Postgres, WordPress + WooCommerce za testiranje integracije)

## Struktura

```
src/
  Zalihe.Domain/          entiteti i domenska pravila, bez zavisnosti
  Zalihe.Application/     servisi i slučajevi korišćenja, interfejsi (npr. ISalesChannel)
  Zalihe.Infrastructure/  EF Core, WooCommerce klijent, email, Hangfire poslovi
  Zalihe.Web/             API kontroleri, webhook endpointi, servira React build
  web/                    React aplikacija (Vite)
tests/
  Zalihe.Domain.Tests/
  Zalihe.Application.Tests/
  Zalihe.IntegrationTests/
docs/
  SPEC.md
  DESIGN.md
  ARCHITECTURE.md
  mockups/
```

`docs/ARCHITECTURE.md` objašnjava slojeve, fajlove i redosled dodavanja funkcionalnosti. Dopunjuje se kad se struktura promeni.

## Konvencije

- Kod, identifikatori, komentari i commit poruke na engleskom. UI je višejezičan (vidi "Prevodi").
- Clean Architecture "light": bez MediatR-a, CQRS-a i event sourcinga. Obični servisi sa jasnim metodama.
- Svaka tabela sa podacima firme ima `TenantId`. Filtriranje ide preko EF Core global query filtera, nikad ručno u upitima.
- Stanje zaliha se nikad ne menja direktno. Svaka promena je novi `StockMovement`, a `StockLevel` se ažurira u istoj transakciji.
- `StockMovement` se nikad ne menja ni ne briše. Greška se ispravlja novim kretanjem (korekcijom).
- Količine: `decimal(18,3)`. Novac: `decimal(18,2)`. Vreme: `DateTimeOffset` u UTC.
- Nikad ne čuvati lične podatke kupaca iz porudžbina (ime, adresa, telefon, email).
- API ključevi kanala se čuvaju šifrovani (ASP.NET Data Protection).

## API spreman za mobilnu aplikaciju

Posle v1 planirana je React Native (Expo) aplikacija koja koristi isti API. Zato od početka:

- Sva poslovna logika je u API-ju. React komponente samo prikazuju i šalju podatke.
- API ne zavisi od serverske sesije niti od cookie-ja, osim za samu autentifikaciju.
- Autentifikacija se konfiguriše tako da kasnije može da prihvati i cookie (web) i bearer token (mobilna) preko `IdentityConstants.BearerAndApplicationScheme`. U v1 se koristi samo cookie.
- Antiforgery zaštita važi samo za zahteve autentifikovane cookie-jem, ne za bearer token.
- Liste uvek imaju paginaciju, pretragu i filtriranje na serveru.
- Svi endpointi su opisani u OpenAPI specifikaciji (tipovi odgovora, statusni kodovi), jer se isti generisani klijent koristi i u mobilnoj aplikaciji.
- Greške se vraćaju u jedinstvenom formatu (`ProblemDetails`).

## Prevodi (i18n)

- Jezici: srpski latinica (`sr-Latn`, glavni i podrazumevani) i engleski (`en`).
- Frontend: `react-i18next`. Tekstovi su u `web/src/locales/sr-Latn.json` i `web/src/locales/en.json`. Nikad ne pisati tekst direktno u komponenti.
- Ključevi su grupisani po oblasti: `common.*`, `items.*`, `stock.*`, `channels.*`, `errors.*`.
- Svaki novi ključ se dodaje u oba fajla u istom zadatku. Test proverava da oba fajla imaju isti skup ključeva.
- API ne vraća prevedene poruke, već kodove grešaka sa parametrima (npr. `item.sku_duplicate`) u `ProblemDetails`. Frontend ih prevodi preko `errors.*` ključeva.
- Emailovi se prevode na backendu (`IStringLocalizer`, resx fajlovi) prema jeziku korisnika.
- Brojevi, datumi i novac se formatiraju preko `Intl` API-ja prema izabranom jeziku. Unos decimalnih brojeva mora prihvatiti zarez kao decimalni separator (`sr-Latn`).
- Jezik korisnika se čuva na korisniku (`User.Language`), a menja se u podešavanjima.

## Testovi

- Backend: xUnit, NSubstitute za lažne zavisnosti, Shouldly za čitljive provere.
- Unit testovi (`Zalihe.Domain.Tests`, `Zalihe.Application.Tests`): brzi, bez baze i mreže.
- Integracioni testovi (`Zalihe.IntegrationTests`): prava PostgreSQL baza preko Testcontainers i `WebApplicationFactory`. Obavezno pokrivaju izolaciju firmi (firma A ne vidi podatke firme B) i webhook endpoint.
- WooCommerce API se u testovima nikad ne poziva stvarno, već se koristi lažni `ISalesChannel` ili lažni HTTP handler.
- Frontend: Vitest + React Testing Library, samo za komponente sa logikom (forme, validacija, formatiranje brojeva).
- Struktura testa: Arrange / Act / Assert. Naziv: `Metoda_Situacija_OcekivaniRezultat`.
- Svaka ispravljena greška dobija test koji je reprodukuje.
- Pre završetka zadatka pokrenuti `dotnet test` i frontend testove. Zadatak nije gotov dok testovi ne prolaze.

## Dizajn

- Pre svakog rada na UI-ju pročitaj `docs/DESIGN.md`. Boje, fontovi, razmaci i zabrane iz njega su obavezni.
- Boje i fontovi idu isključivo preko Mantine teme (`web/src/theme.ts`), nikad hex vrednosti u komponentama.
- Makete su u `docs/mockups/`: PNG slike za izgled i HTML izvor za tačne boje, razmake i veličine. HTML je samo referenca, ne kopira se u projekat; ekrani se prave kao React komponente sa Mantine-om.

## Pravila rada

- Radi u malim koracima: jedna funkcionalnost po zadatku, od baze do ekrana.
- Ne dodavati funkcionalnosti koje su u `docs/SPEC.md` navedene kao van obima v1, čak ni "usput".
- Pre dodavanja nove NuGet ili npm zavisnosti, pitaj.
- Domenska logika (obračun stanja, obrada porudžbina) mora imati unit testove.
- Posle izmena API-ja regeneriši TypeScript klijent.
- Posle svake migracije proveri da global query filteri i dalje važe za nove entitete.

## Komande

```bash
docker compose up -d --wait                                # Postgres (localhost:5433), WordPress (localhost:8080)
dotnet run --project src/Zalihe.Web --launch-profile http  # API na http://localhost:5131
cd src/web && npm run dev                                  # React na http://localhost:5173, /api ide na backend
dotnet test                                                # svi .NET testovi
cd src/web && npm test                                     # frontend testovi (Vitest)
cd src/web && npm run build && npm run lint                # frontend build i lint
cd src/web && npm run api                                  # osveži openapi.json iz pokrenutog API-ja i regeneriši klijent

# migracije
dotnet ef migrations add NazivPromene -p src/Zalihe.Infrastructure -s src/Zalihe.Web -o Persistence/Migrations
dotnet ef database update -p src/Zalihe.Infrastructure -s src/Zalihe.Web
```

Postgres iz Dockera je na portu 5433, jer je 5432 zauzet lokalno instaliranim PostgreSQL-om.
