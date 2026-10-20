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

## 6. Šta mora da radi u produkciji

Stvari koje lokalno ne mogu da se vide, a moraju da se provere na serveru:

- **Ključevi za Data Protection** se čuvaju trajno (volume ili baza). Bez toga posle restarta kontejnera šifrovani API ključevi prodavnica postaju nečitljivi i sve prodavnice moraju ponovo da se povežu.
- **Cookie** je `Secure`, a aplikacija iza Caddyja čita prave `X-Forwarded-*` zaglavlja.
- **Antiforgery** radi preko HTTPS-a.
- **Webhookovi** iz prave WooCommerce prodavnice stižu preko interneta.
- **Emailovi** stižu u inbox, ne u spam.
- **Hangfire poslovi** (sinhronizacija, slanje stanja, upozorenja) rade, a Hangfire dashboard nije javno dostupan.
- **Health check** odgovara.
- **Logovi** su sačuvani i mogu da se pregledaju.

## 7. Redosled

1. **Priprema u kodu, lokalno:**
   - Dockerfile
   - produkcijska konfiguracija
   - produkcijski `docker-compose` sa Caddyjem
   - skripta za backup

   Sve se isproba lokalno.
2. **Kupovina servera**, Ubuntu 24.04, SSH ključ, firewall (samo 22, 80, 443).
3. **Domen** i DNS zapis prema serveru.
4. **Prvo postavljanje**, provera liste iz tačke 6 i probni backup i vraćanje.
5. **Povezivanje prodavnice preko wc-auth**, isprobano na pravoj adresi.
6. **Doterivanje**, pa prvi pravi korisnik.

Server može prvo da radi kao probna verzija bez pravih korisnika. Postaviti aplikaciju ne znači pustiti je ljudima.
