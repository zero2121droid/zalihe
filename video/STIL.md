# Lagerko: pravila za promo snimke

Važe za sve klipove u `video/`. Alati su u `video/lib/`, a uputstvo za njih je na dnu ovog fajla.

## Podaci

- Uvek demo prodavnica „Nit“ (prodavnica odeće), sa korisnicom Ana. Bez pravih imena, adresa i telefona kupaca.
- Ako demo podaci ne postoje, napravi seed skriptu `video/seed-demo.*` koja ih napravi, i ne diraj prave podatke.
- Glavni artikal kroz priču: „Majica Basic, bela – M“, šifra NIT-MAJ-BELA-M, minimum 5 kom.

## Boje (iz aplikacije, tamna tema)

- pozadina `#11130E`, površina `#191C15`, linija `#2C3125`
- tekst `#E4E7DC`, sekundarni `#C4C9B8`, prigušeni `#9BA18F`, akcenat `#B4C17E`
- upozorenje/greška (samo u reklami) `#E07A5F`
- Font za tekst: Inter (ili IBM Plex Sans ako je u projektu). Za brojeve i logo: monospace iz aplikacije.
  U ovom projektu je to **IBM Plex Sans** i **IBM Plex Mono**.
- Logo: koristi pravi logo iz aplikacije (SVG/komponenta), ne crtaj nov.

## Natpisi u snimku

- Tamna kutija (`#191C15`, 92% neprovidnosti, ivica `#2C3125`, radius 8px), tekst `#E4E7DC`, 30px, 600, centrirano dole, oko 60px od dna.
- Najviše dva reda. Kratke rečenice na srpskom latinicom, u drugom licu („upišeš“, „imaš“).

## Vertikalni klip 1080x1920 (Reels/TikTok)

- Logo levo gore: x=80, y=160, širina 280px.
- Naslov u dva reda, SemiBold 76px: prvi red `#E4E7DC` na y=290, drugi red akcenat `#B4C17E` na y=380.
- Snimak: y=555, visina 810 (1440x1080 isečak smanjen na 1080x810). Tanke linije `#2C3125` iznad i ispod.
  - Običan ekran aplikacije: snima se u prozoru 1312x810 (`VERTICAL` u `lib/record.js`), crop x=232 (bez bočnog menija), bez skaliranja. Prvobitno pravilo je bilo 1920x1080 i crop x=300, ali je tada odsecalo početak naslova i desnu kolonu sa brojevima, a tekst aplikacije na telefonu je bio premali (oko 10 px).
  - Zumirani kadar ili sadržaj levo: crop x=0.
  - Logo i završna kartica (1920x1080): crop x=240 (centrirano).
- Dole dva reda poruke, SemiBold 56px, `#E4E7DC`, na y=1450 i y=1525.
- Sitno: „Besplatno dok traje beta · bez kartice“, Medium 36px, `#9BA18F`, y=1650.

## Klip sa telefona (mobilni prikaz aplikacije)

Za radnje koje se rade sa telefona: Porudžbina sa Instagrama, Prijem robe i Istorija. Izveštaji i CSV uvoz ostaju u prikazu sa računara (tabele i uvoz iz Excela se rade za računarom).

- Snima se mobilni prikaz u prozoru 390x580 (`PHONE` u `lib/record.js`), sa Chrome-om pokrenutim na 2x, pa je snimak 780x1160 i oštar.
- Umesto strelice se vidi trag dodira (krug).
- Okvir 1080x1920: logo x=80, y=110, širina 220px; naslov SemiBold 64px na y=190 (`#E4E7DC`) i y=268 (akcenat); ekran telefona 780x1160 na x=150, y=400, zaobljen 40px, sa ivicom `#2C3125`; poruka SemiBold 48px na y=1610 i y=1672; sitno Medium 32px `#9BA18F` na y=1770.
- Natpis je isti kao u ostalim klipovima, ali 15 CSS px (30px u snimku), iznad donje navigacije. Duže rečenice se ručno dele u dva reda.
- Završna kartica je u ekranu telefona, sa istim sadržajem.
- U `renderVertical` se prosleđuje `phone: true`.

## 16:9 klip

1920x1080 bez okvira, natpisi u snimku.

## Završna kartica (3,5 s)

- Logo na sredini, ispod „Sajt, Instagram, telefon i lokal na jednom mestu.“, pa „Besplatno dok traje beta. Bez kartice.“ i dugme „Napravi besplatan nalog“ (pozadina `#B4C17E`, tekst `#11130E`).

## Izvoz

- H.264, 30 fps, `-crf 20 -preset veryfast -pix_fmt yuv420p -movflags +faststart`, bez zvuka.
- Fade in 10 frejmova, fade out poslednjih 0,5 s.
- Svaka scena se renderuje u svoj .mp4 (pod privremenim imenom, preimenuj tek kad ffmpeg izađe sa 0), pa se spoje concat demuxerom sa `-c copy`.
- Dugačka kucanja i čekanja smeju da se ubrzaju 1,5–2x, nikad više od 3x.
- Ne menjaj interfejs aplikacije radi snimka. Ako nešto mora da se doteruje (npr. localhost → nit.rs, isečeno čekanje), zapiši to u `video/<klip>/NAPOMENE.md`.

## Provera pre predaje

- Izvuci 6–8 kadrova iz gotovog klipa (`ffmpeg -vf fps=...`) i pogledaj ih: da li se natpisi vide celi, da li je bitan broj u isečku, da nema praznih ili crnih kadrova.
- Na kraju napiši trajanje, putanju do fajla i listu poglavlja (vreme → naziv).

---

## Alati

Potrebno je: pokrenut Docker (`docker compose up -d --wait`), API i Vite (komande su u `CLAUDE.md`), `ffmpeg`, Python sa Pillow-om i `npm install` u `video/`. Zavisnosti snimaka su u `video/package.json`, odvojene od aplikacije.

| Fajl | Čemu služi |
|---|---|
| `seed-demo.js` | pravi demo firmu „Nit“ u lokalnoj bazi: `node video/seed-demo.js --stage=base` |
| `lib/record.js` | snima scenario u Playwright-u: spor klik i kucanje, vidljiv kursor, oznake scena |
| `lib/captions.js` | natpis kao fiksni DOM element, ubacuje se u stranicu tokom snimanja |
| `lib/render.js` | seče snimak na scene, ubrzava ih, spaja i izvlači poster i kontrolne kadrove |
| `lib/vertical.sh`, `lib/vertical_frame.py` | od 1920x1080 scene pravi 1080x1920 klip sa okvirom |
| `lib/endcard.js` | završna kartica od 3,5 s, ista za sve klipove |
| `lib/frames.js` | kadar-po-kadar snimanje HTML stranice sa `render(t)` za animacije |
| `lib/html.js` | zajednički fontovi, boje i pravi logo za HTML stranice |
| `lib/contact.sh` | spaja kontrolne kadrove klipa u jednu sliku, za proveru pre predaje |

Svaki klip se pravi iz svog foldera, npr. `node video/porudzbina/snimi.js`. Gotovi `.mp4` i `.jpg` nisu u git-u jer se mogu ponovo napraviti.

Faze demo podataka (`--stage`), da svaki klip može da se snimi sam:

| Faza | Majica Basic, bela – M | Za klip |
|---|---|---|
| `base` | 8 kom, poslednja ručna porudžbina R-0001 | Izveštaji, Porudžbina, CSV uvoz |
| `order` | 4 kom, upisana R-0002 (IG 154) | Prijem robe |
| `full` | 28 kom, plus prijem po fakturi 118 i izmena korisnika Marko | Istorija, Obilazak |

Demo nalog: `ana@nit.test` / `Lagerko-demo-1`. Seed briše i ponovo pravi samo svoju firmu (ID `0190aaaa-…`) i odbija da radi na bazi van `localhost:5433`. Podaci drugih firmi se samo čitaju: proizvodi, slike i ključ test prodavnice uzimaju se iz postojeće lokalne firme „Nit“.
