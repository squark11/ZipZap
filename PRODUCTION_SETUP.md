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
| `Pilot:PaymentMode` ≠ `test` (W1) | — | ✗ start |
| `RateLimiting:Enabled=false` | ignorowane (limiter zawsze włączony) | ✗ start |

## 2a. Tryb publicznego pilotażu (hartowanie w Development)
Pilotaż bywa wdrażany w `Development`, ale **publiczny test nie może wystawiać narzędzi
deweloperskich ani mockowego przepływu płatności**. Ustaw:

| Zmienna | Efekt |
|---|---|
| `PILOT__PUBLIC=true` | Tryb hartowany: **Swagger wyłączony**; **cały mockowy przepływ płatności wyłączony** — dostawca `mock` nie jest rejestrowany (brak sesji, `/api/payments/webhook/mock` → 404 nawet z poprawnym podpisem `mock-dev-secret`, `/api/payments/mock/*` → 404); **seed administratora pominięty**; CORS tylko z allowlisty; limiter zawsze włączony. |
| `PILOT__ADMINPASSWORDCONFIRMED=true` | **Warunek operacyjny** (patrz niżej) — bez niego start zostaje przerwany. |
| `PILOT__PAYMENTMODE=test` | **Wymagane** (decyzja: W1). Zamówienia testowe bez opłaty — **tylko konta z rolą `Tester`** (Panel → Użytkownicy → Nadaj rolę „Tester pilotażu"); reszta może przeglądać, ale nie zamawiać. Aplikacja nie pokazuje ekranu płatności. Zasady grupy: `PILOT_BUSINESS_MODEL.md` §9.6. |
| `PILOT__TESTORDERSPERTESTERPERDAY=3` | Limit zamówień testowych na testera w 24 h (koszt towaru ponosi operator). |
| `CORS__ALLOWEDORIGINS__0=https://panel.dowozka.pl` | Allowlista origin dla panelu/PWA (kolejne `__1`, `__2`). Bez niej w trybie hartowanym CORS jest **zamknięty**. |
| `JWT__SIGNINGKEY`, `CONNECTIONSTRINGS__POSTGRES` | Muszą być produkcyjne (guard powyżej). |

**Zamówienia w pilotażu są testowe i nieksięgowe (W1).** Zamówienie zapisuje tryb `test`;
**nie powstaje żaden wpis płatności**, sesja ani autoryzacja, a po dostawie **nie** jest
księgowana prowizja ani rozliczenie. Nic nie udaje autoryzacji płatności.

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

> ⚠ **Wdrożenie = migracja produkcyjnej bazy.** Ponieważ migracje uruchamiają się przy starcie,
> każdy deploy nowego kodu (np. auto-deploy Render z `main`) od razu zmienia schemat produkcyjnej bazy.
> Branch pilotażowy zawiera migracje, które **nie zostały jeszcze zastosowane na produkcji**:
> `Ordering_PaymentMode` (kolumna `orders.PaymentMode`, domyślnie `online`) i
> `Ordering_PurchasingRounds` (tabele `purchasing_schedule` z wierszem startowym i `purchasing_rounds`,
> kolumny rundy w `orders` — wszystkie nullable), `Ordering_RoundPicking` (nowe tabele
> `order_item_picks` i `order_item_pick_history`) oraz `Delivery_Dispatch` (kolumny okna/kolejności/wersji w
> `delivery.deliveries` — nullable lub z wartością domyślną — i tabela `delivery_history`) oraz
> `Delivery_AdminOverride` (nullable kolumna `Reason` w `delivery_history`). Zdarzenie
> `OrderReadyForPickup` ma nowe pola opcjonalne (okno dostawy) — starsze wiadomości w outboxie są zgodne.
> Gałąź `pilot-outbox-deadletter` dodaje w KAŻDYM module (Catalog, Delivery, Identity, Ordering, Payments)
> dwie migracje tabeli `outbox_messages`: `*_OutboxDeadLetter` (nullable `NextAttemptAtUtc`, `DeadLetteredAtUtc`)
> i `*_OutboxLeaseAndManualRetry` (nullable `LockedUntilUtc`, `LastManualRetryAtUtc`, `LastManualRetryByUserId`)
> — razem 10 migracji. Wszystkie zmiany są addytywne (bez usuwania danych). Przed
> scaleniem do `main` wykonaj kopię bazy (Neon: branch/snapshot) i scalaj dopiero po zatwierdzeniu.

### Outbox: ponawianie i odłożone zdarzenia
Brak nowych zmiennych środowiskowych. Reguły (decyzja właściciela, 2026-09-28):
- Błąd **trwały** (nieznany typ zdarzenia, nieczytelny ładunek) → wiadomość od razu trafia do odłożonych
  (`DeadLetteredAtUtc`) i zostaje w bazie.
- Każdy inny błąd (awaria bazy, usługi zewnętrznej, handlera) → ponawianie **bez limitu** z narastającym odstępem
  10 s → 20 s → … maks. co 30 min (`NextAttemptAtUtc`); nigdy automatycznie do odłożonych. Wiadomość czekająca na
  termin nie blokuje kolejnych.
- Pole `Error` zawiera **wyłącznie kod kategorii** (`unknown_type`, `unreadable_payload`, `database`, `conflict`,
  `external_service`, `timeout`, `handler_error`) — nigdy treść wyjątku ani ładunek. Logi: id i typ wiadomości,
  kategoria i nazwa typu wyjątku (bez treści).

**Wiele instancji API (np. nakładanie się starej i nowej instancji podczas deployu Render).** Partia jest pobierana
atomowo (`UPDATE … FOR UPDATE SKIP LOCKED`) z 5-minutową dzierżawą (`LockedUntilUtc`): druga instancja pomija
wiadomości pobrane przez pierwszą. Gdy instancja padnie, dzierżawa wygasa i wiadomość wraca (nic nie ginie).
Oznaczenie „wysłana" zapisuje się po KAŻDEJ wiadomości. Duplikat jest nadal możliwy (gwarancja „co najmniej raz"):
publikacja się udała, a zapis oznaczenia nie (awaria w tej chwili), albo pojedyncza obsługa trwała > 5 min.
Dlatego dyspozytor zdarzeń pomija handler, który już obsłużył dane zdarzenie (inbox `messaging.inbox_messages`,
klucz = Id zdarzenia + handler) — także przy ponowieniu po awarii jednego z kilku handlerów i przy ręcznym
ponowieniu. Idempotencja samych handlerów:

| Handler | Skutek powtórki bez inboxa |
|---|---|
| Delivery ← `OrderReadyForPickup` | bezpieczny (sprawdzenie + unikalny `OrderId`) |
| Ordering ← `PaymentAuthorized` / `OrderPickedUp` / `OrderDelivered` | bezpieczny sekwencyjnie (strażnik stanu); równolegle mógłby dopisać podwójny wpis historii |
| Ordering ← zdarzenia Catalog (projekcje) | wynik ten sam, ale spóźniona powtórka starszego zdarzenia nadpisałaby nowsze dane |
| Payments ← `OrderPlaced` / `OrderDelivered` | bezpieczny (unikalny `OrderId`; W1 nie tworzy płatności); przy realnej bramce równoległa powtórka mogłaby założyć drugą sesję u dostawcy |
| Notifications ← wszystkie | **nieidempotentny** — drugie powiadomienie / push |

Pozostałe okno: skutek handlera zapisany, a znacznik w inboxie nie (awaria dokładnie w tej chwili) — wtedy np.
powiadomienie może pójść drugi raz. Ograniczenie operacyjne: obsługa jednego zdarzenia powinna trwać wyraźnie
krócej niż 5 min (dzierżawa; partia przerywa pracę po 2,5 min). Domyślne limity pojedynczego wywołania to 2 min
(SMTP) i 100 s (HTTP), ale handler z kilkoma wywołaniami może się zbliżyć do granicy — przy realnych integracjach
(bramka płatności, e-mail) ustaw krótsze limity czasu.

**Panel administratora → „Zdarzenia"** (tylko rola Admin):
- lista odłożonych i osobno „ponawianych długo" (≥ 10 nieudanych prób — nadal ponawiane automatycznie, NIE są
  odkładane), liczniki per moduł; tylko metadane: moduł, typ, identyfikator zdarzenia, czasy, liczba prób,
  kategoria błędu — bez ładunku, treści błędów i danych klientów;
- „Ponów" działa na JEDNO odłożone zdarzenie, warunkowo (liczba prób musi się zgadzać z tą widzianą w panelu —
  podwójne kliknięcie lub dwóch administratorów naraz ponawia dokładnie raz), zapisuje autora i czas w wierszu
  (`LastManualRetry*`) oraz w audycie (`outbox.manual_retry`). Brak ponawiania masowego. Ponawiaj dopiero po usunięciu
  przyczyny (np. wdrożeniu poprawki) — ponowienie może ponownie uruchomić obsługę zdarzenia.
- Awaryjnie (gdy panel niedostępny) to samo w SQL, np. w module zamówień:
  ```sql
  UPDATE ordering.outbox_messages
     SET "DeadLetteredAtUtc" = NULL, "NextAttemptAtUtc" = NULL, "LockedUntilUtc" = NULL,
         "LastManualRetryAtUtc" = now()
   WHERE "Id" = '<id z panelu lub logu>' AND "DeadLetteredAtUtc" IS NOT NULL AND "ProcessedAtUtc" IS NULL;
  ```
  (odpowiednio `delivery.`, `payments.`, `identity.`, `catalog.`).

**Stare wartości `Error` po wdrożeniu.** Wersja obecnie na produkcji zapisywała do `Error` surowe
`Exception.Message` (np. odrzucenie SMTP z adresem e-mail) — tylko dla wiadomości niewysłanych; po udanej wysyłce
pole było czyszczone. Po wdrożeniu każda taka wiadomość jest ponawiana od razu i pole zostaje nadpisane kategorią
albo wyczyszczone, a panel i tak nie pokazuje wartości spoza listy kodów („Starszy błąd — treść ukryta").
Osobne czyszczenie **nie powinno być potrzebne**; po wdrożeniu sprawdź zapytaniem tylko do odczytu (dla każdego
schematu), że wynik to 0:
```sql
SELECT count(*) FROM ordering.outbox_messages
 WHERE "Error" IS NOT NULL AND "Error" NOT IN
   ('unknown_type','unreadable_payload','database','conflict','external_service','timeout','handler_error');
```
Surowe treści mogą nadal być w kopiach zapasowych bazy i w starych logach Render (dawny kod logował pełny wyjątek) —
decyzja o ich retencji należy do właściciela. Ścieżka RabbitMQ (włączana tylko przez `RabbitMq:Host`) nadal wpisuje
treść wyjątku do nagłówka `x-error` kolejki martwych w brokerze — do poprawy, jeśli broker zostanie włączony.

> ⚠ **Panel logowania (wersja obecnie wdrożona)** wypełniał formularz i pokazywał podpowiedź z domyślnym
> kontem admina (`admin@zipzap.local` / `Admin123!`) także na produkcji. Na branchu pilotażowym jest to
> ograniczone do `localhost`, ale **do czasu wdrożenia poprawki** jedyną ochroną jest zmiana hasła admina
> produkcji — zrób to niezależnie od wdrożenia.

### Rundy zakupowe
Harmonogram rund (12:00/16:00, cutoff 30 min, pon–sob, dostawa ≥ 60 min po rundzie — **wartości
startowe do potwierdzenia**) jest w bazie i edytowalny w panelu (Konfiguracja → Rundy zakupowe).
Nie wymaga zmiennych środowiskowych. Strefa `Europe/Warsaw` — obraz `aspnet:8.0` zawiera tzdata/ICU.

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
