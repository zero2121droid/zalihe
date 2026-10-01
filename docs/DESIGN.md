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
| `accent` | `#B4C17E` | `#B4C17E` | primarno dugme (tamni tekst na njemu), logo |
| `accent-hover` | `#CBD69A` | `#A3B16A` | hover za primarno dugme |
| `accent-text` | `#B4C17E` | `#55621E` | linkovi, ikona aktivne stavke, fokus okvir |
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
- Bočna navigacija 232 px, sadržaj najviše 1180 px širine.
- Visina dugmadi 40 px na računaru, minimum 44 px na telefonu, 56 px za glavnu akciju na telefonu.

## Ikone

Linijske ikone, debljina 1.8, 16 px u navigaciji i 18-20 px na telefonu. Biblioteka: Tabler Icons ili Lucide, jedna, dosledno. Dugme samo sa ikonom uvek ima `aria-label`.

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
