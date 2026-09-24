# ZipZap — konfiguracja i wdrożenie produkcyjne

Jak uruchomić ZipZap poza developmentem, gdzie trzymać sekrety i co dokonfigurować.

## 1. Warstwy konfiguracji
Kolejność (późniejsze nadpisują wcześniejsze):
1. `backend/src/ZipZap.Api/appsettings.json` — **domyślne wartości developerskie**
   (commitowane; zawierają tylko placeholdery, nie realne sekrety).
2. `appsettings.{ASPNETCORE_ENVIRONMENT}.json` — opcjonalnie per środowisko.
3. **Zmienne środowiskowe** — nadpisują wszystko. Zagnieżdżenie przez `__`
   (np. `Jwt__SigningKey`, `Payments__Mock__Secret`).

`docker compose` czyta plik **`.env`** (ignorowany przez git) do podstawienia
zmiennych. Skopiuj `.env.example` → `.env` i uzupełnij.

## 2. Guard produkcyjny (fail-fast)
Gdy `ASPNETCORE_ENVIRONMENT=Production`, API **odmówi startu**, jeśli wykryje
sekrety deweloperskie/niekompletne (i wypisze, co ustawić):
- `Jwt:SigningKey` = placeholder `CHANGE_ME…` lub < 32 znaki,
- `ConnectionStrings:Postgres` z `Password=zipzap` (domyślne),
- `RabbitMq:Password` puste lub `zipzap`,
- `Payments:Provider = mock` (na produkcji wymagany realny dostawca),
- `Payments:Mock:Secret = mock-dev-secret`,
- `Seed:AdminPassword` puste lub `Admin123!`.

## 2a. Tryb publicznego pilotażu (hartowanie w Development)
Pilotaż bywa wdrażany w `Development` (mock płatności, brak twardego guardu), ale
**publiczny test nie może wystawiać narzędzi deweloperskich**. Ustaw:

| Zmienna | Efekt |
|---|---|
| `PILOT__PUBLIC=true` | Tryb hartowany niezależnie od `ASPNETCORE_ENVIRONMENT`: **Swagger wyłączony**, **strona mocka płatności `/api/payments/mock/*` → 404**, **seed administratora pominięty** (żadnego konta z domyślnym hasłem), CORS bez otwartego fallbacku. |
| `CORS__ALLOWEDORIGINS__0=https://panel.dowozka.pl` | Allowlista origin dla panelu/PWA (dodawaj kolejne `__1`, `__2`). Bez niej w trybie hartowanym CORS jest **zamknięty**; w czystym dev (bez allowlisty) — otwarty dla wygody. |

Skutki hartowania:
- **Seed admina**: uruchamia się tylko poza trybem hartowanym i tylko gdy `Seed:AdminPassword` jest jawnie ustawione — nigdy z `Admin123!`. Na pilotażu konto admina zakłada się raz (istnieje w bazie), więc seed jest zbędny.
- **Limity nadużyć**: publiczne `POST` na logowanie/rejestrację/reset hasła/opinie są ograniczone (10/min na IP) — działa w każdym środowisku, bez dodatkowej konfiguracji.
- **Logi**: adresy e-mail w logach są maskowane (bez pełnych danych osobowych).

> Płatności rzeczywiste pozostają **wyłączone** do decyzji właściciela (patrz `PILOT_BUSINESS_MODEL.md`).

## 3. Zmienne środowiskowe (z `.env.example`)
| Zmienna | Opis |
|---|---|
| `ASPNETCORE_ENVIRONMENT` | `Development` / `Production`. Produkcja włącza guard. |
| `POSTGRES_DB/USER/PASSWORD` | Baza. Na produkcji ustaw silne hasło. |
| `RABBITMQ_USER/PASSWORD` | Broker. Silne hasło. |
| `JWT__SIGNINGKEY` | Klucz podpisujący JWT. **Długi, losowy, min. 32 znaki.** |
| `PAYMENTS__PROVIDER` | Dostawca płatności (`mock` tylko dev; prod: realny). |
| `PAYMENTS__MOCK__SECRET` | Sekret webhooka mocka (tylko dev). |
| `PAYMENTS__PUBLICURL` | Bazowy URL powrotu z płatności. |
| `IDENTITY__PUBLICURL` | URL w linkach e-mail (weryfikacja, reset hasła). |
| `GOOGLE__CLIENTID` | OAuth Client ID (logowanie Google; opcjonalne). |
| `SEED__ADMINEMAIL/PASSWORD` | Administrator startowy. Silne hasło. |

Wygenerowanie klucza JWT:
```bash
openssl rand -base64 48
```

## 4. Uruchomienie (Docker Compose)
```bash
cp .env.example .env        # uzupełnij sekrety
# ustaw ASPNETCORE_ENVIRONMENT=Production i realne wartości w .env
docker compose up -d --build
```
API: `http://<host>:5080` (za reverse-proxy z TLS na produkcji). Migracje bazy
wykonują się automatycznie przy starcie.

## 5. Integracje wymagające dokonfigurowania (odłożone)
Poniższe mają w kodzie **abstrakcję + mock** — realny adapter wymaga poświadczeń.
Nigdy nie commituj kluczy; ustaw je zmiennymi środowiskowymi.

### 5.1 Płatności — realny dostawca (np. Przelewy24)
- Zarejestruj sklep u dostawcy, pobierz `MerchantId` / `PosId` / `CRC` / klucz API.
- Zaimplementuj adapter `IPaymentProvider` (obok `MockPaymentProvider`) i ustaw
  `Payments__Provider` na jego klucz; sekrety podaj env-em.
- Webhook pozostaje **autorytatywny** (weryfikacja podpisu po stronie serwera).

### 5.2 Logowanie Google
- Utwórz OAuth 2.0 Client ID (typ: aplikacja) w Google Cloud Console.
- Ustaw `Google__ClientId`. Klient (mobile) przesyła zweryfikowany ID token do
  `POST /api/identity/google`.

### 5.3 Push (FCM) — aplikacja mobilna
- Projekt Firebase, plik konfiguracyjny FCM, klucz serwera.
- Zaimplementuj realny `IPushSender` (obok `LoggingPushSender`); rejestracja
  tokenów urządzeń już jest (`POST /api/notifications/devices`).

## 6. Bezpieczeństwo — checklista
- [ ] `ASPNETCORE_ENVIRONMENT=Production` (guard aktywny).
- [ ] Wszystkie sekrety z env/menedżera sekretów; `.env` poza gitem.
- [ ] TLS (reverse proxy: nginx/traefik) przed API; CORS zawężony do domen klienta.
- [ ] Silne hasła DB/broker/admin; rotacja kluczy JWT wg polityki.
- [ ] Kopie zapasowe bazy; monitoring `/health/ready`.
- [ ] Realny dostawca płatności zamiast `mock` przed przyjmowaniem płatności.
