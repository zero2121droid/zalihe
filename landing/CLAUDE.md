# Lagerko: landing strana

Ovaj folder sadrži javnu stranu za `lagerko.com`. To je jedna statička strana, odvojena od aplikacije. Fajl je samostalan: sve što treba za izradu je ovde, pa folder može da se premesti i van repoa aplikacije.

## Proizvod

**Lagerko** je veb aplikacija za praćenje zaliha za male firme. Prva ciljna grupa su mali WooCommerce webshopovi u Srbiji i regionu.

**Glavna poruka:** „Znaj koliko robe stvarno imaš”. Nikad „sinhronizacija sa WooCommerce-om”. WooCommerce je način ulaska, a vrednost je u svemu van prodavnice.

**Kome se obraćamo:** vlasniku webshopa sa stotinama artikala i varijacija, koji prodaje i van sajta (Instagram, telefon, lokal), redovno prima robu, ima zaposlene i uz WooCommerce vodi Excel.

**Kome se ne obraćamo:** prodavnici koja prodaje samo preko sajta i gde vlasnik sve radi sam. Njoj je WooCommerce dovoljan.

**Jedan cilj strane:** posetilac pravi nalog. Sve ostalo je sporedno.

## Tehnika

- Čist HTML, CSS i malo običnog JavaScripta. Bez frameworka, build alata i npm zavisnosti.
- Struktura:

```
landing/
  index.html
  styles.css
  script.js            prekidač teme, animacija u heroju i Excel scena
  assets/
    fonts/             IBM Plex Sans i Mono, woff2, samo latinica i latin-ext
    images/            pravi snimci ekrana, webp
    favicon.svg
    og.png             1200 × 630
  robots.txt
  sitemap.xml
```

- Strana radi i bez JavaScripta. Sadržaj je u samom HTML-u, jer strana mora da bude indeksirana i brza.
- Fontovi su na našem serveru, ne na Google Fonts, da podaci posetilaca ne idu trećoj strani.
- Bez kolačića, analitike i piksela dok vlasnik projekta to posebno ne odobri.
- Deploy je mimo aplikacije: Nginx direktno servira ovaj folder. Izrada strane ne dira aplikaciju, njen build ni deploy.

## Adrese

- registracija: `https://app.lagerko.com/register`
- prijava: `https://app.lagerko.com/login`
- privatnost: `https://app.lagerko.com/privacy`

Kanonska adresa strane je `https://lagerko.com/`.

## Jezik i ton

- Samo `sr-Latn` u prvoj verziji, `<html lang="sr-Latn">`. Engleska verzija dolazi kad se pojave kupci van regiona.
- Oslovljavanje na „ti”, kao u aplikaciji. Bez muškog ili ženskog roda u tekstu.
- Kratko, konkretno i mirno. Bez reči „revolucionarno”, „moćno”, „rešenje”, „sveobuhvatno”.
- Primeri su iz stvarnog posla: veličine, prijem robe, popis, porudžbina sa Instagrama.
- Formati: `1.284.350 RSD`, `12,5`, `−2` (pravi minus) i `+24`.

## Sekcije, po redu

Tekstovi ispod su polazna verzija. Sme da se doteruje ton, ali ne i obećanja.

### 1. Zaglavlje

Logo **lagerko**, a desno „Prijava” i primarno dugme „Napravi nalog”. Na telefonu ostaju logo i dugme, a „Prijava” je kao link. Bez menija sa mnogo stavki.

### 2. Hero

- **Naslov:** Znaj koliko robe stvarno imaš.
- **Podnaslov:** Sajt, Instagram, telefon i lokal na jednom mestu. Lagerko vodi stanje za svaki artikal i kaže ti šta treba da naručiš.
- **Dugme:** Napravi besplatan nalog
- **Ispod dugmeta:** Bez kartice. Prvih 10 firmi dobija osnivački popust.
- **Vizuelno:** mali živi prikaz umesto velike slike. Red artikla „Majica Nit · M · plava”, porudžbina stiže i iznad broja kratko iskoči `−2`, broj pređe sa `31` na `29`, a pored piše „Isto stanje je u prodavnici”. Pravi se u HTML-u i CSS-u, u tokenima ispod. Uz `prefers-reduced-motion` stoji mirno krajnje stanje.

### 2a. Excel scena

Odmah ispod heroja, primer C iz artifacta „Lagerko skrol scene”. Skrol vodi kroz pet koraka u kojima `lager_FINAL_novo(2).xlsx` postaje spisak artikala u Lagerku. Tekstovi koraka i gotova tabela su u HTML-u, a `script.js` ih pretvara u zakačenu scenu. Bez JavaScripta i uz `prefers-reduced-motion` to je obična lista pored gotove tabele.

### 3. Problem

**Naslov:** Excel je tačan samo dok neko ne proda nešto.

Tri kratka bloka:

- **Prodaš nešto čega nema.** Na sajtu piše da ima, a na polici je prazno. Sledi poziv kupcu i izvinjenje.
- **Stanje je u više glava.** Sajt ima svoj broj, Excel svoj, a poruke sa Instagrama nisu nigde.
- **Naručuješ napamet.** Ono što se prodaje ponestane, a ono što stoji vezuje novac.

### 4. Kako radi

**Naslov:** Tri koraka do tačnog stanja.

1. **Poveži prodavnicu.** Odobriš pristup u WooCommerce-u, bez kopiranja ključeva. Proizvodi i varijacije se uvoze sa pregledom pre potvrde.
2. **Upiši šta se dešava van sajta.** Prijem robe, popis i prodaja preko Instagrama, telefona ili u lokalu.
3. **Gledaj jedan broj.** Porudžbine skidaju robu sa stanja, a novo stanje ide nazad u prodavnicu.

### 5. Šta dobijaš

Mreža sa šest stavki, svaka sa linijskom ikonom, naslovom i jednom rečenicom. Uz bar dve stavke ide pravi snimak ekrana.

- **Prodaja van sajta.** Ručna porudžbina skida robu sa stanja, pa sajt ne prodaje ono što je otišlo preko Instagrama.
- **Prijem robe.** Dobavljač, broj fakture i stavke na jednom mestu, sa nabavnom cenom ako je želiš.
- **Upozorenja za minimum.** Vidiš šta je ispod minimuma, a jednom dnevno stiže i email sa spiskom.
- **Izveštaji za naručivanje.** Najprodavaniji artikli i roba koja stoji, zajedno sa vrednošću zaliha.
- **Istorija svake promene.** Za svaki artikal znaš ko je, kada i zašto promenio stanje.
- **Zaposleni bez wp-admina.** Svako dobija svoju ulogu, a nabavne cene vidi samo ko sme.

### 6. Dokaz

Ova sekcija se **ne objavljuje** dok ne postoji prava izjava prave prodavnice, sa dozvolom. Do tada je nema ni u HTML-u, ni kao prazno mesto. Kad dođe, sadrži ime, firmu, fotografiju i konkretan rezultat.

### 7. Cena

**Naslov:** Besplatno dok traje beta.

Tekst: Probaj Lagerko na svojoj prodavnici bez kartice. Pre bilo kakve naplate znaćeš tačnu cenu, a prvih 10 firmi dobija osnivački popust.

Iznosi planova se ne objavljuju dok ih vlasnik projekta ne potvrdi.

### 8. Česta pitanja

Otvaraju se preko `<details>`, bez JavaScripta.

- **Radi li bez web prodavnice?** Radi. Artikle možeš da uneseš ručno ili uvezeš iz CSV fajla.
- **Radi li sa Shopify-jem?** Još ne. Za sada se povezuje samo WooCommerce.
- **Mogu li da koristim Lagerko umesto kase ili knjigovodstva?** Ne. Lagerko vodi robu. Ne izdaje fiskalne račune i nije knjigovodstveni program.
- **Čuvate li podatke mojih kupaca?** Ne. Iz porudžbina uzimamo ono što je potrebno za stanje, bez imena, adrese, telefona i emaila kupca.
- **Šta ako Lagerko privremeno ne radi?** Prodavnica nastavlja da radi, jer WooCommerce sam smanjuje stanje. Lagerko posle preuzme ono što je propustio.
- **Da li radi na telefonu?** Radi u browseru na računaru i telefonu. Posebna mobilna aplikacija još ne postoji.

### 9. Završni poziv

**Naslov:** Saznaj koliko robe stvarno imaš.

Dugme: Napravi besplatan nalog

### 10. Podnožje

Logo, kontakt email, Politika privatnosti, Prijava i godina. Kontakt email je pravi, nikad izmišljen.

## Činjenice

Sme da se tvrdi samo ono što aplikacija zaista radi.

**Postoji:** povezivanje WooCommerce prodavnice, uvoz proizvoda i varijacija, automatsko skidanje sa stanja i vraćanje pri otkazivanju, slanje stanja u prodavnicu, ručne porudžbine, prijem robe, korekcije i popis, CSV uvoz, minimalne zalihe i email upozorenja, izveštaji, istorija, uloge (admin, menadžer, zaposleni), skrivanje nabavnih cena, srpski i engleski jezik, tamna i svetla tema.

**Ne obećavati:** Shopify, više magacina, mobilnu aplikaciju, fiskalizaciju, SEF i e-fakture, knjigovodstvo, čitanje faktura pomoću AI-ja, AI predviđanja, izvoz podataka, porudžbenice dobavljačima, lotove i rokove trajanja.

**Skeniranje barkoda** je za sada proba u brzoj pretrazi. Ne ističe se kao gotova funkcija.

**Nikad ne izmišljati:** izjave korisnika, ocene, broj korisnika, logotipe klijenata, procente uštede ni „već koristi X firmi”.

Ako tvrdnja nije na listi „Postoji”, proveri aplikaciju ili pitaj vlasnika projekta.

## Dizajn

Karakter je **miran i precizan, kao dobar alat**. Strana treba da izgleda kao nastavak aplikacije, a ne kao tipičan SaaS šablon. Glavni vizuelni sadržaj su brojevi i pravi ekrani.

### Tokeni

Boje su CSS varijable u `:root`. Hex vrednosti postoje samo tu. Tamna tema je podrazumevana, prekidač je u zaglavlju, a izbor se pamti u `localStorage` (obuhvaćeno sa `try/catch`).

| Token | Tamna | Svetla | Upotreba |
|---|---|---|---|
| `--bg` | `#11130E` | `#F5F5EF` | pozadina strane |
| `--surface` | `#191C15` | `#FFFFFF` | sekcije i blokovi |
| `--surface-2` | `#20241B` | `#E9EBE1` | sporedne površine |
| `--selected` | `#252B1C` | `#E1E7CF` | istaknuto stanje |
| `--line` | `#2C3125` | `#D9DCCE` | okviri sekcija |
| `--line-soft` | `#23271E` | `#E7E9DF` | linije između redova |
| `--line-strong` | `#3A4030` | `#BFC4B0` | okvir sekundarnog dugmeta |
| `--text` | `#E4E7DC` | `#1B1E16` | glavni tekst |
| `--text-2` | `#C4C9B8` | `#3B4033` | sekundarni tekst |
| `--muted` | `#9BA18F` | `#5C6250` | opisi i oznake |
| `--accent` | `#B4C17E` | `#B4C17E` | primarno dugme |
| `--accent-hover` | `#CBD69A` | `#A3B16A` | hover primarnog dugmeta |
| `--accent-text` | `#B4C17E` | `#55621E` | linkovi i fokus okvir |
| `--on-accent` | `#11130E` | `#11130E` | tekst na primarnom dugmetu |
| `--status-ok` | `#9DB06A` | `#4C6A1C` | na stanju |
| `--status-low` | `#E0A84A` | `#94600A` | ispod minimuma |
| `--status-out` | `#E8806A` | `#B03A22` | nema na stanju |

Status je uvek boja i tekst, nikad samo boja.

### Tipografija

- **IBM Plex Sans** za tekst. Fallback: `'Segoe UI', system-ui, sans-serif`.
- **IBM Plex Mono** za brojeve, šifre, oznake sekcija i logo. Fallback: `ui-monospace, monospace`.
- Hero naslov je 44-72 px sa `clamp()`, a naslovi sekcija 26-36 px, težina 600.
- Tekst je 16-18 px, sa širinom reda do oko 65 znakova.
- Oznaka sekcije: mono, 11 px, VELIKA SLOVA, razmak `0.12em`.
- Brojevi: `font-variant-numeric: tabular-nums`.

### Oblik

- Zaobljenja: 4 px za dugmad, 6 px za sekcije.
- Sekcije se odvajaju okvirom od 1 px, bez senki.
- Razmaci po skali od 4 px.
- Sadržaj najviše 1180 px, centriran.
- Dugmad 40 px, na telefonu najmanje 44 px, a glavna akcija na telefonu 56 px.
- Linijske ikone, debljina 1.8, iz jedne biblioteke (Tabler Icons), kao inline SVG.

### Logo

Znak je kutija sa poklopcem i dve oči. Naziv je **lagerko**, malim slovima, u IBM Plex Mono. Putanja znaka u okviru 16 × 16:

```svg
<path fill-rule="evenodd" d="M0 1h16v4H0z M1 6h14v9H1z M4.5 8.5h2v3.5h-2z M9.5 8.5h2v3.5h-2z"/>
```

Oči su rupe, pa se kroz njih vidi pozadina. Boja znaka je `--accent-text`.

### Pokret

Tih, kratak i uvek sa značenjem. Dozvoljeni su: `−2` koji iskoči, broj koji pređe na novu vrednost, kvačica koja se iscrta, logo kao na prijavi u aplikaciji (kutija i poklopac padnu, oči se otvore, povremeno pogleda levo-desno i trepne, poklopac skoči na hover), crteži iz učitavanja aplikacije koji se odigraju jednom kad dođu na ekran i Excel scena koju vodi skrol. Bez paralakse, ulazaka sekcija pri skrolovanju i petlji koje odvlače pažnju. Uz `prefers-reduced-motion` sve staje.

### Zabranjeno

- gradijenti, glassmorphism i senke kao dekoracija
- emoji umesto ikona
- kartice sa obojenom levom ivicom
- Inter, Roboto, Arial
- ilustracije, maskote, stock fotografije i AI slike ljudi
- beli tekst na srednje tamnim bojama
- izmišljeni snimci ekrana

## Slike

Snimci ekrana su pravi, iz aplikacije sa demo podacima prodavnice Nit, bez ličnih podataka. Prave se u obe teme ako strana prikazuje obe. Dok ne postoje, u kodu ostaje komentar `<!-- TODO: snimak ... -->`, a ne izmišljen ekran.

Prioritet: stanje artikala, detalj artikla sa grafikom i istorijom, prodaja na telefonu, izveštaji.

## SEO i kvalitet

- Jedan `h1`, smislena hijerarhija naslova i semantički `<header>`, `<main>`, `<section>`, `<footer>`.
- `title`: „Lagerko: praćenje zaliha za WooCommerce prodavnice”.
- `meta description`: „Znaj koliko robe stvarno imaš, gde god da prodaješ. Sajt, Instagram, telefon i lokal na jednom mestu.”
- `canonical`, Open Graph i `og.png`.
- JSON-LD `SoftwareApplication`, bez izmišljenih ocena.
- `robots.txt` i `sitemap.xml`.
- Kontrast teksta najmanje 4.5:1, vidljiv fokus od 2 px, a sve radi sa tastature.
- Slike imaju `alt`, `width`, `height` i `loading="lazy"`, osim onih u prvom ekranu.
- Bez horizontalnog skrolovanja od 320 px naviše.
- Cilj je manje od 300 KB za prvi prikaz, bez snimaka niže na strani.

## Pre završetka

1. Proveri širine 320, 390, 768, 1280 i 1920 px, u obe teme.
2. Proveri sve linkove.
3. Proveri rad bez JavaScripta i uz `prefers-reduced-motion`.
4. Prođi tekst kroz listu „Činjenice”.
5. Proveri Lighthouse za mobilni prikaz.

## Otvoreno

Ovo odlučuje vlasnik projekta. Dok ne odluči, važi navedeno podrazumevano ponašanje.

| Pitanje | Podrazumevano |
|---|---|
| Kontakt email | Bez objave, u kodu `TODO` |
| Objava iznosa planova | Bez iznosa, samo tekst o beti |
| Analitika | Bez analitike |
| Engleska verzija | Samo `sr-Latn` |
| Kratak video proizvoda | Bez videa, dok ne bude snimljen |
