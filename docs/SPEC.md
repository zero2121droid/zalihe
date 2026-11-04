# Zalihe: specifikacija MVP-a (v1)

## 1. Cilj

Vlasnik malog webshopa ili firme u svakom trenutku zna koliko čega ima, ne prodaje ono čega nema i zna kada treba da naruči.

**Ciljni korisnici v1:** mali webshopovi na WooCommerce-u sa sopstvenim magacinom (Srbija i region). Jezgro aplikacije je generičko, pa je mogu koristiti i firme bez webshopa, preko ručnog unosa i CSV uvoza, ali marketing u prvoj fazi cilja webshopove.

**Pozicioniranje:** WooCommerce sam vodi jedan broj po proizvodu i smanjuje ga kad stigne porudžbina. Lagerko zna koliko robe firma stvarno ima, gde je otišla i šta treba naručiti, gde god da prodaje (sajt, Instagram, telefon, lokal). WooCommerce dovodi korisnike, a ostalo ih zadržava:
- **WooCommerce je ulaz.** Takve firme se lako nalaze, a povezivanje i uvoz za par minuta odmah pokazuju vrednost. Bez sinhronizacije aplikacija je samo bolji Excel.
- **Vrednost je van prodavnice.** Prodaja van sajta, prijem i popis, istorija svake promene, zaposleni bez pristupa wp-adminu i izveštaji za naručivanje. Poruka u marketingu je „znaš koliko robe stvarno imaš”, a ne „sinhronizacija sa WooCommerce-om”.
- **Ko nije kupac:** mala prodavnica koja prodaje samo preko sajta i gde vlasnik sve radi sam; njoj je WooCommerce dovoljan. Kupac ima stotine artikala (sa varijacijama), prodaje i van sajta, redovno prima robu, ima zaposlene i danas vodi Excel pored WooCommerce-a.

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
- Uvoz proizvoda i varijacija, sa povezivanjem po SKU-u; novi artikli dobijaju barkod iz WooCommerce polja „GTIN, UPC, EAN ili ISBN“
- Razlike povezanih artikala i proizvoda (naziv, barkod, kategorija, prodajna i nabavna cena) prikazuju se u pregledu uvoza, a primenjuju se samo izabrane, i to samo iz prodavnice u aplikaciju. Unapred su izabrana samo polja koja su u aplikaciji prazna; prazno polje u prodavnici nikad ne briše vrednost, a stanje se nikad ne preuzima
- Automatsko skidanje sa stanja kad stigne porudžbina i vraćanje kad se otkaže
- Slanje stanja nazad u shop
- Ekran „Porudžbine”: porudžbine iz prodavnica (broj, datum, status, proizvodi, koliko je skinuto sa stanja), pretraga, filter po statusu i „nepovezani proizvodi”, sa putem do povezivanja. Bez podataka kupca.

### 2.5 Upozorenja
Kad stanje padne na minimalnu zalihu ili ispod: oznaka u aplikaciji i email (najviše jedan email dnevno po firmi, sa spiskom).

### 2.6 Pregled i izveštaji
Trenutno stanje (pretraga, filter po kategoriji, "ispod minimuma", sortiranje po nazivu, stanju, prodaji za 30 dana i vrednosti), vrednost zaliha po nabavnoj ceni, najprodavaniji artikli u poslednjih 30 dana, artikli bez prometa 60+ dana, istorija kretanja po artiklu.

### 2.7 Firme i korisnici
Registracija firme, vlasnik plus pozivanje dodatnih korisnika, svako sa svojom ulogom (11.2). Vlasnik koji registruje firmu je admin.

Pozivanje: korisnik upiše email, a pozvani dobija link koji važi 7 dana i može da se iskoristi jednom; ponovni poziv šalje nov link, a stari prestaje da važi, a poziv može da se opozove. Pozvani upiše ime i lozinku i ulazi u firmu. Adresa koja već ima nalog ne može da se pozove (jedan korisnik pripada jednoj firmi). Poziva admin i bira ulogu pozvanog (podrazumevano „Zaposleni”); ponovni poziv može da promeni ulogu.

Zaboravljena lozinka: korisnik upiše email i dobija link za novu lozinku (na svom jeziku), koji važi 2 sata i može da se iskoristi jednom. Odgovor je isti i kad adresa nema nalog, a jedan nalog dobija najviše jedan takav email na 2 minuta. Posle nove lozinke korisnik je prijavljen, nalog se otključava (ako je bio zaključan zbog pogrešnih lozinki), a ostali uređaji se odjavljuju.

Podešavanja: naziv firme, a za prijavljenog korisnika ime, jezik i promena lozinke. Email je i korisničko ime i u v1 se ne menja. Naziv firme menja samo admin.

### 2.8 Van obima v1 (ne implementirati)
Fiskalizacija i ESIR, SEF i e-fakture, porudžbenice dobavljačima, više magacina, lotovi i serijski brojevi, rokovi trajanja, mobilna aplikacija, skeniranje barkoda (osim probe u brzoj pretrazi, ispod), Shopify i drugi kanali, knjigovodstvo, prava po korisniku (osim uloga iz 11.2), AI predviđanje, naplata pretplate (prvi korisnici besplatno ili ručna faktura).

**Proba (oktobar 2026):** skeniranje barkoda kamerom u brzoj pretrazi: kamera pročita kod, a artikal sa tim barkodom ili šifrom se otvori. Ako se ne pokaže korisnim, uklanja se i ostaje za mobilnu aplikaciju (11.3), gde je i skeniranje za prijem i popis.

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
| `ExternalOrder` | Id, TenantId, ChannelId, ExternalId, Status, PlacedAt, LastModifiedAt | Unique (ChannelId, ExternalId); PlacedAt = kad je naručena |
| `ExternalOrderLine` | Id, ExternalOrderId, ExternalProductId, Name, ItemId, OrderedQuantity, AppliedQuantity | AppliedQuantity = koliko je već skinuto; Name = naziv proizvoda u prodavnici |
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

Stavke bez mapiranog artikla se čuvaju kao nepovezane i prikazuju korisniku na ekranu „Porudžbine” da ih poveže. Veza važi za porudžbine naručene posle povezivanja: stanje artikla u trenutku povezivanja već sadrži ranije porudžbine (preuzeto iz prodavnice ili izbrojano), pa ih ponovna obrada ne skida drugi put.

Unit testovi moraju pokriti: novu porudžbinu, duplirani događaj, otkazivanje, ponovno aktiviranje posle otkazivanja, izmenu količine, uklonjenu stavku, događaje van redosleda.

## 7. WooCommerce integracija

### 7.1 Povezivanje
Preusmeravanje korisnika na `{shop}/wc-auth/v1/authorize` sa `app_name`, `scope=read_write`, `user_id` (ID kanala), `return_url` i `callback_url`. WooCommerce šalje ključeve POST-om na `callback_url` (mora biti HTTPS). `user_id` je jednokratni token (u bazi samo heš), a započeto povezivanje važi sat vremena. Ručni unos ključeva ostaje kao rezerva, za prodavnice u kojima sigurnosni dodatak blokira `wc-auth`. U lokalnom razvoju callback ide na HTTPS port backenda (profil `https`).

### 7.2 Početni uvoz
`GET /wp-json/wc/v3/products` (stranično, `per_page=100`) i za varijabilne proizvode `GET /products/{id}/variations`. Povezivanje sa postojećim artiklima po SKU-u, a ostali se nude za kreiranje. Nabavna cena novog artikla se uzima iz WooCommerce polja „Cost of goods sold” (`cost_of_goods_sold.total_value`; varijacija bez svoje cene nasleđuje cenu proizvoda). Ako je funkcija u prodavnici isključena ili je cena 0, nabavne cene nema.

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
Retry sa rastućim razmakom (Hangfire). Posle ponovljenih neuspeha kanal dobija status greške i vlasnik se obaveštava (npr. sigurnosni plugin blokira API ili su ključevi opozvani). Kanal pamti od kada prodavnica ne radi, a status greške dobija kad ne radi bar 30 minuta (jedan neuspeh je često prolazan); prvi uspešan kontakt ga vraća. Vlasnik vidi traku sa razlogom iznad svakog ekrana aplikacije; email ide uz upozorenja (2.5), kad se uvede slanje emailova.

## 8. Bezbednost i privatnost
- Iz porudžbina se čuvaju samo ID, status, datumi, stavke (proizvod i njegov naziv) i količine. Sirovi `WebhookEvent.Payload` se pre čuvanja čisti od podataka kupca (billing, shipping, customer polja) i briše posle 30 dana.
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

**Redosled.** Prema pozicioniranju (1), posle 11.1 prednost imaju funkcionalnosti van prodavnice, a ne dublja integracija ili novi kanali:
1. ručne porudžbine (11.5);
2. brži prijem i popis, zatim prijem sa fakture (11.7);
3. mobilna aplikacija za magacin, sa skeniranjem barkoda (11.3).

Shopify i drugi kanali (11.6) čekaju. Firme bez webshopa mogu da koriste aplikaciju, ali se marketingom ne ciljaju dok se osnovna grupa ne potvrdi.

### 11.1 Obaveštenja u realnom vremenu (SignalR) — prva stavka posle v1
Promene stanja (npr. porudžbina iz WooCommerce-a) se odmah prikazuju na otvorenim ekranima, bez osvežavanja.
- Hub sa grupama po `TenantId`, uz proveru pripadnosti firmi pri povezivanju
- Obaveštenje se šalje posle uspešne transakcije kretanja zaliha, i iz Hangfire poslova preko `IHubContext`-a
- Frontend na poruku samo invalidira odgovarajuće TanStack Query upite (ne prenosi podatke kroz SignalR)
- Posle ponovnog povezivanja frontend osvežava sve aktivne upite, pa propuštene poruke nisu problem
- U v1 se isti efekat postiže periodičnim osvežavanjem preko TanStack Query-ja

### 11.2 Uloge korisnika — obavezno pre pozivanja zaposlenih

**Stanje (oktobar 2026):** napravljeno, sa podešavanjem „Zaposleni vide nabavne cene”. Postojeći korisnici i pozivi su pri uvođenju dobili ulogu admin, jer su do tada sve videli i smeli. Izveštaji i uvozi su samo za admina i menadžera, koji cene vide uvek, pa se cene sakrivaju u artiklima (cena, vrednost, sortiranje po vrednosti, forma), istoriji artikla i na Početnoj. Izmena artikla od zaposlenog bez prava zadržava nabavnu cenu koju on ne vidi, a izmena istorije koja je menjala samo nabavnu cenu se izostavlja sa strane.
Nabavna cena otkriva zaradu na svakom proizvodu, pa je ne sme videti svako ko radi u magacinu.

| Uloga | Šta sme |
|---|---|
| Admin | sve: podešavanja firme, korisnici i njihove uloge, povezivanje prodavnice |
| Menadžer | podešavanja (npr. minimalne zalihe, upozorenja), artikli, kretanja, uvoz, izveštaji, nabavne cene; ne upravlja korisnicima ni ulogama |
| Zaposleni | artikli (i dodavanje i izmena), stanje, kretanja (prijem, prodaja, povrat, popis), porudžbine; prodajna cena i vrednost po prodajnoj ceni; nabavna cena samo kad je admin dozvoli (vidi niže). Bez uvoza, izveštaja i podešavanja firme |

- Vlasnik koji registruje firmu je admin. Firma uvek ima bar jednog admina (`user.last_admin`).
- Ko šta sme u API-ju: samo admin menja naziv firme, vidi korisnike i pozive, poziva, menja uloge, povezuje prodavnicu i menja njene ključeve. Admin i menadžer još uvoze (CSV i iz prodavnice), vide izveštaje, proveravaju vezu sa prodavnicom i pokreću proveru stanja. Svi vide artikle, kretanja, porudžbine, Početnu i listu prodavnica. Zabranjeno je 403 sa kodom `auth.forbidden`.
- Promena uloge važi u roku od minuta, bez ponovne prijave: claimovi u cookie-ju se jednom u minutu ponovo prave iz korisnika.
- Da li zaposleni vide nabavne cene bira admin u podešavanjima firme („Zaposleni vide nabavne cene”), za sve zaposlene u firmi odjednom. Podrazumevano je isključeno, pa je oprezan vlasnik zaštićen od početka, a vlasnik sa malim timom kome veruje ne mora svima da daje ulogu menadžera samo zbog cena. Admin i menadžer cene vide uvek. Promena važi od sledećeg zahteva, bez ponovne prijave.
- Kad je isključeno, zaposleni ne vidi nabavnu cenu ni ništa iz čega se ona izračunava: vrednost zaliha po nabavnoj ceni (stanje × nabavna cena otkriva cenu), ukupnu vrednost na Početnoj i u izveštajima, vrednost po kategoriji ni vezanu robu u izveštaju „Roba koja stoji”, izmene nabavne cene u istoriji artikla, nabavnu cenu u formi artikla, CSV uvozu i uvozu iz prodavnice.
- Pravo važi u API-ju, ne samo u prikazu: za zaposlenog bez tog prava API vraća ova polja kao `null` (u OpenAPI-ju opisana kao takva), odbija njihov unos i ne dozvoljava filtriranje ni sortiranje po njima. Sakrivanje samo u React-u nije zaštita, jer je API isti za mobilnu aplikaciju.
- Uloga je na korisniku u firmi (claim u cookie-ju i u bearer tokenu), a podešavanje na firmi, pa se čita iz baze pri zahtevu, a ne iz cookie-ja, da isključivanje važi odmah. Provera je jedno mesto u Application sloju (npr. `ICurrentUser.CanSeePurchasePrices`: uloga + podešavanje firme), ne `if` u svakom kontroleru.
- Integracioni testovi za svaku ulogu i oba položaja podešavanja: zaposleni bez prava ne dobija nabavnu cenu ni vrednost ni iz jednog endpointa, a posle uključivanja ih dobija bez ponovne prijave.

### 11.3 Mobilna aplikacija (React Native, Expo)
Namena: rad u magacinu, odnosno prijem robe, popis i skeniranje barkoda kamerom telefona. Prijem sa fakture (11.7) radi preko istog API-ja, pa se faktura može uslikati i telefonom.
- Koristi isti API i isti generisani TypeScript klijent kao web
- Autentifikacija preko bearer tokena (ASP.NET Core Identity API endpointi, access + refresh token), paralelno sa cookie-jem za web
- Tokeni se čuvaju u sigurnom skladištu uređaja (`expo-secure-store`), nikad u `AsyncStorage`-u
- Pretpostavke u v1 koje ovo omogućavaju: pravila u odeljku "API spreman za mobilnu aplikaciju" u `CLAUDE.md`

### 11.4 Izgled po firmi
Vlasnik da oseti aplikaciju kao svoju, bez kvarenja čitljivosti i statusa zaliha.
- Logo firme u meniju, pored ili umesto naziva (podešavanja firme).
- Jedna boja naglaska (dugmad, aktivna stavka menija, linkovi) po izboru iz 5–6 unapred spremljenih boja. Svaka prolazi kontrast iz DESIGN.md u obe teme i jasno se razlikuje od statusnih boja. Statusne boje i ostatak palete se ne menjaju.
- Boje se ne preuzimaju iz WordPress-a: WooCommerce ključevi ne daju pristup izgledu sajta, teme i graditelji stranica čuvaju boje svako na svoj način, a proizvoljna boja može da ruši kontrast ili da liči na upozorenje.

### 11.5 Ručne porudžbine
Za prodaju van prodavnice (Instagram, telefon, radnja), da firma ima pregled svih prodaja na jednom mestu, sa ili bez WooCommerce-a.

**Stanje (novembar 2026):** napravljen korak (1). Ručna porudžbina ima stavke, opcionu referencu i napomenu i statuse Potvrđena i Otkazana (kodovi `confirmed` i `cancelled`; `cancelled` je isti kod kao u WooCommerce-u, pa deli prevod). Pravi se odmah kao potvrđena, bez nacrta. Otkazivanje vraća robu, a ponovna potvrda je opet skida. Na ekranu „Porudžbine” je dugme „Nova porudžbina”, a ručne porudžbine su u istoj listi, sa brojem bez `#` (R-0001) i oznakom „Ručno”. Izmena stavki posle čuvanja još ne postoji: greška se ispravlja otkazivanjem i novom porudžbinom.

- **Prodaja na artiklu je ručna porudžbina sa jednom stavkom.** Dugme „Prodaja” na detalju artikla ne pravi samo kretanje, nego porudžbinu, pa je svaka prodaja i u „Porudžbinama”, a greška se ispravlja otkazivanjem porudžbine umesto korekcijom. Prijem, povrat i korekcija ostaju obična kretanja. API za ručni izlaz (`Sale` kretanje) ostaje, ali ga ekran više ne koristi.
- **Ručna porudžbina nije fiskalni račun.** Lagerko ne izdaje račune (fiskalizacija je van obima, 2.8); radnja račun izdaje svojom kasom (ESIR), a u Lagerku beleži samo robu.

- Uloge ostaju podeljene: WooCommerce porudžbine Lagerko samo čita i nikad im ne menja status (status pokreće emailove kupcu, povraćaj novca i dodatke za kurire). Ručne porudžbine postoje samo u Lagerku i ne prave se u WooCommerce-u (to bi poslalo email kupcu, tražilo podatke kupca, a uvezena porudžbina bi skinula robu drugi put). U prodavnicu ide samo stanje.
- Ručna porudžbina menja stanje kao i svaka druga, pa `StockPushService` (7.5) šalje novo stanje u sve povezane prodavnice, bez nove integracije. Primer: 3 majice, 2 prodate na Instagramu, shop za nekoliko sekundi prikazuje 1.
- Model: `ExternalOrder` sa novim poljem `Source` (`WooCommerce`, `Manual`) i opcionalnim `ChannelId` (ručni unos nije kanal, vidi 5). Broj se dodeljuje automatski u okviru firme (R-0001…), uz opcionalnu referencu (npr. „IG 154”). Stavke: artikal, količina, opciono prodajna cena (cena dolazi u koraku 3).
- Bez podataka kupca: najviše slobodna napomena, uz upozorenje u formi da se tu ne upisuju adrese i telefoni.
- Statusi: Nacrt, Potvrđena, Poslata, Otkazana, Vraćena. Obrada je ista kao u 6, samo sa `activeStatuses = { Potvrđena, Poslata }`, pa izmena količine, uklonjena stavka, otkazivanje i ponovno aktiviranje rade bez posebnog koda za prelaze. Isti unit testovi kao u 6, za ručni izvor.
- Nedovoljno robe: upozorenje, ali potvrda je dozvoljena i stanje može u minus (WooCommerce porudžbina je možda stigla malo ranije, a prodavac zna bolje). Istovremena prodaja poslednjeg komada u shopu i ručno ne može potpuno da se spreči jer slanje stanja kasni nekoliko sekundi; minus stanje i upozorenje to čine vidljivim.
- Vraćena roba se vraća na stanje; oštećena se zatim otpisuje korekcijom, kao i danas. Izmena posle slanja je dozvoljena, jer je svaka ispravka novo kretanje.
- Ekran „Porudžbine” prikazuje obe vrste zajedno, sa kolonom i filterom po izvoru.
- Redosled: (1) ručna porudžbina sa stavkama i statusima Potvrđena/Otkazana; (2) Poslata, Vraćena i filter po izvoru; (3) opciono prodajna cena na stavci i promet u izveštajima.
- Pre početka proveriti fiskalizaciju (2.8): ako radnja izdaje račune, ručna porudžbina počinje da liči na kasu, a to je posebna odluka.

### 11.6 Ostalo
Shopify, porudžbenice dobavljačima, više magacina, naplata pretplate, SEF/e-fakture za B2B distributere.

### 11.7 Prijem robe sa fakture (AI)
Prijem sa fakture od 30 stavki danas znači 30 puta otvoriti artikal i upisati količinu. Cilj: korisnik slika ili otpremi fakturu, a aplikacija popuni prijem koji on samo proveri i potvrdi. Nije „AI predviđanje” iz 2.8, ali je nov obim, pa ide posle v1 (faza 0 može i ranije, kao posebna odluka).

Osnovno pravilo: AI nikad ne pravi kretanja zaliha. On samo popunjava **nacrt prijema**, a kretanja nastaju tek kad čovek potvrdi.

```
 ručni unos ────────────►┐
                         ├─► nacrt prijema ──potvrda──► N × StockMovement(Receipt) u jednoj transakciji
 slika / PDF ──AI──────►┘    (dobavljač, br. dokumenta,   (čovek)   + StockLevel + slanje stanja u prodavnicu (7.5)
                              datum, stavke)
```

#### Faza 0: prijem sa više stavki (bez AI-ja)

**Stanje (novembar 2026):** napravljeno. Ekran „Prijem robe” (`/receipts`) u meniju, lista proknjiženih prijema i stranica „Novi prijem”. Prijem se knjiži odmah, bez čuvanja nacrta: status `Draft` postoji u modelu za fazu 1, ali ga API još ne vraća. Kretanje svake stavke ima napomenu = dobavljač i `ExternalRef` = broj dokumenta. Broj dokumenta nije obavezan (prijem bez otpremnice). Polja za fazu 1 (`Origin`, `RawName`, `RawCode`, `MatchKind`) se dodaju sa njom.

- Ekran „Prijem robe”: dobavljač (slobodan tekst), broj fakture ili otpremnice, datum, stavke (artikal, količina, opciono nabavna cena).
- Potvrda pravi sva `Receipt` kretanja u jednoj transakciji, sa `ExternalRef` = broj dokumenta. Proknjižen prijem se ne menja; greška se ispravlja korekcijom, kao i svako kretanje.
- Nabavna cena sa prijema se nudi kao nova cena artikla i primenjuje samo ako je korisnik izabere (isti obrazac kao razlike pri uvozu iz prodavnice, 2.4). Polje za cenu se popuni trenutnom cenom artikla; izbor „Postavi kao nabavnu cenu” se pojavi tek kad se cena razlikuje, a unapred je označen samo za artikal koji još nema nabavnu cenu. Promena cene se upisuje u istoriju artikla.
- Isti dobavljač i isti broj dokumenta drugi put: upozorenje (`receipt.duplicate_document`), uz mogućnost da se ipak nastavi.
- Uloge (11.2): prijem sme svako ko sme kretanja. Zaposleni bez prava na nabavne cene ne vidi cene ni na prijemu, a API ih ne vraća i ne prima od njega; cene kasnije dopunjuje admin ili menadžer.

#### Faza 1: AI čita fakturu
```
 web / telefon        API                       Hangfire posao                 Claude API
 slika ili PDF ─POST─► nacrt, status Obrada ──► IDocumentExtractor ──────────► strukturisan JSON
                                                   │                           (stavke: naziv, šifra, barkod,
 ◄──── polling ─────── nacrt sa stavkama ◄──────────┤                            količina, jedinica, cena,
       (TanStack Query)                             ▼                            rabat; ukupan iznos)
                                             uparivanje sa artiklima (bez AI-ja)
```

- `IDocumentExtractor` je interfejs u Application sloju (kao `ISalesChannel`), a implementacija sa Claude API-jem u Infrastructure. Testovi koriste lažni extractor i nikad ne zovu pravi API.
- Čitanje traje 10–30 s, pa ide kao Hangfire posao (sa `TenantId` kao parametrom), a ne u samom zahtevu. Frontend prati status nacrta. Isto radi i za mobilnu aplikaciju (11.3).
- Uparivanje stavki sa artiklima, redom:
  1. barkod ili SKU sa fakture tačno odgovara artiklu;
  2. zapamćena šifra dobavljača: kad korisnik jednom poveže stavku dobavljača sa artiklom, veza se pamti po dobavljaču i sledeći put se primenjuje sama;
  3. predlog po nazivu (nekoliko kandidata), označen „proveri”;
  4. nepovezano: korisnik bira artikal ili pravi nov.
- Provera: AI očita i ukupan iznos dokumenta, a aplikacija ga upoređuje sa zbirom stavki. Razlika se prikazuje pre potvrde.
- Ekran: levo dokument, desno stavke sa oznakom *povezano / predlog / nepovezano*. Potvrda ide kroz isti tok kao faza 0.
- Pakovanja: na fakturi „2 kutije”, artikal u komadima. Uz šifru dobavljača se pamti i faktor (npr. 1 kutija = 12 kom).
- Pre ugradnje: prototip na 5–10 pravih faktura dobavljača ciljnih korisnika, da se izmere tačnost i cena po fakturi.

#### Model podataka
| Entitet | Ključna polja | Napomena |
|---|---|---|
| `Receipt` | Id, TenantId, SupplierName, DocumentNumber, DocumentDate, Status (`Processing`, `Draft`, `Posted`, `Failed`), Origin (`Manual`, `Scan`), CreatedBy, PostedAt | Posle `Posted` se ne menja |
| `ReceiptLine` | Id, ReceiptId, ItemId?, RawName, RawCode, Quantity, Unit, UnitPrice?, MatchKind | Sirovi podaci sa dokumenta ostaju i posle povezivanja |
| `SupplierItemCode` | Id, TenantId, SupplierKey, Code (ili normalizovan naziv), ItemId, UnitFactor | Naučene veze, unique (TenantId, SupplierKey, Code) |
| `AiUsage` | Id, TenantId, Feature, Model, InputTokens, OutputTokens, OccurredAt | Potrošnja po firmi |

Sve tabele imaju global query filter po `TenantId`.

#### Dokument i privatnost
- Na fakturi nema podataka kupaca, ali može biti ime vozača ili osobe koja je primila robu. Dokument se čuva 30 dana radi provere (kao `WebhookEvent`, 8), pa se briše; ostaju samo očitane stavke.
- Politika privatnosti navodi da se dokument šalje AI servisu (Anthropic) na obradu. API po komercijalnim uslovima podrazumevano ne koristi podatke za treniranje; tačne uslove i DPA proveriti pre puštanja u rad.

#### Nalog, ključ i troškovi
- Claude pretplata (claude.ai) i API su odvojeni; aplikacija koristi samo API, koji se plaća po potrošnji (unapred kupljeni krediti, kartica za plaćanje u inostranstvu).
- Posebna organizacija „Lagerko” u Claude Console, na poslovni email kad postoji, sa sopstvenim plaćanjem i fakturama. Ne meša se sa ličnim nalogom i može se predati ako firma promeni vlasnika.
- Dva workspace-a, `dev` i `prod`, svaki sa svojim API ključem i limitom potrošnje, da greška u razvoju ne potroši produkciju.
- Jedan ključ, samo na serveru: lokalno `dotnet user-secrets`, u produkciji env varijabla (DEPLOY.md). Nikad u frontendu ni u gitu. Korisnici nemaju svoj nalog kod Anthropic-a ni svoj ključ.
- Anthropic vidi samo ukupnu potrošnju Lagerka, pa aplikacija sama beleži `usage` iz svakog odgovora u `AiUsage`, uz `TenantId`. Iz toga: mesečni limit skeniranja po firmi, uvid koliko koja firma košta i osnova za cene planova (PRICING.md).
- Limit skeniranja je **tvrd** (za razliku od limita porudžbina), jer svako skeniranje košta: `receipt.scan_limit_reached` sa `limit` i `current` u `ProblemDetails`. Ručni prijem (faza 0) radi i posle limita.
- Skeniranje je uračunato u cenu plana („X faktura mesečno”), korisnik ne plaća tokene direktno.
- Knjigovodstvo: usluga iz inostranstva; pitati knjigovođu za obračun PDV-a kad firma bude u sistemu PDV-a.
- AWS Bedrock ili Google Vertex su moguća zamena za direktan API ako klijent zahteva obradu u određenom regionu (npr. EU). Na početku nije potrebno.

#### Otvorena pitanja (pre faze 1)
1. **SEF i e-fakture.** Domaći dobavljači firmama verovatno šalju e-fakture preko SEF-a kao UBL XML (proveriti za ciljne korisnike). Takva faktura se čita tačno i bez AI-ja, pa bi upload XML fajla bio jeftin dodatak, a AI ostaje za uvoznu robu, otpremnice bez cena, papir i PDF. Integracija sa SEF-om ostaje van obima (2.8).
2. **Koja cena je nabavna.** Bez PDV-a i posle rabata (PDV obveznik može da odbije PDV) ili sa PDV-om (paušalac ne može)? Verovatno podešavanje na firmi.
3. ~~Da li faza 0 ulazi u v1.~~ Napravljena je u novembru 2026.

### 11.8 Vlasnik platforme
Vlasnik platforme je osoba koja vodi Lagerko, ne uloga u firmi: admin je svako ko registruje firmu, pa ovo pravo ne sme da zavisi od te uloge. Određuje ga konfiguracija (`Platform:OwnerEmails`), bez nove uloge i migracije.

- Za svaku novu firmu vlasnik dobija email (naziv firme, ime i email osobe koja ju je registrovala), kao posao u pozadini, pa greška u slanju ne obara registraciju. Prihvaćen poziv u postojeću firmu nije nova firma i ne šalje email.
- Sledeće: stranica samo za vlasnika, sa brojevima i tabelom firmi (artikli, povezana prodavnica, poslednje kretanje). Prikazuje samo to, ne sadržaj podataka firme.
