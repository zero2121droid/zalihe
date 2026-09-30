# Zalihe: specifikacija MVP-a (v1)

## 1. Cilj

Vlasnik malog webshopa ili firme u svakom trenutku zna koliko čega ima, ne prodaje ono čega nema i zna kada treba da naruči.

**Ciljni korisnici v1:** mali webshopovi na WooCommerce-u sa sopstvenim magacinom (Srbija i region). Jezgro aplikacije je generičko, pa je mogu koristiti i firme bez webshopa, preko ručnog unosa i CSV uvoza, ali marketing u prvoj fazi cilja webshopove.

**Rok:** prvi pravi korisnik za oko 10 nedelja (10-20h nedeljno).

## 2. Obim v1

### 2.1 Artikli
Naziv, SKU (jedinstven u okviru firme), opciono barkod, jedinica mere, kategorija, nabavna i prodajna cena (opciono), minimalna zaliha, aktivan/neaktivan.

Svaka WooCommerce varijacija (npr. majica M plava) je poseban artikal. Opciono polje `GroupName` služi samo za grupisanje u prikazu.

### 2.2 Kretanja zaliha
Tipovi: `Receipt` (prijem), `Sale` (izlaz/prodaja), `Adjustment` (korekcija: popis, otpis, oštećenje), `Return` (povrat).
Svako kretanje: artikal, količina sa predznakom, datum, izvor (`Manual`, `Csv`, `WooCommerce`), spoljna referenca, napomena, korisnik.

### 2.3 CSV/Excel uvoz
Uvoz artikala sa početnim stanjem. Korisnik mapira kolone, vidi pregled pre uvoza i listu grešaka po redovima. Početno stanje se upisuje kao `Adjustment` sa napomenom "Početno stanje".

### 2.4 WooCommerce integracija
- Povezivanje shopa bez ručnog kopiranja ključeva (WooCommerce auth endpoint)
- Uvoz proizvoda i varijacija, sa povezivanjem po SKU-u
- Automatsko skidanje sa stanja kad stigne porudžbina i vraćanje kad se otkaže
- Slanje stanja nazad u shop

### 2.5 Upozorenja
Kad stanje padne na minimalnu zalihu ili ispod: oznaka u aplikaciji i email (najviše jedan email dnevno po firmi, sa spiskom).

### 2.6 Pregled i izveštaji
Trenutno stanje (pretraga, filter po kategoriji, "ispod minimuma"), vrednost zaliha po nabavnoj ceni, najprodavaniji artikli u poslednjih 30 dana, artikli bez prometa 60+ dana, istorija kretanja po artiklu.

### 2.7 Firme i korisnici
Registracija firme, vlasnik plus pozivanje dodatnih korisnika. Bez uloga i prava pristupa u v1.

### 2.8 Van obima v1 (ne implementirati)
Fiskalizacija i ESIR, SEF i e-fakture, porudžbenice dobavljačima, više magacina, lotovi i serijski brojevi, rokovi trajanja, mobilna aplikacija, skeniranje barkoda, Shopify i drugi kanali, knjigovodstvo, uloge i prava, AI predviđanje, naplata pretplate (prvi korisnici besplatno ili ručna faktura).

## 3. Arhitektura

### 3.1 Stack
- ASP.NET Core (.NET 10), PostgreSQL, EF Core
- Hangfire sa Postgres skladištem za pozadinske poslove
- React + TypeScript + Vite, React Router, TanStack Query, Mantine
- ASP.NET Core Identity, cookie autentifikacija
- OpenAPI + generisani TypeScript klijent (NSwag ili Orval)
- Produkcija: jedan VPS, Docker Compose, reverse proxy sa HTTPS-om

### 3.2 Frontend i API
ASP.NET Core servira React build (statičke fajlove) i API (`/api/...`) sa iste adrese. U razvoju Vite dev server prosleđuje `/api` zahteve backendu (proxy). Cookie autentifikacija sa antiforgery zaštitom za zahteve koji menjaju podatke.

### 3.3 Multi-tenancy
Jedna baza, kolona `TenantId` u svim tabelama sa podacima firme. `ITenantContext` daje trenutnu firmu: iz claima ulogovanog korisnika, a kod webhookova i pozadinskih poslova se postavlja eksplicitno. EF Core global query filter filtrira sve upite. Pozadinski poslovi uvek primaju `TenantId` kao parametar.

### 3.4 Prevodi
Aplikacija je od prvog dana dvojezična: srpski latinica (glavni) i engleski. Frontend koristi `react-i18next` sa JSON fajlovima po jeziku, API vraća kodove grešaka koje frontend prevodi, a emailovi se prevode na backendu. Formati brojeva i datuma zavise od jezika, uz podršku za zarez kao decimalni separator. Detaljna pravila su u `CLAUDE.md`. Isti fajlovi sa prevodima se kasnije koriste i u mobilnoj aplikaciji.

## 4. Model podataka

| Entitet | Ključna polja | Napomena |
|---|---|---|
| `Tenant` | Id, Name, CreatedAt | Firma |
| `User` | Identity polja, TenantId, Language | Language: `sr-Latn` (podrazumevano) ili `en` |
| `Item` | Id, TenantId, Name, Sku, Barcode, Unit, Category, GroupName, PurchasePrice, SalePrice, MinStock, IsActive | Unique (TenantId, Sku) |
| `StockMovement` | Id, TenantId, ItemId, Type, Quantity, OccurredAt, Source, ExternalRef, Note, UserId | Samo dodavanje |
| `StockLevel` | ItemId (PK), TenantId, Quantity, UpdatedAt, RowVersion | Keš, ažurira se u istoj transakciji |
| `SalesChannel` | Id, TenantId, Type, BaseUrl, EncryptedCredentials, WebhookSecret, Status, LastSyncedAt | |
| `ItemChannelMapping` | Id, TenantId, ItemId, ChannelId, ExternalId, ParentExternalId | ParentExternalId za varijacije |
| `ExternalOrder` | Id, TenantId, ChannelId, ExternalId, Status, LastModifiedAt | Unique (ChannelId, ExternalId) |
| `ExternalOrderLine` | Id, ExternalOrderId, ItemId, OrderedQuantity, AppliedQuantity | AppliedQuantity = koliko je već skinuto |
| `WebhookEvent` | Id, ChannelId, ReceivedAt, Payload, ProcessedAt, Error | Sirovi događaji (inbox) |

`StockLevel` mora uvek biti jednak zbiru kretanja. Postoji administrativna akcija za ponovni obračun.

## 5. Kanali prodaje

Jezgro ne zna za WooCommerce, već radi preko interfejsa u Application sloju:

```csharp
public interface ISalesChannel
{
    Task<IReadOnlyList<ExternalProduct>> FetchProductsAsync(CancellationToken ct);
    Task<IReadOnlyList<ExternalOrder>> FetchOrdersSinceAsync(DateTimeOffset since, CancellationToken ct);
    Task PushStockAsync(IReadOnlyList<StockUpdate> updates, CancellationToken ct);
}
```

Ručni unos i CSV nisu kanali, već direktni slučajevi korišćenja.

## 6. Obrada porudžbina (ključna logika)

Umesto obrade svakog prelaza statusa, za svaku porudžbinu se računa željeno stanje i primenjuje samo razlika. Tako su duplirani, zakasneli i izmenjeni događaji automatski ispravni.

```
activeStatuses = { processing, on-hold, completed }

za porudžbinu (u transakciji, sa zaključavanjem reda ExternalOrder):
  za svaku stavku:
    desired = status u activeStatuses ? orderedQuantity : 0
    delta   = desired - appliedQuantity
    ako delta > 0: StockMovement(Sale,   -delta, ExternalRef = orderId)
    ako delta < 0: StockMovement(Return, -delta, ExternalRef = orderId)
    appliedQuantity = desired
  stavke koje su nestale iz porudžbine tretiraju se kao desired = 0
```

Stavke bez mapiranog artikla se čuvaju kao nepovezane i prikazuju korisniku da ih poveže. Po povezivanju se porudžbina ponovo obrađuje.

Unit testovi moraju pokriti: novu porudžbinu, duplirani događaj, otkazivanje, ponovno aktiviranje posle otkazivanja, izmenu količine, uklonjenu stavku, događaje van redosleda.

## 7. WooCommerce integracija

### 7.1 Povezivanje
Preusmeravanje korisnika na `{shop}/wc-auth/v1/authorize` sa `app_name`, `scope=read_write`, `user_id` (ID kanala), `return_url` i `callback_url`. WooCommerce šalje ključeve POST-om na `callback_url` (mora biti HTTPS). U lokalnom razvoju ključevi se unose ručno.

### 7.2 Početni uvoz
`GET /wp-json/wc/v3/products` (stranično, `per_page=100`) i za varijabilne proizvode `GET /products/{id}/variations`. Povezivanje sa postojećim artiklima po SKU-u, a ostali se nude za kreiranje.

### 7.3 Webhookovi
Registracija preko API-ja za `order.created` i `order.updated`, sa adresom `/webhooks/woo/{channelId}`.
- Provera potpisa: `X-WC-Webhook-Signature` = base64(HMAC-SHA256(telo zahteva, webhook secret))
- WooCommerce pri kreiranju šalje ping (form-encoded `webhook_id=...`), treba vratiti 200
- Endpoint samo proveri potpis, sačuva `WebhookEvent`, zakaže Hangfire posao i odmah vrati 200

### 7.4 Usklađivanje
Hangfire posao na svakih 15-30 minuta: `GET /orders?modified_after={LastSyncedAt}` i obrada kroz istu logiku iz poglavlja 6. Pokriva propuštene webhookove.

### 7.5 Slanje stanja
Posle promene stanja mapiranog artikla zakazuje se slanje sa kratkim odlaganjem (npr. 30 s) radi grupisanja. Proste proizvode šalje `POST /products/batch`, a varijacije `POST /products/{parentId}/variations/batch`, sa `manage_stock: true` i `stock_quantity`. WooCommerce stanje je ceo broj, pa se šalje zaokruženo naniže. Aplikacija je izvor istine za stanje, a shop samo prikazuje.

### 7.6 Greške
Retry sa rastućim razmakom (Hangfire). Posle ponovljenih neuspeha kanal dobija status greške i vlasnik se obaveštava (npr. sigurnosni plugin blokira API ili su ključevi opozvani).

## 8. Bezbednost i privatnost
- Iz porudžbina se čuvaju samo ID, status, datumi, stavke i količine. Sirovi `WebhookEvent.Payload` se pre čuvanja čisti od podataka kupca (billing, shipping, customer polja) i briše posle 30 dana.
- API ključevi šifrovani preko ASP.NET Data Protection
- Svi zahtevi preko HTTPS-a, antiforgery zaštita za cookie autentifikaciju

## 9. Lokalni razvoj
Docker Compose sa servisima: PostgreSQL, WordPress + WooCommerce (+ MariaDB). WooCommerce se puni test proizvodima (proste i varijabilne) preko WP-CLI skripte. Backend i React se pokreću lokalno, van Dockera.

## 10. Plan po nedeljama

| Nedelja | Cilj |
|---|---|
| 1 | Kostur: rešenje, Docker Compose, Identity cookie auth, Vite + React servirani iz ASP.NET Core, generisanje API klijenta, i18n postavka (sr-Latn + en, izbor jezika), jedan ekran od baze do UI-ja (lista i dodavanje artikala) |
| 2 | Multi-tenancy (global filteri, registracija firme), kompletan CRUD artikala |
| 3 | Kretanja zaliha, obračun stanja, istorija po artiklu, unit testovi |
| 4 | CSV uvoz sa mapiranjem kolona i pregledom |
| 5 | Početna strana i rezerva za učenje/dug |
| 6-7 | WooCommerce: povezivanje, uvoz, webhookovi, obrada porudžbina, usklađivanje, slanje stanja |
| 8 | Upozorenja (UI + email) i izveštaji |
| 9 | Doterivanje, produkcijski deploy, backup baze |
| 10 | Prvi pravi korisnik |

## 11. Posle v1 (samo za orijentaciju)

### 11.1 Obaveštenja u realnom vremenu (SignalR) — prva stavka posle v1
Promene stanja (npr. porudžbina iz WooCommerce-a) se odmah prikazuju na otvorenim ekranima, bez osvežavanja.
- Hub sa grupama po `TenantId`, uz proveru pripadnosti firmi pri povezivanju
- Obaveštenje se šalje posle uspešne transakcije kretanja zaliha, i iz Hangfire poslova preko `IHubContext`-a
- Frontend na poruku samo invalidira odgovarajuće TanStack Query upite (ne prenosi podatke kroz SignalR)
- Posle ponovnog povezivanja frontend osvežava sve aktivne upite, pa propuštene poruke nisu problem
- U v1 se isti efekat postiže periodičnim osvežavanjem preko TanStack Query-ja

### 11.2 Mobilna aplikacija (React Native, Expo)
Namena: rad u magacinu, odnosno prijem robe, popis i skeniranje barkoda kamerom telefona.
- Koristi isti API i isti generisani TypeScript klijent kao web
- Autentifikacija preko bearer tokena (ASP.NET Core Identity API endpointi, access + refresh token), paralelno sa cookie-jem za web
- Tokeni se čuvaju u sigurnom skladištu uređaja (`expo-secure-store`), nikad u `AsyncStorage`-u
- Pretpostavke u v1 koje ovo omogućavaju: pravila u odeljku "API spreman za mobilnu aplikaciju" u `CLAUDE.md`

### 11.3 Ostalo
Shopify, porudžbenice dobavljačima, više magacina, naplata pretplate, SEF/e-fakture za B2B distributere.
