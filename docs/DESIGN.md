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

## Ikone

Linijske ikone, debljina 1.8, 16 px u navigaciji i 18-20 px na telefonu. Biblioteka: Tabler Icons ili Lucide, jedna, dosledno. Dugme samo sa ikonom uvek ima `aria-label`.

## Pokret

Animacije su tihe i kratke i uvek nešto znače. Pravljene su samo od SVG-a i CSS-a, bez biblioteke.

- Linijske ikone debljine 1.8 u bojama tokena, nikad scene ni ilustracije.
- Učitavanje: podrazumevani `Loader` su stubići na polici (isti se koristi i u dugmadima). Sve kutije u animacijama su kutija iz logoa (puna, poklopac odvojen procepom, oči kao rupe; `logoMark.ts`). Za celu stranicu služe kutije koje se slažu (iste kao kutija praznog stanja), za tabele redovi koji „dišu“ (samo providnost, bez preliva), a kad se otvara jedna stvar (artikal, spisak proizvoda iz prodavnice) kutija koja se otvara. Pretraga ne sklanja listu: stari rezultati ostaju, a u polju za pretragu se vrte mali stubići dok ne stignu novi. Loader se ne prikazuje ako odgovor stigne za manje od 200 ms, a kad se jednom pojavi, ostaje najmanje 500 ms, da ne bljesne (`useLoadingDisplay`).
- Pretraga u listi: dok se kuca, redovi trenutne strane koji ne sadrže ukucani tekst (naziv, šifra, barkod) se skupe (0,3 s), a ukucani deo naziva i šifre se istakne (pozadina `selected`, tekst `accent-text`). Kad stignu rezultati sa servera, oni zamene listu (`SearchMatch`, `SearchRow` u `search.tsx`).
- Promena stanja: broj glatko pređe na novu vrednost (oko 0,5 s), a iznad njega kratko iskoči `+24` ili `−2`. Novo kretanje u istoriji se kratko istakne.
- Unos količine (prijem, prodaja, povrat, korekcija): pored polja su dugmad − i + (`surface-2`, 40 px, na telefonu 44 px), jedan korak po pritisku, a dok se drži, menja se brže. „Stanje posle” se ne prepisuje odjednom: svaka cifra se zavrti do nove vrednosti kao brojčanik (0,6 s, `Odometer`); separatori i znak stoje.
- Filter pilule (Artikli, Porudžbine, Izveštaji, istorija artikla): izbor (pozadina `selected`, okvir `accent`) otklizi i promeni širinu do izabrane pilule (0,35 s), umesto da skoči (`FilterPills`). Izabrani izveštaj se pri promeni blago podigne na mesto (0,3 s).
- Šifra na detalju artikla se kopira klikom: ikona kopiranja se uz malu oprugu zameni kvačicom, a pozadina postane `selected` (1,6 s); šifra ostaje, pa se ništa pored nje ne pomera (`CopySku`).
- Sortiranje po koloni (Artikli): klik na naslov kolone sortira po njoj (nazivi od A, brojevi od najvećeg), drugi klik okreće redosled; strelica pokazuje smer. Redovi koji ostanu na strani otklizaju sa starog mesta na novo (0,45 s, jedan za drugim po 25 ms) umesto da skoče; dok klize, neprozirni su, a onaj koji putuje dalje ide preko ostalih (`useFlipRows`).
- Prekidač: palac pri prebacivanju malo pređe cilj i vrati se, kao opruga (0,3 s).
- Veza sa prodavnicom (Kanali prodaje) je uvek ikona i tekst: dok traje provera, strelice se vrte; kad provera uspe, kvačica se iscrta; dok veza ne radi, uzvičnik se povremeno malo pomeri (`ChannelStatus`).
- Kad nešto nestane sa ekrana, a može da se vrati (deaktiviran artikal), dole u sredini se pojavi poruka sa „Poništi” (pozadina `text`, tekst `bg`). Linija `accent` ispod nje se skraćuje 5 s, koliko traje mogućnost poništavanja, i staje dok je pokazivač ili fokus na poruci (`UndoToast`).
- Uvoz iz prodavnice: lista ostaje na mestu, ali izbledi (30%) i zaključa se, a animacija uvoza stoji u sredini ekrana na sekciji (`surface`, okvir od 1 px). Bez zamagljivanja (vidi „Zabranjeno“: glassmorphism). Kad stigne rezultat, broj uvezenih proizvoda odbroji jedan po jedan do poslednjeg (oko 30 ms po broju, 0,6-3 s), paketi stanu, a animacija ostaje dok je korisnik ne zatvori na X (ili Escape). Uvoz je jedan zahtev, pa se broji tek kad je poznat stvaran broj, nikad izmišljen napredak.
- Uvoz ima i tanku traku napretka (4 px, `accent`) koja se puni uz brojač, a kad se završi, skladište na animaciji dobije `accent-text` boju.
- Čuvanje forme: dugme prvo pokaže stubiće, a kad uspe, iscrta se kvačica i piše „Sačuvano“ (pozadina `selected`, tekst `accent-text`), oko 0,6 s, pa se forma zatvori (`SaveButton`, `useSaved`).
- Greška u formi: polje sa greškom se kratko protrese (0,36 s) na svaki neuspeo pokušaj slanja, a poruka ispod kaže šta da se ispravi (`useShake`).
- Početna: brojevi u pregledu odbroje od 0 do svoje vrednosti (oko 0,5 s), a artikli ispod minimuma se pojave jedan za drugim.
- Pulsira samo ono što traži pažnju (status ispod minimuma ili bez zaliha), i to samo na detalju artikla, ne u listama.
- Uz `prefers-reduced-motion` sve animacije staju, a brojevi se menjaju odmah. Mirno stanje mora biti čitljivo bez animacije.
- Komponente: `src/web/src/components/loaders.tsx`, `stock.tsx`, `feedback.tsx`, `Odometer.tsx`, `StepButton.tsx`, `FilterPill.tsx`, `CopySku.tsx` i `UndoToast.tsx`. Sve se mogu videti na `/dev/animacije` (samo u razvoju).

## Zabranjeno

- gradijenti, glassmorphism, senke kao dekoracija
- emoji umesto ikona
- kartice sa obojenom levom ivicom
- Inter, Roboto, Arial
- podrazumevana Mantine tema bez prilagođavanja
- beli tekst na srednje tamnim bojama (loš kontrast)
- status prikazan samo bojom, bez teksta
- ilustracije i maskote

## Pristupačnost

- Kontrast teksta najmanje 4.5:1 u obe teme. `muted` se koristi samo na `bg`, `sidebar` i `surface`.
- Pravi `<button>`, `<a>`, `<input>` sa `<label>`, nikad `onClick` na `div`.
- Vidljiv fokus: okvir `accent-text` boje od 2 px.

## Mantine

Tema se podešava jednom, u `web/src/theme.ts`: boje iz tabele iznad kao tokeni za obe teme, fontovi, zaobljenja, i podrazumevani stilovi za `Button`, `TextInput`, `Table` i `Badge`. Komponente koriste temu i ne zadaju sopstvene boje.
