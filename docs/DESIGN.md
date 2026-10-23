# Zalihe: dizajn

Reference: makete ekrana Početna, Artikli i Prodaja van sajta (telefon). Kad se ovaj dokument i maketa razlikuju, važi ovaj dokument.

## Karakter

**Miran i precizan, kao dobar alat.** Aplikacija se koristi svaki dan, brzo i bez razmišljanja. Ništa ne viče, osim onoga što stvarno traži pažnju (artikal ispod minimuma ili bez zaliha). Brojevi su glavni sadržaj i najvidljiviji element na ekranu.

## Boje

Dve teme: **tamna** (podrazumevana) i **svetla**. Korisnik bira temu prekidačem u bočnoj navigaciji ili na stranici za prijavu, a izbor se pamti na uređaju (u browseru), ne na nalogu. Boje se definišu isključivo kao tokeni (CSS varijable i Mantine tema), nikad kao hex vrednosti u komponentama.

| Token | Tamna | Svetla | Upotreba |
|---|---|---|---|
| `bg` | `#11130E` | `#F5F5EF` | pozadina stranice |
| `sidebar` | `#141710` | `#EDEEE6` | bočna navigacija |
| `surface` | `#191C15` | `#FFFFFF` | sekcije, tabele, polja za unos, modal |
| `surface-2` | `#20241B` | `#E9EBE1` | aktivna stavka navigacije, dugmad za količinu |
| `selected` | `#252B1C` | `#E1E7CF` | izabrani filter ili opcija |
| `line` | `#2C3125` | `#D9DCCE` | okviri sekcija |
| `line-soft` | `#23271E` | `#E7E9DF` | linije između redova |
| `line-strong` | `#3A4030` | `#BFC4B0` | okvir sekundarnog dugmeta i istaknute kartice |
| `text` | `#E4E7DC` | `#1B1E16` | glavni tekst |
| `text-2` | `#C4C9B8` | `#3B4033` | sekundarni tekst u tabelama |
| `muted` | `#9BA18F` | `#5C6250` | opisi, oznake, jedinice mere |
| `accent` | `#B4C17E` | `#B4C17E` | primarno dugme (tamni tekst na njemu) |
| `accent-hover` | `#CBD69A` | `#A3B16A` | hover za primarno dugme |
| `accent-text` | `#B4C17E` | `#55621E` | linkovi, ikona aktivne stavke, fokus okvir, znak u logou |
| `on-accent` | `#11130E` | `#11130E` | tekst na primarnom dugmetu |

`accent-text` postoji zato što svetla maslinasta nije čitljiva kao tekst na beloj pozadini (kontrast 1,9). U tamnoj temi je isti kao `accent`. Hover za linkove: u tamnoj temi `accent-hover`, u svetloj `text`.

**Status zaliha** (uvek boja + tekst, nikad samo boja):

| Status | Tamna | Svetla | Upotreba |
|---|---|---|---|
| Na stanju | `#9DB06A` | `#4C6A1C` | tačka statusa; broj ostaje `text` |
| Ispod minimuma | `#E0A84A` | `#94600A` | broj, tačka i oznaka |
| Nema na stanju | `#E8806A` | `#B03A22` | broj, tačka i oznaka |
| Prijem i povrat | `#B4C17E` | `#55621E` | `+` količine u kretanjima |

Statusne boje svetle teme su tamnije od tamne teme jer se koriste kao tekst; sve imaju kontrast najmanje 5:1 na `surface`.

Primarno dugme u obe teme ima svetlu maslinastu pozadinu i tamni tekst (`on-accent`), ne beli tekst na tamnoj zelenoj.

## Tipografija

- **IBM Plex Sans** za sav tekst interfejsa. Ima punu podršku za č, ć, š, ž, đ.
- **IBM Plex Mono** za brojeve količina, šifre (SKU), vremena, oznake sekcija i logo.
- Fallback: `'Segoe UI', system-ui, sans-serif` i `ui-monospace, monospace`.
- Svi brojevi u tabelama: `font-variant-numeric: tabular-nums`, poravnati desno.

| Uloga | Veličina | Težina |
|---|---|---|
| Naslov ekrana | 26 px | 600 |
| Naslov sekcije | 15 px | 600 |
| Tekst | 14 px (telefon 15 px) | 400 / 500 |
| Opis, oznaka | 12 px | 400 |
| Oznaka sekcije (mono, VELIKA SLOVA, razmak 0.12em) | 11 px | 400 |
| Broj u pregledu (KPI) | 26 px mono | 500 |
| Količina u tabeli | 18 px mono | 500 |
| Količina pri unosu (telefon) | 48 px mono | 500 |

Jedinica mere stoji uz broj, manjom veličinom i `muted` bojom: **31** kom.

## Brojevi i formati

- Srpski format: `1.284.350` za hiljade, `12,5` za decimale. Formatiranje preko `Intl`, nikad ručno.
- Prodaja i izlaz: `−2` (pravi znak minus `−`, ne crtica). Prijem i povrat: `+24`.
- Novac uz oznaku valute manjim slovima: `1.284.350 RSD`.

## Oblik i raspored

- Zaobljenja: 4 px za dugmad i polja, 6 px za sekcije, 16 px samo za filter pilule.
- Sekcije se odvajaju **okvirom od 1 px**, bez senki.
- Pregled (KPI) je jedna traka podeljena linijama, ne niz zasebnih kartica.
- Razmaci po skali od 4 px: 4, 8, 12, 16, 20, 24, 28, 36.
- Bočna navigacija 232 px. Raspored je fluidan: sadržaj koristi raspoloživu širinu do granice koja zavisi od vrste stranice, i uvek je poravnat uz navigaciju (levo).
  - ekrani sa tabelama i pregledima (Početna, Artikli, Uvoz, Kretanja, Izveštaji): najviše 1920 px
  - stranice sa tekstom i formama (npr. podešavanja): najviše 1180 px
  - prijava i registracija: uska kolona od 400 px
  - modali: centrirani na ekranu, 760 px, a na ekranima širim od 1920 px 880 px; na telefonu cela širina, a viši sadržaj se pomera unutar modala. Forma u modalu ima najviše dve kolone, jer se razvučena polja teško popunjavaju
- Veličina teksta i elemenata ne zavisi od širine ekrana. Uvećanje je posao sistema i browsera (zum), da se ne bi sabralo sa njim.
- Visina dugmadi 40 px na računaru, minimum 44 px na telefonu, 56 px za glavnu akciju na telefonu.

### Telefon i tablet

- Meni na telefonu je traka na dnu ekrana (64 px, `sidebar`, linija `line` iznad): Početna, Artikli, Porudžbine i Više. Trenutna stavka ima tekst `text` i ikonu `accent-text`. „Više“ otvara listu odozdo sa ostalim ekranima, firmom, korisnikom, temom i odjavom (`PhoneTabs`). Gornja traka ima logo i lupu za brzu pretragu; burger meni ne postoji.
- Visine kontrola idu preko tokena `--z-control` (dugmad i polja: 40 px, na telefonu 44 px) i `--z-icon-button` (36 / 44 px) u `global.css`, nikad fiksno u komponenti. Straničenje je 44 px na telefonu; filter pilule ostaju 32 px, ali primaju dodir u visini od 44 px.
- Na telefonu se veći razmaci smanjuju za jedan korak: `lg` 24 → 16 px, `xl` 36 → 24 px.
- Tabele ne skroluju u stranu: gde je prostor za sadržaj uzak (ispod `md`, a lista artikala ispod `lg`), tabela prelazi na sažet raspored (`useCompactTables`). Ostaju naziv i glavni broj, a šifra, status i ostalo idu ispod naziva. Tabele u modalima i u uvozu na telefonu postaju liste blokova.
- KPI traka (`z-kpi`) je na telefonu jedna kolona: oznaka levo, broj desno, jer dve kolone ne primaju veće iznose, a tekst se ne smanjuje.
- Modal na telefonu zauzima ceo ekran. Red sa dugmadi (`z-modal-actions`) je zalepljen za dno, „Otkaži“ se krije (isto radi X), a glavna akcija je 56 px.
- Unos količine na telefonu je kao u maketi „Prodaja van sajta“: − levo, broj 48 px mono u sredini, + desno (56 px).
- Red dugmadi koji na telefonu treba da bude kolona dugmadi pune širine dobija klasu `z-phone-stack`; radnje na artiklu su mreža 2 × 2.

## Ikone

Linijske ikone, debljina 1.8, 16 px u navigaciji i 18-20 px na telefonu. Biblioteka: Tabler Icons ili Lucide, jedna, dosledno. Dugme samo sa ikonom uvek ima `aria-label`.

## Pokret

Animacije su tihe i kratke i uvek nešto znače. Pravljene su samo od SVG-a i CSS-a, bez biblioteke.

- Linijske ikone debljine 1.8 u bojama tokena, nikad scene ni ilustracije.
- Učitavanje: podrazumevani `Loader` su stubići na polici (isti se koristi i u dugmadima). Sve kutije u animacijama su kutija iz logoa (puna, poklopac odvojen procepom, oči kao rupe; `logoMark.ts`). Za celu stranicu služe kutije koje se slažu (iste kao kutija praznog stanja), za tabele redovi koji „dišu“ (samo providnost, bez preliva), a kad se otvara jedna stvar (artikal, spisak proizvoda iz prodavnice) kutija koja se otvara. Pretraga ne sklanja listu: stari rezultati ostaju, a u polju za pretragu se vrte mali stubići dok ne stignu novi. Loader se ne prikazuje ako odgovor stigne za manje od 200 ms, a kad se jednom pojavi, ostaje najmanje 500 ms, da ne bljesne (`useLoadingDisplay`).
- Pretraga u listi: dok se kuca, redovi trenutne strane koji ne sadrže ukucani tekst (naziv, šifra, barkod) se skupe (0,3 s), a ukucani deo naziva i šifre se istakne (pozadina `selected`, tekst `accent-text`). Kad stignu rezultati sa servera, oni zamene listu (`SearchMatch`, `SearchRow` u `search.tsx`).
- Promena stanja: broj glatko pređe na novu vrednost (oko 0,5 s), a iznad njega kratko iskoči `+24` ili `−2`. Novo kretanje u istoriji se kratko istakne.
- Promena statusa (stanje padne ispod minimuma, ostane bez zaliha ili se vrati iznad minimuma): tačka statusa iskoči uz malu oprugu (0,45 s), a novi tekst uklizi odozdo (0,3 s), u istom trenutku kad iskoči `−2` (`StockStatusLabel animate`, na Artiklima i detalju artikla).
- Slanje u prodavnicu (detalj artikla, ispod statusa, samo za artikal povezan sa prodavnicom): dok promena čeka slanje (oko 30 s), strelice se vrte i piše „Šalje se u prodavnicu“ (`accent-text`); kad prodavnica primi stanje, iscrta se kvačica i piše „Isto stanje je u prodavnici“ (`muted`). Prikazuje se samo ono što API javi (`ShopStockStatus`).
- Unos količine (prijem, prodaja, povrat, korekcija): pored polja su dugmad − i + (`surface-2`, 40 px, na telefonu 44 px), jedan korak po pritisku, a dok se drži, menja se brže. „Stanje posle” se ne prepisuje odjednom: svaka cifra se zavrti do nove vrednosti kao brojčanik (0,6 s, `Odometer`); separatori i znak stoje.
- Filter pilule (Artikli, Porudžbine, Izveštaji, istorija artikla): izbor (pozadina `selected`, okvir `accent`) otklizi i promeni širinu do izabrane pilule (0,35 s), umesto da skoči (`FilterPills`). Izabrani izveštaj se pri promeni blago podigne na mesto (0,3 s).
- Šifra na detalju artikla se kopira klikom: ikona kopiranja se uz malu oprugu zameni kvačicom, a pozadina postane `selected` (1,6 s); šifra ostaje, pa se ništa pored nje ne pomera (`CopySku`).
- Sortiranje po koloni (Artikli): klik na naslov kolone sortira po njoj (nazivi od A, brojevi od najvećeg), drugi klik okreće redosled; strelica pokazuje smer. Redovi koji ostanu na strani otklizaju sa starog mesta na novo (0,45 s, jedan za drugim po 25 ms) umesto da skoče; dok klize, neprozirni su, a onaj koji putuje dalje ide preko ostalih (`useFlipRows`).
- Prekidač: palac pri prebacivanju malo pređe cilj i vrati se, kao opruga (0,3 s).
- Veza sa prodavnicom (Kanali prodaje) je uvek ikona i tekst: dok traje provera, strelice se vrte; kad provera uspe, kvačica se iscrta; dok veza ne radi, uzvičnik se povremeno malo pomeri (`ChannelStatus`).
- Red koji ispadne iz liste zbog akcije korisnika (porudžbina iz filtera „Nepovezani“ kad se njeni proizvodi povežu, opozvan poziv u Podešavanjima) se skupi (0,3 s), kao kod pretrage, umesto da nestane naglo (`useLeaving`). Broj nepovezanih porudžbina na pilulama se zavrti kao brojčanik (`Odometer`).
- Novi poziv u Podešavanjima se kratko istakne (kao novo kretanje). „Pošalji ponovo“ posle slanja pokaže kvačicu koja se iscrta i „Poslato“ (1,6 s), pa se vrati.
- Navigacija: pozadina trenutne stranice (`surface-2`) otklizi do nove stavke (0,3 s), kao izbor na filter pilulama (`useSlidingBox`).
- Straničenje: redovi nove strane uđu sa 8 px pomaka sa strane na koju se ide (sledeća zdesna, prethodna sleva, 0,25 s), tek kad stigne odgovor (`PageTurn`).
- Prekidač teme: sunce se okrene i smanji, a mesec dođe na njegovo mesto (0,35 s), i obrnuto.
- Kad nešto nestane sa ekrana, a može da se vrati (deaktiviran artikal), dole u sredini se pojavi poruka sa „Poništi” (pozadina `text`, tekst `bg`). Linija `accent` ispod nje se skraćuje 5 s, koliko traje mogućnost poništavanja, i staje dok je pokazivač ili fokus na poruci (`UndoToast`).
- Uvoz iz prodavnice: lista ostaje na mestu, ali izbledi (30%) i zaključa se, a animacija uvoza stoji u sredini ekrana na sekciji (`surface`, okvir od 1 px). Bez zamagljivanja (vidi „Zabranjeno“: glassmorphism). Kad stigne rezultat, broj uvezenih proizvoda odbroji jedan po jedan do poslednjeg (oko 30 ms po broju, 0,6-3 s), paketi stanu, a animacija ostaje dok je korisnik ne zatvori na X (ili Escape). Uvoz je jedan zahtev, pa se broji tek kad je poznat stvaran broj, nikad izmišljen napredak.
- CSV uvoz: kad se polju izabere kolona, ta kolona u uzorku kratko zasvetli (`selected`, 0,9 s). Brojevi u pregledu (spremno, greške, preskočeno) odbroje od 0, redovi sa problemima se pojave jedan za drugim (posle dvanaestog zajedno), a na kraju se broj uvezenih artikala odbroji kao kod uvoza iz prodavnice.
- Uvoz ima i tanku traku napretka (4 px, `accent`) koja se puni uz brojač, a kad se završi, skladište na animaciji dobije `accent-text` boju.
- Čuvanje forme: dugme prvo pokaže stubiće, a kad uspe, iscrta se kvačica i piše „Sačuvano“ (pozadina `selected`, tekst `accent-text`), oko 0,6 s, pa se forma zatvori (`SaveButton`, `useSaved`).
- Greška u formi: polje sa greškom se kratko protrese (0,36 s) na svaki neuspeo pokušaj slanja, a poruka ispod kaže šta da se ispravi (`useShake`).
- Početna: brojevi u pregledu odbroje od 0 do svoje vrednosti (oko 0,5 s), a artikli ispod minimuma se pojave jedan za drugim.
- Pulsira samo ono što traži pažnju (status ispod minimuma ili bez zaliha), i to samo na detalju artikla, ne u listama.
- Uz `prefers-reduced-motion` sve animacije staju, a brojevi se menjaju odmah. Mirno stanje mora biti čitljivo bez animacije.
- Komponente: `src/web/src/components/loaders.tsx`, `stock.tsx`, `feedback.tsx`, `Odometer.tsx`, `StepButton.tsx`, `FilterPill.tsx`, `CopySku.tsx`, `UndoToast.tsx`, `ShopStockStatus.tsx`, `PageTurn.tsx` i `ThemeToggle.tsx`. Sve se mogu videti na `/dev/animacije` (samo u razvoju).

## Zabranjeno

- gradijenti, glassmorphism, senke kao dekoracija
- emoji umesto ikona
- kartice sa obojenom levom ivicom
- Inter, Roboto, Arial
- podrazumevana Mantine tema bez prilagođavanja
- beli tekst na srednje tamnim bojama (loš kontrast)
- status prikazan samo bojom, bez teksta
- ilustracije i maskote

## Prijava i registracija

- Prijava, registracija, zaboravljena i nova lozinka i prihvatanje poziva imaju isti raspored (`AuthLayout`): sadržaj najviše 1180 px, centriran na ekranu; forma levo (najviše 420 px), a desno znak i naziv **lagerko** u jednom redu, krupno (znak 96 px, naziv 80 px mono), na pozadini `bg`, bez kartice i bez teksta ispod naziva. Jezik i tema su gore desno.
- Naslov strane je 44 px (na telefonu 30 px), klasa `z-auth-title`. Bez muškog ili ženskog roda u tekstu („Prijava“, ne „Dobro došao“).
- Na prijavi su „Zapamti me na ovom uređaju“ (levo) i „Zaboravljena lozinka?“ (desno) u jednom redu ispod lozinke, tim redom i za Tab.
- Ispod `md` veliki znak se sklanja, a običan logo ide gore levo, iznad forme.

## Uvoz iz prodavnice: razlike

- Ispod novih i povezivanih proizvoda je sekcija „Razlike sa prodavnicom“: za svaki artikal naziv (link) i šifra, a ispod polje za potvrdu po izmeni, „Prodajna cena: 4.000 RSD → **500 RSD**“ (staro `muted` naziv polja, novo podebljano). Promena cene veća od dvostruke dobija „· velika razlika“ u `status-low`. Unapred su štiklirana samo polja koja su u Lagerku prazna.
- Kad nema novih ni povezivanih proizvoda, ispod je dugme „Primeni izabrane izmene“, bez animacije uvoza; posle njega se prodavnica pročita ponovo i lista pokaže samo ono što se još razlikuje.

## Brza pretraga

- Ctrl+K (⌘K na Macu), dugme „Nađi artikal“ u meniju ili lupa na telefonu otvaraju prozor sa poljem (56 px) i do osam artikala. Svaki rezultat pokazuje stanje i status, pa često ne mora da se otvara. Strelice biraju (izabran red: `selected` i okvir `accent`), Enter otvara artikal, Esc zatvara. Na telefonu prozor zauzima ceo ekran, pa umesto oznake Esc u polju stoji X (`--z-icon-button`). Ukucani tekst se istakne kao u pretrazi liste (`QuickSearch`). Artikal čija je cela šifra ili barkod jednak ukucanom tekstu je uvek prvi, pa čitač barkoda (USB, Bluetooth) radi bez ičeg posebnog: otkuca kod i Enter.
- Skeniranje kamerom (proba, SPEC 2.8): gde postoji kamera, pored polja je ikona kamere. Umesto liste se pojavi slika zadnje kamere sa okvirom (`accent`, 2 px) i linijom koja ide gore-dole dok traži kod; ispod je „Usmeri kameru na barkod.“ i „Otkaži“. Pročitan kod se upiše u polje, a kad tačno odgovara barkodu ili šifri artikla, artikal se odmah otvori. Kamera se gasi čim pročita kod ili se prozor zatvori (`BarcodeScanner`).

## Početna

- Kad porudžbine iz prodavnice imaju nepovezane proizvode, iznad brojki stoji traka kao kod prodavnice koja ne radi (okvir `line-strong`, tačka i naslov `status-low`): koliko porudžbina, zašto je stanje pogrešno i dugme „Poveži proizvode“ ka filteru „Nepovezani“. Bez takvih porudžbina traka ne postoji.

## Prvi koraci

- Na Početnoj, dok firma nema artikle ili nijedan minimum, stoji sekcija „Prvi koraci“: dodaj artikle, postavi minimalne zalihe, a treći korak, povezivanje WooCommerce prodavnice, nosi oznaku „nije obavezno“, jer firma bez web prodavnice radi ručno ili iz CSV fajla. Završen korak je precrtan, sa kvačicom u krugu (`selected`, okvir `accent`). Napredak „1 od 2“ i traka od 4 px broje samo obavezne korake. Kad su oba gotova, sekcija nestaje.

## Grafik stanja

- Na detalju artikla, između brojki i istorije: stepenasta linija stanja (2 px, `accent`) za 30 ili 90 dana (filter pilule), isprekidana linija minimuma (`status-low`) sa oznakom „minimum 10“, a vreme na ili ispod minimuma je osenčeno `status-low` sa 12% providnosti i objašnjeno ispod grafika. Jedna serija, pa bez legende. Prelaz preko grafika pokazuje dan i stanje. Ose su tihe: tri datuma dole (`muted`, 11 px mono), poslednji je „danas“.

## Kartica prodavnice

- Ispod statusa veze je traka od četiri brojke (kao `z-kpi`): povezani proizvodi, porudžbine sa nepovezanim proizvodima (broj u `status-low` i link „Pogledaj porudžbine“ kad ih ima), poslednja porudžbina koja je stigla (vreme i broj) i slanje stanja („Ništa ne čeka slanje“ ili koliko proizvoda čeka).

## Podešavanja

- Levo podmeni (220 px), desno samo izabrani deo, u jednoj sekciji. Delovi su grupisani oznakama sekcije: **Firma** (Naziv firme, Korisnici), **Moj nalog** (Ime i jezik, Lozinka), **Održavanje** (Provera stanja). Novi deo (npr. uloge) se dodaje kao stavka u grupi, ne kao još jedna sekcija na stranici.
- Svaki deo ima svoju adresu (`/settings/company`, `/settings/users`, `/settings/profile`, `/settings/password`, `/settings/stock-check`); `/settings` i nepoznata adresa otvaraju Naziv firme.
- Izabrana stavka ima pozadinu `selected` i okvir `accent`, kao filter pilula, i to otklizi do nove stavke (0,3 s); izabrani deo se pri promeni blago podigne na mesto (0,3 s).
- Na telefonu je podmeni jedan red pilula iznad dela (bez oznaka grupa), visine `--z-control`.

## Pristupačnost

- Kontrast teksta najmanje 4.5:1 u obe teme. `muted` se koristi samo na `bg`, `sidebar` i `surface`.
- Pravi `<button>`, `<a>`, `<input>` sa `<label>`, nikad `onClick` na `div`.
- Vidljiv fokus: okvir `accent-text` boje od 2 px.

## Mantine

Tema se podešava jednom, u `web/src/theme.ts`: boje iz tabele iznad kao tokeni za obe teme, fontovi, zaobljenja, i podrazumevani stilovi za `Button`, `TextInput`, `Table` i `Badge`. Komponente koriste temu i ne zadaju sopstvene boje.
