# Produkcija: server, domen, email, backup

Beleška za postavljanje Zaliha na pravi server. Cene i dostupnost su provereni 20. 10. 2026; pre kupovine ih treba pogledati ponovo na sajtu provajdera.

## 1. Šta aplikaciji treba

Jedan VPS na kome rade aplikacija (ASP.NET Core sa React buildom), PostgreSQL, Hangfire (u istom procesu kao aplikacija) i Caddy (HTTPS).

- **2 vCPU, 4 GB RAM, 40+ GB disk.** 2 GB je tesno za .NET i Postgres zajedno.
- **Lokacija u EU** (GDPR, blizina Srbije), ne SAD.
- **Ubuntu 24.04 LTS.**
- x86 ili ARM, svejedno: .NET i Postgres rade na oba, Docker image se pravi za izabrani.

## 2. Izbor servera

| Provajder | Plan | Okvirna cena | Napomena |
|---|---|---|---|
| **netcup** (prvi izbor) | vServer (x86), plan sa 4 GB (npr. „VPS 500”) | ~6 €/mes. | Dobar odnos cene i pouzdanosti. Lokacija Nirnberg ili Beč. Proveriti trajanje ugovora („Vertragslaufzeit”): neki planovi traže 12 meseci ili naplaćuju naknadu za mesečni ugovor. |
| **OVHcloud** | VPS-1 (2 vCore, 4 GB, 40 GB NVMe) | ~4,5–5 $/mes. | Dnevni backup servera uključen. Lokacija u EU. |
| **Hetzner** | CX23 | ~5,5–6 €/mes. | Najbolji panel, ali 2026. dva puta poskupeo, a najjeftiniji planovi su od oktobra često „not available”. Uzeti samo ako može da se naruči. |
| Contabo | — | — | Ne za produkciju: deljeni procesor jako varira, spora podrška. |

Na netcup sajtu:

- **vServer (x86)** je pravi izbor.
- **Root-Server** ima procesor samo za tebe, pa je potreban tek kad bude puno korisnika. Na njega se lako pređe kasnije.
- **Webhosting** je deljeni PHP hosting i na njemu ne rade Docker, .NET ni Postgres.

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
| `deploy/Caddyfile` | HTTPS (sertifikat sam), preusmerenje sa HTTP-a, bezbednosna zaglavlja, kompresija |
| `deploy/.env.example` | podešavanja i lozinke; kopira se u `deploy/.env`, koji se nikad ne commituje |
| `deploy/deploy.sh` | nova verzija: `git pull`, build, restart, čeka da sve bude zdravo |
| `deploy/backup.sh` | baza i ključevi za šifrovanje u jedan folder, čuvanje 30 dana, kopija na drugo mesto (rclone) |
| `deploy/restore.sh` | vraća backup (pita pre nego što bilo šta promeni) |

Aplikacija u produkciji:

- Migracije primenjuje sama pri pokretanju (`Database:MigrateOnStartup`).
- Čita `X-Forwarded-*` od Caddyja (`ForwardedHeaders:Enabled`), pa su kolačići `Secure`.
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
- **Prodavnica:** povezivanje preko wc-auth (posebna funkcija, posle postavljanja).
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

**4. Kod.**

1. Na serveru napraviti SSH ključ: `ssh-keygen -t ed25519`.
2. Javni deo dodati na GitHub-u kao *Deploy key*, samo za čitanje (Settings → Deploy keys).
3. Klonirati repozitorijum: `git clone git@github.com:…/zalihe.git ~/zalihe`

**5. Podešavanja.**

1. `cp deploy/.env.example deploy/.env`
2. Popuniti domen, `APP_URL`, `POSTGRES_PASSWORD` (`openssl rand -hex 32`) i SMTP.
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

**Nova verzija**, posle pusha na `main`: na serveru pokrenuti `deploy/deploy.sh`.

## 10. Redosled

1. ~~Priprema u kodu, isprobana lokalno~~ (gotovo)
2. Server, firewall, Docker (koraci 1–3 iz tačke 9)
3. Domen i DNS
4. Prvo postavljanje, backup i proba vraćanja (koraci 4–10)
5. Povezivanje prodavnice preko wc-auth, isprobano na pravoj adresi
6. Doterivanje, pa prvi pravi korisnik

Server može prvo da radi kao probna verzija bez pravih korisnika. Postaviti aplikaciju ne znači pustiti je ljudima.
