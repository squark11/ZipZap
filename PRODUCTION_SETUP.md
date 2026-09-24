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

## 2. Guard fail-fast (produkcja i publiczny pilotaż)
API **odmówi startu** (i wypisze, co ustawić — bez wartości sekretów), gdy
`ASPNETCORE_ENVIRONMENT=Production` **lub** `Pilot:Public=true` — także wtedy, gdy
środowisko nazywa się `Development`. Reguły (`Security/HardeningGuard.cs`):

| Reguła | Produkcja | Publiczny pilotaż |
|---|---|---|
| `Jwt:SigningKey` z `CHANGE_ME`/`DEV_ONLY` lub < 32 znaki | ✗ start | ✗ start |
| `ConnectionStrings:Postgres` z hasłem `zipzap` (parsowane, odporne na spacje) | ✗ start | ✗ start |
| `RabbitMq:Password` puste/`zipzap` | ✗ start | ✗ start, jeśli broker używany (`RabbitMq:Host`) |
| `Payments:Provider = mock` / `Payments:Mock:Secret = mock-dev-secret` | ✗ start | n/d — mock w pilotażu **nie istnieje** |
| `Seed:AdminPassword = Admin123!` | ✗ start | n/d — seed pomijany |
| brak `Pilot:AdminPasswordConfirmed=true` | — | ✗ start |
| `RateLimiting:Enabled=false` | ignorowane (limiter zawsze włączony) | ✗ start |

## 2a. Tryb publicznego pilotażu (hartowanie w Development)
Pilotaż bywa wdrażany w `Development`, ale **publiczny test nie może wystawiać narzędzi
deweloperskich ani mockowego przepływu płatności**. Ustaw:

| Zmienna | Efekt |
|---|---|
| `PILOT__PUBLIC=true` | Tryb hartowany: **Swagger wyłączony**; **cały mockowy przepływ płatności wyłączony** — dostawca `mock` nie jest rejestrowany (brak sesji, `/api/payments/webhook/mock` → 404 nawet z poprawnym podpisem `mock-dev-secret`, `/api/payments/mock/*` → 404); **seed administratora pominięty**; CORS tylko z allowlisty; limiter zawsze włączony. |
| `PILOT__ADMINPASSWORDCONFIRMED=true` | **Warunek operacyjny** (patrz niżej) — bez niego start zostaje przerwany. |
| `CORS__ALLOWEDORIGINS__0=https://panel.dowozka.pl` | Allowlista origin dla panelu/PWA (kolejne `__1`, `__2`). Bez niej w trybie hartowanym CORS jest **zamknięty**. |
| `JWT__SIGNINGKEY`, `CONNECTIONSTRINGS__POSTGRES` | Muszą być produkcyjne (guard powyżej). |

**Zamówienia w pilotażu są testowe i nieksięgowe.** Bez dostawcy płatności zamówienie
tworzy wpis płatności `Pending` **bez sesji i bez autoryzacji**; po dostawie **nie** jest
księgowana prowizja ani rozliczenie (prowizja tylko dla płatności potwierdzonej webhookiem).
Nic nie udaje autoryzacji płatności.

### Procedura przed publicznym testem — konto administratora
Pominięcie seeda **nie usuwa ani nie zmienia** istniejącego konta `admin@zipzap.local`
(ani innych kont administratorów). Przed ustawieniem `PILOT__ADMINPASSWORDCONFIRMED=true`:
1. Zaloguj się na każde konto administratora i zmień hasło na **unikalne i silne**
   (Konfiguracja → Konto i bezpieczeństwo), najlepiej z włączonym 2FA.
2. Konta administratorów, których nie używasz, **dezaktywuj** (Użytkownicy).
3. Dopiero wtedy ustaw `PILOT__ADMINPASSWORDCONFIRMED=true` i wdroż.

Dodatkowo usługa sama sprawdza przy starcie, czy któreś aktywne konto administratora ma
znane hasło domyślne (`Admin123!`). Jeśli tak — `/health/ready` zwraca **503** do czasu
zmiany hasła (w logach tylko liczba kont, bez haseł i adresów).

### Gotowość usługi (`/health/ready`)
`/health/ready` zwraca **503**, gdy: baza jest nieosiągalna, **migracja któregokolwiek modułu
się nie powiodła** (proces działa dalej, ale nie zgłasza gotowości) lub (w trybie publicznym)
admin ma domyślne hasło. Odpowiedź publiczna nie zawiera szczegółów — są w logach.
➜ **Na hostingu ustaw health check na `/health/ready`** (nie `/health`), inaczej te warunki
nie zablokują ruchu.

### Limiter i zaufanie do nagłówków proxy
- Limit: 10 × `POST`/min na IP klienta dla logowania, rejestracji, resetu hasła i opinii
  (`RATELIMITING__PERMITPERMINUTE`). W trybie hartowanym nie da się go wyłączyć.
- IP klienta: `X-Forwarded-For` z **`ForwardLimit=1`** — liczy się tylko ostatni wpis
  dopisany przez proxy hostingu; adresy dopisane przez klienta po lewej są ignorowane, więc
  podrobiony nagłówek nie omija limitu. Znane adresy proxy można podać w
  `FORWARDEDHEADERS__KNOWNPROXIES__0` / `FORWARDEDHEADERS__KNOWNNETWORKS__0` (CIDR).
  Założenie: kontener jest osiągalny **wyłącznie przez proxy hostingu** (tak jest na Render).

### Logi
Adresy e-mail są maskowane; treść wiadomości (linki z tokenami weryfikacji/resetu) **nigdy**
nie trafia do logów.

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
