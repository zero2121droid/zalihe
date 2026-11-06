# Produkcija: server, domen, email, backup

Beleška za postavljanje Zaliha na pravi server. Cene i dostupnost su provereni 20. 10. 2026; pre kupovine ih treba pogledati ponovo na sajtu provajdera.

## 1. Šta aplikaciji treba

Jedan VPS na kome rade aplikacija (ASP.NET Core sa React buildom), PostgreSQL, Hangfire (u istom procesu kao aplikacija) i Caddy (HTTPS).

- **2 vCPU, 4 GB RAM, 40+ GB disk.** 2 GB je tesno za .NET i Postgres zajedno.
- U radu Lagerko troši oko 1–1,5 GB (sistem i Docker ~0,4, aplikacija ~0,2–0,4, Postgres ~0,3–0,6, Caddy ~0,05). Procena, izmeriti na serveru sa `docker stats`.
- `deploy.sh` pravi image na samom serveru, dok stara verzija radi. Build (.NET i Vite) traži još 1,5–2 GB dok traje.
- Broj klijenata nije ograničenje: i nekoliko stotina malih webshopova je malo opterećenje (webhook po porudžbini, povremeno slanje stanja).
- **Lokacija u EU** (GDPR, blizina Srbije), ne SAD.
- **Ubuntu 24.04 LTS.**
- x86 ili ARM, svejedno: .NET i Postgres rade na oba, Docker image se pravi za izabrani.

## 2. Izbor servera

| Provajder | Plan | Okvirna cena | Napomena |
|---|---|---|---|
| **netcup** (prvi izbor) | vServer (x86), „VPS 500 G12.5” (2 vCore, 4 GB, 64 GB SSD) | 7,98 €/mes. (31. 10. 2026) | Dobar odnos cene i pouzdanosti. Lokacija Nirnberg ili Beč. Ugovor na mesec dana. |
| **OVHcloud** | VPS-1 (2 vCore, 4 GB, 40 GB NVMe) | ~4,5–5 $/mes. | Dnevni backup servera uključen. Lokacija u EU. |
| **Hetzner** | CX23 | ~5,5–6 €/mes. | Najbolji panel, ali 2026. dva puta poskupeo, a najjeftiniji planovi su od oktobra često „not available”. Uzeti samo ako može da se naruči. |
| Contabo | — | — | Ne za produkciju: deljeni procesor jako varira, spora podrška. |

Na netcup sajtu:

- **vServer (x86)** je pravi izbor.
- **Root-Server** ima procesor samo za tebe, pa je potreban tek kad bude puno korisnika. Na njega se lako pređe kasnije.
- **Webhosting** je deljeni PHP hosting i na njemu ne rade Docker, .NET ni Postgres.

Koji netcup plan (cene od 31. 10. 2026, ugovor na mesec dana):

| Plan | Cena | Kada |
|---|---|---|
| VPS 500 G12.5 (2 vCore, 4 GB, 64 GB) | 7,98 € | samo Lagerko, ili Lagerko i još jedna mala aplikacija |
| VPS 1000 G12.5 (4 vCore, 8 GB, 128 GB) | 14,01 € | kad druga aplikacija sigurno dolazi; staje 4–6 sličnih aplikacija (tačka 11) |
| VPS 2000 G12.5 (8 vCore, 16 GB, 256 GB) | 26,01 € | ne treba u prvoj godini |

Krenuti od VPS 500 i preći na veći kad zatreba: ugovor je mesečni.

„0% VAT” je u redu, jer Srbija nije u EU.

Pri kupovini proveriti ukupnu cenu i da li se IPv4 adresa plaća posebno.

## 3. Domen

Nezavisan je od servera i može da se kupi kasnije, kad se izabere konačan naziv aplikacije.

- **.rs**: kod domaćeg registra (npr. mCloud, Loopia).
- **.com**: najzgodnije kod Cloudflare-a ili Porkbuna, zbog lakog podešavanja DNS-a.

Domen je potreban za:

- **HTTPS:** Caddy sam dobija sertifikat čim DNS pokazuje na server.
- **Povezivanje prodavnice preko wc-auth:** traži HTTPS i javnu adresu.
- **Email:** SPF, DKIM i DMARC zapisi u DNS-u, da emailovi ne idu u spam.

## 4. Email

Aplikacija šalje preko SMTP-a (MailKit), pa je provajder samo podešavanje u `Email` sekciji konfiguracije.

- **Brevo** ili **Resend**: besplatan nivo je dovoljan za početak.
- Pošiljalac na sopstvenom domenu (npr. `upozorenja@domen.rs`), uz SPF/DKIM zapise koje provajder da.
- `Email:AppUrl` mora biti javna adresa aplikacije, jer se od nje prave linkovi u emailovima (pozivi).
- Ako provajder servera blokira izlazne portove 587 i 465 (tako je na trenutnom netcup serveru), koristiti Brevo port 2525 sa `SMTP_SECURITY=StartTls`.
- `PLATFORM_OWNER_EMAIL` (`Platform:OwnerEmails`) je adresa vlasnika platforme, na koju stiže email za svaku novu firmu, a njen nalog vidi stranicu „Platforma”. Dovoljno je prosleđivanje (npr. Cloudflare Email Routing), jer ta adresa samo prima poštu. Registracija potvrđuje email kodom, pa nalog sa tom adresom može da napravi samo onaj ko prima njenu poštu.

## 5. Backup baze

- Svake noći `pg_dump` baze, šifrovan, **na drugo mesto**, ne kod istog provajdera: Hetzner Storage Box ili Backblaze B2 (centi mesečno).
- Čuvanje 30 dana.
- Backup provajdera (npr. OVH dnevni snapshot) je dodatak, ne zamena: ne pomaže ako nalog nestane.
- Vraćanje iz backupa isprobati pre prvog pravog korisnika.

## 6. Šta je u repozitorijumu

| Fajl | Šta radi |
|---|---|
| `Dockerfile` | jedan image: React build u `wwwroot` i API; radi kao korisnik `app`, ne root |
| `deploy/docker-compose.yml` | aplikacija, Postgres i Caddy; port aplikacije nije otvoren napolje, samo Caddy (80, 443) |
| `deploy/nginx/` | landing na trenutnom serveru (tačka 13) |
| `deploy/ci-deploy.sh` | šta GitHub Actions sme da pokrene na serveru (tačka 15) |
| `deploy/Caddyfile` | HTTPS (sertifikat sam), preusmerenje sa HTTP-a, bezbednosna zaglavlja, kompresija |
| `deploy/.env.example` | podešavanja i lozinke; kopira se u `deploy/.env`, koji se nikad ne commituje |
| `deploy/deploy.sh` | nova verzija: `git pull`, build, restart, čeka da sve bude zdravo |
| `deploy/backup.sh` | baza i ključevi za šifrovanje u jedan folder, čuvanje 30 dana, kopija na drugo mesto (rclone) |
| `deploy/restore.sh` | vraća backup (pita pre nego što bilo šta promeni) |

Aplikacija u produkciji:

- Migracije primenjuje sama pri pokretanju (`Database:MigrateOnStartup`).
- Čita `X-Forwarded-*` od Caddyja (`ForwardedHeaders:Enabled`), pa su kolačići `Secure`, a ograničenje registracije po IP adresi vidi pravu adresu klijenta.
- Ključeve za šifrovanje čuva u volumenu `keys` (`DataProtection:KeysPath`).
- Na `/health` odgovara `Healthy` dok je baza dostupna.
- Logove čuva Docker: 5 × 10 MB po kontejneru, pregled sa `docker compose logs app`.

**Ključevi za šifrovanje i baza idu zajedno.** Bez ključeva:

- šifrovani API ključevi prodavnica u bazi ne mogu da se pročitaju, pa svaka prodavnica mora ponovo da se poveže;
- svi korisnici bivaju odjavljeni.

Zato ih `backup.sh` uvek čuva zajedno.

## 7. Lokalna proba cele produkcije

Pre servera, ili posle većih izmena, ista postavka se pokreće na računaru. Ovde su portovi 80 i 443 zauzeti, pa se koriste 8081 i 8443.

1. `deploy/.env` popuniti ovako:
   - `COMPOSE_PROJECT_NAME=zalihe-prod`
   - `DOMAIN=localhost`
   - `APP_URL=https://localhost:8443`
   - `HTTP_PORT=8081` i `HTTPS_PORT=8443`
   - za SMTP: `SMTP_HOST=host.docker.internal`, `SMTP_PORT=1025`, `SMTP_SECURITY=None`, pa emailovi idu u Mailpit
2. `docker compose -f deploy/docker-compose.yml up -d --build --wait`
3. Otvoriti https://localhost:8443. Browser upozorava na sertifikat, jer Caddy ovde koristi svoj lokalni.
4. Na kraju: `docker compose -f deploy/docker-compose.yml down`. Uz `-v` se brišu i podaci te probe.

Isprobano 20. 10. 2026:

- Proxy i keširanje:
  - HTTP se preusmerava na HTTPS.
  - Kolačići su `Secure`.
  - `index.html` se ne kešira, a `/assets` se kešira godinu dana.
  - Nepoznata `/api` adresa vraća 404 ProblemDetails.
- U browseru:
  - prijava, artikal, direktan link na stranicu
  - poziv emailom sa ispravnom adresom u linku
  - prihvatanje poziva
- Prijava preživi `down`/`up`, dakle ključevi za šifrovanje ostaju.
- Backup, pa izmena, pa `restore.sh`: izmena nestaje, a prijava i dalje važi.

## 8. Šta mora da se proveri na serveru

Ono što lokalno ne može:

- **Sertifikat:** Caddy dobija pravi Let's Encrypt sertifikat (DNS mora da pokazuje na server, a portovi 80 i 443 moraju biti otvoreni).
- **Webhookovi:** iz prave WooCommerce prodavnice stižu preko interneta.
- **Emailovi:** stižu u inbox, ne u spam (SPF, DKIM, DMARC).
- **Prodavnica:** povezivanje preko wc-auth (napravljeno i isprobano lokalno; na serveru proveriti sa pravim domenom i bar jednom prodavnicom sa sigurnosnim dodatkom).
- **Backup:** noćni backup stvarno ide na drugo mesto, a vraćanje sa tog mesta je isprobano.

## 9. Postavljanje na server, korak po korak

**1. Server.** Kupiti VPS (Ubuntu 24.04), dodati svoj SSH ključ pri kupovini, pa se prijaviti kao root i napraviti korisnika:

```bash
adduser zalihe && usermod -aG sudo zalihe
rsync --archive --chown=zalihe:zalihe ~/.ssh /home/zalihe   # isti SSH ključ za novog korisnika
```

U `/etc/ssh/sshd_config` postaviti `PasswordAuthentication no` i `PermitRootLogin no`, pa `systemctl restart ssh`.

**2. Firewall.**

```bash
ufw allow OpenSSH && ufw allow 80 && ufw allow 443 && ufw enable
```

Docker zaobilazi ufw za portove koje objavi. Zato compose objavljuje samo 80 i 443 (Caddy), a Postgres i aplikacija nisu objavljeni.

**3. Docker i automatska bezbednosna ažuriranja.** Docker se instalira po uputstvu sa docs.docker.com (Ubuntu, „apt repository”). Zatim:

```bash
sudo usermod -aG docker zalihe
sudo apt install unattended-upgrades
```

Swap od 2 GB, da build pri deployu ne ostane bez memorije (tačka 1):

```bash
sudo fallocate -l 2G /swapfile && sudo chmod 600 /swapfile && sudo mkswap /swapfile && sudo swapon /swapfile
echo '/swapfile none swap sw 0 0' | sudo tee -a /etc/fstab
```

**4. Kod.**

1. Na serveru napraviti SSH ključ: `ssh-keygen -t ed25519`.
2. Javni deo dodati na GitHub-u kao *Deploy key*, samo za čitanje (Settings → Deploy keys).
3. Klonirati repozitorijum: `git clone git@github.com:…/zalihe.git ~/zalihe`

**5. Podešavanja.**

1. `cp deploy/.env.example deploy/.env`
2. Popuniti `COMPOSE_PROJECT_NAME` (`zalihe-prod`), domen, `APP_URL`, `POSTGRES_PASSWORD` (`openssl rand -hex 32`), SMTP i `PLATFORM_OWNER_EMAIL`.
3. `chmod 600 deploy/.env`

**6. DNS.** A zapis za domen (npr. `app.zalihe.rs`) na IP adresu servera, i AAAA ako server ima IPv6. Sačekati da `dig +short app.zalihe.rs` vrati tu adresu.

**7. Pokretanje.** `deploy/deploy.sh`. Prvi build traje nekoliko minuta. Zatim otvoriti `https://<domen>/health`: treba da piše `Healthy`.

**8. Backup.**

1. Instalirati rclone: `sudo apt install rclone`.
2. Napraviti udaljeno mesto za backup (`rclone config`): prvo običan remote (npr. Backblaze B2 ili Hetzner Storage Box), pa preko njega `crypt` remote, da su kopije šifrovane.
3. Lozinku za `crypt` sačuvati i van servera (menadžer lozinki). Bez nje se backup ne može pročitati.
4. U `deploy/.env` upisati `BACKUP_REMOTE=ime-crypt-remota:zalihe`.
5. Probati ručno, sa `deploy/backup.sh`.
6. Dodati u `crontab -e`:

```
15 3 * * * /home/zalihe/zalihe/deploy/backup.sh >> /home/zalihe/backup.log 2>&1
```

**9. Proba vraćanja.**

1. Preuzeti jedan backup sa udaljenog mesta: `rclone copy …`
2. Vratiti ga sa `deploy/restore.sh <folder>`.
3. Proveriti aplikaciju.

Ovo treba uraditi jednom pre prvog pravog korisnika, i ponoviti s vremena na vreme.

**10. Praćenje.** Besplatan uptime monitor (npr. UptimeRobot ili Better Stack) na `https://<domen>/health`, sa emailom kad prestane da odgovara.

**Nova verzija**, posle pusha na `main`: na serveru pokrenuti `deploy/deploy.sh`. Na trenutnom serveru se umesto toga koristi `app deploy lagerko` (tačka 12).

## 10. Redosled

1. ~~Priprema u kodu, isprobana lokalno~~ (gotovo)
2. Server, firewall, Docker (koraci 1–3 iz tačke 9)
3. Domen i DNS
4. Prvo postavljanje, backup i proba vraćanja (koraci 4–10)
5. Povezivanje prodavnice preko wc-auth, isprobano na pravoj adresi
6. Doterivanje, pa prvi pravi korisnik

Server može prvo da radi kao probna verzija bez pravih korisnika. Postaviti aplikaciju ne znači pustiti je ljudima.

## 11. Više aplikacija na istom serveru (kasnije)

Do druge aplikacije sve ostaje kao u tačkama 1–10. Ovo se radi tek kad druga aplikacija stvarno krene, redom:

1. **Veći server, ako treba.** Pre svake nove aplikacije pogledati `docker stats` i `free -h`: posle deploya mora ostati bar 1 GB slobodno. Ako ne ostaje, preći na VPS 1000 (tačka 2).
2. **Jedan Caddy za sve.** Caddy se izdvaja iz `deploy/docker-compose.yml` u poseban compose (npr. `~/proxy`), sa zajedničkom Docker mrežom (npr. `web`).
   - Svaka aplikacija se priključuje toj mreži, pod jedinstvenim imenom (npr. `zalihe-app`).
   - Caddyfile ima po jedan blok za svaki domen (`app.zalihe.rs { reverse_proxy zalihe-app:8080 }`).
   - Samo taj Caddy objavljuje portove 80 i 443.
3. **Jedan Postgres za sve.** Svaka aplikacija dobija svoju bazu i svog korisnika, sa lozinkom samo za tu bazu. Tako se štedi ~0,5 GB po aplikaciji. Nadogradnja Postgresa tada pogađa sve aplikacije odjednom, pa se radi posle backupa.
4. **Ograničenje memorije po kontejneru.** U compose-u `mem_limit` za svaku aplikaciju (npr. `512m` za Lagerko), da jedna aplikacija ne uzme memoriju drugima. Za .NET aplikacije i `DOTNET_gcServer: "0"`, jer Server GC troši više memorije nego što maloj aplikaciji treba. .NET sam poštuje `mem_limit`.
5. **Build van servera.** GitHub Actions pravi image i šalje ga na `ghcr.io`, a `deploy.sh` umesto builda radi `docker compose pull`. Tako nestaje skok od 1,5–2 GB pri deployu. Potrebno kad je na serveru više od dve aplikacije, ili ranije ako deploy počne da puca zbog memorije.
6. **Backup svake baze.** `backup.sh` i `restore.sh` sada znaju samo za bazu `zalihe` i njene ključeve. Proširiti ih na sve baze (i ključeve svake aplikacije koja ih ima), pa isprobati vraćanje za svaku.
7. **Praćenje svake aplikacije.** Poseban uptime monitor za `/health` svake aplikacije.

Rizik: kad server padne, padaju sve aplikacije zajedno. Zato je backup na drugom mestu (tačka 5) obavezan za svaku.

## 12. Trenutni server

Lagerko sada radi drugačije od postavke opisane u tačkama 6 i 9, jer deli server sa drugom aplikacijom (netcup, Debian 13, ARM64, 4 GB, IP `152.53.182.73`):

| Adresa | Šta je | Kod | Grana | Port |
|---|---|---|---|---|
| `lagerko.com` | landing, Nginx direktno služi `landing/` | `/srv/lagerko` | `main` | |
| `www.lagerko.com` | preusmerenje na `lagerko.com` | | | |
| `app.lagerko.com` | beta | `/srv/lagerko` | `main` | 3001 |
| `dev.lagerko.com` | provera izmena pre bete | `/srv/lagerko-dev` | `dev` | dodeli skripta |

- HTTPS i domene radi **Nginx** na serveru (Let's Encrypt preko certbota), a ne Caddy. Aplikacija je objavljena samo na `127.0.0.1`.
- `deploy/docker-compose.override.yml` postoji samo na serveru: isključuje Caddy i određuje port. Pravi ga skripta `app` (`/usr/local/bin/app`).
- Nginx mora da šalje `proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;`. Bez toga aplikacija sve posetioce vidi kao jednu adresu, pa ograničenje registracije (30 zahteva na sat) važi za sve zajedno.
- Zbog toga se **`deploy/deploy.sh` ovde ne pokreće**: sa `-f docker-compose.yml` zanemaruje override, pa bi aplikacija ostala bez porta, a Caddy bi tražio portove 80 i 443 koje drži Nginx.

| Šta | Beta | Dev |
|---|---|---|
| Nova verzija (radi i sam push, tačka 15) | `app deploy lagerko` | `app deploy lagerko-dev` |
| Izmena podešavanja | `app env lagerko` | `app env lagerko-dev` |
| Logovi | `app logovi lagerko app` | `app logovi lagerko-dev app` |

Izmena `.env` običnim `nano` ne važi dok se ne pokrene `app env` ili `app deploy`. Email ide preko Breva na portu 2525. `app deploy` povlači granu koja je izabrana u tom folderu. Za izmenu samo landing strane nije potreban build, dovoljno je `git -C /srv/lagerko pull`.

**Dve instance.** `COMPOSE_PROJECT_NAME` u `deploy/.env` je obavezan i različit: `zalihe-prod` i `zalihe-dev`. Po njemu se zovu kontejneri, baza, ključevi i image. Bez njega compose odbija da se pokrene, jer bi druga instanca inače preuzela bazu prve. Dok je jedan deploy u toku ne pokretati drugi: build traži 1,5–2 GB, a server ima 4 GB i 2 GB swapa.

**Pravila za dev:**

- Povezivati samo test prodavnice. Ista prodavnica na dve instance znači da obe menjaju njeno stanje.
- Ne kopirati bazu bete na dev.
- Izmena ide na granu `dev`, proveri se na `dev.lagerko.com`, pa se spoji u `main`. Migracije se tako prvo izvrše na dev bazi.

**Backup bete** ide svake noći u 3:15 (root crontab, log u `/var/log/lagerko-backup.log`) na Backblaze B2, bucket `lagerko-backup` u regionu `eu-central-003`, preko rclone `crypt` remota `b2-crypt:zalihe`. Ključ `lagerko-server` važi samo za taj bucket, a prethodne verzije se čuvaju 7 dana. Lozinke za `crypt` su i van servera, u menadžeru lozinki: bez njih se backup ne može pročitati. Vraćanje je isprobano 6. 11. 2026 u privremenom Postgres kontejneru: sve tabele su vraćene, a ključevi su u kopiji. Dev instanca nema backup, namerno.

## 13. Selidba na lagerko.com

DNS je na Cloudflare-u, zapisi su **DNS only** (A na `152.53.182.73`): `@`, `www`, `app` i `dev`. Beta kreće sa praznom bazom, pa nema prenosa podataka.

**1. Kod.** Grane `main` i `dev` moraju biti na GitHub-u sa ovim izmenama, jer `app nova` i `app deploy` kod uzimaju odatle.

**2. Beta na `app.lagerko.com`.** Prvo se ime instance upisuje u `.env`, inače deploy ne prolazi. `/letsencrypt/!` čuva putanje do starog sertifikata, jer `nginx` bez njih ne prolazi proveru dok certbot ne upiše nove.

```bash
cd /srv/lagerko/deploy
grep -q '^COMPOSE_PROJECT_NAME=' .env || echo 'COMPOSE_PROJECT_NAME=zalihe-prod' >> .env
docker compose down -v                     # BRIŠE bazu i ključeve dosadašnje probne verzije
sed -i 's|^APP_URL=.*|APP_URL=https://app.lagerko.com|; s|^DOMAIN=.*|DOMAIN=app.lagerko.com|' .env
sed -i 's|^DOMAIN=.*|DOMAIN=app.lagerko.com|' ../.app-config
sed -i '/letsencrypt/!s|lagerko\.miljanmilojkovic\.com|app.lagerko.com|g' /etc/nginx/conf.d/lagerko.conf
nginx -t
certbot --nginx -d app.lagerko.com --redirect --non-interactive
app deploy lagerko
```

**3. Stara adresa.** Preusmerava se na novu dok svi linkovi ne pređu:

```bash
cat > /etc/nginx/conf.d/lagerko-old.conf <<'CONF'
server {
    listen 80;
    listen [::]:80;
    server_name lagerko.miljanmilojkovic.com;

    return 301 https://app.lagerko.com$request_uri;
}
CONF
nginx -t && systemctl reload nginx
certbot --nginx -d lagerko.miljanmilojkovic.com --keep-until-expiring --redirect --non-interactive
```

**4. Landing.**

```bash
cp /srv/lagerko/deploy/nginx/lagerko.com.conf /etc/nginx/conf.d/
nginx -t && systemctl reload nginx
certbot --nginx -d lagerko.com -d www.lagerko.com --redirect --non-interactive
```

Proveriti da `https://lagerko.com/CLAUDE.md` vraća 404.

**5. Dev na `dev.lagerko.com`.** Ime instance se daje uz komandu, jer ga `app nova` traži pre nego što postoji `.env`. Na pitanje o uređivanju `.env` odgovoriti `n`, a email podešavanja se posle prepisuju sa bete, pa se lozinka ne kuca.

```bash
COMPOSE_PROJECT_NAME=zalihe-dev app nova lagerko-dev zero2121droid/zalihe dev.lagerko.com
cd /srv/lagerko-dev/deploy
sed -i 's/^COMPOSE_PROJECT_NAME=.*/COMPOSE_PROJECT_NAME=zalihe-dev/' .env
sed -i '/^\(SMTP_\|EMAIL_FROM\|PLATFORM_OWNER_EMAIL\)/d' .env
grep -E '^(SMTP_|EMAIL_FROM|PLATFORM_OWNER_EMAIL)' /srv/lagerko/deploy/.env >> .env
git -C /srv/lagerko-dev checkout dev
sed -i '/server_name dev.lagerko.com/a\    add_header X-Robots-Tag "noindex, nofollow" always;' /etc/nginx/conf.d/lagerko-dev.conf
nginx -t && systemctl reload nginx
app deploy lagerko-dev
```

## 14. SignalR (SPEC 11.1)

Hub se još ne pravi, ali je postavka spremna. Dogovor je da hubovi žive pod `/hubs/...`.

- **Nginx:** konfiguracija iz skripte `app` već šalje `Upgrade` i `Connection "upgrade"`, a `proxy_read_timeout 300s` je duži od SignalR pinga (15 s).
- **Caddy:** prosleđuje WebSocket sam.
- **Vite:** `/hubs` se u razvoju prosleđuje na backend sa `ws: true`.
- **Backplane nije potreban** dok radi jedan proces. Hangfire je u istom procesu, pa `IHubContext` iz posla stiže do povezanih klijenata. Beta i dev su odvojeni, svaka sa svojim klijentima.
- **Mobilna aplikacija:** SignalR šalje bearer token kroz `?access_token=...` kod WebSocketa. Tada `/hubs` mora da se izuzme iz Nginx `access_log`-a, da tokeni ne završe u logu.
- **Pri deployu** se sve veze prekinu. SPEC to već pokriva: posle ponovnog povezivanja frontend osveži aktivne upite.

## 15. Automatski deploy (GitHub Actions)

`.github/workflows/deploy.yml`: svaki push na `main` ili `dev` pokrene testove, pa, ako prođu, `app deploy` za tu granu (`main` na betu, `dev` na dev). Izmene samo u `docs/` ne pokreću ništa. Deploy radi jedan po jedan, a build je i dalje na serveru.

Deploy se ne izvršava dok ne postoji promenljiva `DEPLOY_HOST`, pa se ona dodaje poslednja, kad je tačka 13 gotova.

Ključ sme da pokrene samo `deploy/ci-deploy.sh`, koji prima isključivo `beta` ili `dev`. Ko dođe do ključa ne dobija shell na serveru.

Na serveru:

```bash
git -C /srv/lagerko pull --ff-only
install -m 755 /srv/lagerko/deploy/ci-deploy.sh /usr/local/bin/lagerko-ci
```

Na Macu, iz foldera projekta (potreban je `gh`):

```bash
ssh-keygen -t ed25519 -N '' -C github-actions-lagerko -f /tmp/lagerko-ci
gh secret set DEPLOY_SSH_KEY < /tmp/lagerko-ci
ssh root@152.53.182.73 'ssh-keyscan -t ed25519 localhost' | sed 's/^localhost/152.53.182.73/' | gh secret set DEPLOY_KNOWN_HOSTS
ssh root@152.53.182.73 "echo 'restrict,command=\"/usr/local/bin/lagerko-ci\" $(cat /tmp/lagerko-ci.pub)' >> ~/.ssh/authorized_keys"
rm /tmp/lagerko-ci /tmp/lagerko-ci.pub
gh variable set DEPLOY_HOST --body 152.53.182.73
```

Kad build na serveru počne da smeta, image može da se pravi na GitHub-u (tačka 11, korak 5).
