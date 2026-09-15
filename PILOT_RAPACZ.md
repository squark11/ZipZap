# Pilot Rapacz — roadmapa: od stanu bieżącego do aplikacji na telefonach

> Cel: **zainstalowana aplikacja na telefonach** i jeden klient pilotażowy — **sklep Rapacz** (wiele lokalizacji) —
> u którego sprawdzimy, **czy taka sprzedaż ma sens i czy jest zainteresowanie**.
> Zasada: po przekazaniu Rapaczowi dostępu do konta, ma on **bardzo prosto** podpiąć integrację ze swoim
> systemem (płatności), a klient ma **bez tarcia** złożyć zamówienie. Uwagi zbieramy **w aplikacji**.
>
> Ten plik jest planem pilotażu. Pełna roadmapa produktu: [ROADMAP.md](ROADMAP.md).

## 0. Definicja „gotowe do pilotażu" (kryteria wejścia u Rapacza)
- [ ] Klient pobiera/instaluje aplikację na telefonie (PWA „dodaj do ekranu" **oraz** APK do testów).
- [ ] Rapacz po zalogowaniu **samodzielnie** (kreator) ustawia: dane sklepu → dokumenty prawne → **własną bramkę płatniczą** → strefy/godziny dostaw → publikacja.
- [ ] Rapacz dodaje **wszystkie swoje lokalizacje** i przełącza się między nimi.
- [ ] Klient widzi sklepy **proponowane wg odległości** od siebie.
- [ ] Ścieżka zamówienia działa E2E: rejestracja → sklep → koszyk → **zgoda na dokumenty** → **płatność bramką Rapacza** → śledzenie.
- [ ] Klient może **zgłosić uwagę w aplikacji** (co poprawić), Rapacz/admin ją widzi.
- [ ] Formularze publiczne chronione **captchą** (konfigurowalną w panelu).
- [ ] Wszystko brandowalne przez sklep (logo, zdjęcia) — mocki tylko jako start.

---

## 1. Stan bieżący (fundament — gotowe)
- **Backend** modularny (.NET 8, schema-per-module, outbox), **58 testów** (40 integracyjnych + 18 jednostkowych).
- **Aplikacja klienta** (Flutter, PWA instalowalna) + **panel sprzedawcy/admina** (Angular), **23 testy** mobilne.
- **Płatności**: mock + **routing per-sklep** (każdy sklep podpina swoją bramkę; tokeny szyfrowane, write-only) — gotowe pod realny adapter.
- **Rozliczenia**: miesięczna faktura ZipZap→sklep (Plan A 25 zł/dostawa lub B prowizja).
- **Dokumenty prawne per-sklep + twarda zgoda przy zakupie** (warunek podpięcia bramki).
- **Konfiguracja w panelu, nie w env** (ustawienia platformy, integracje; Google Client ID w panelu).
- **Import/eksport** asortymentu + eksport zamówień (księgowość).
- **Rejestracja i logowanie klienta** (e-mail+hasło, weryfikacja e-mail, reset hasła) — **już działa**; logowanie Google skonfigurowane (przycisk do podpięcia w apce).

## 2. Analiza — czego brakuje do pilotażu (luki)
| # | Luka | Dlaczego ważne dla pilotażu |
|---|------|------------------------------|
| A | **Onboarding sklepu rozproszony** po zakładkach | Rapacz ma podpiąć integrację „bardzo prosto" → potrzebny **kreator** krok-po-kroku |
| B | **Multi-lokalizacja** tylko połowicznie | `UserRole` już dopuszcza pracownika w wielu sklepach, ale `ICurrentUser.StoreId` zwraca **jeden**; brak UI „dodaj lokalizację" i przełącznika |
| C | **Brak geolokalizacji** | `Store` nie ma `Latitude/Longitude`; brak lokalizacji klienta → nie da się proponować wg odległości |
| D | **Brak modułu uwag** | wymóg: uwagi składane **w aplikacji** |
| E | **Brak captchy** | wymóg: formularze publiczne chronione, konfiguracja w panelu |
| F | **Sklep bez logo/brandingu** | `Store` nie ma `LogoUrl` (produkt ma `ImageUrl`); wszystko ma być edytowalne przez sklep |
| G | **Instalacja natywna** | PWA jest; brak natywnego **Android APK/AAB** (podpis, ikona, uprawnienia lokalizacji) dla wygody testerów |
| H | **Rejestracja — zakres do potwierdzenia** | klient działa; pytanie czy dochodzi **samodzielna rejestracja sklepu** |

---

## 3. Roadmapa pilotażu (slice po slicu — kolejność wykonania)

Każdy punkt = jeden „next" (backend + panel + apka + testy + smoke), zgodnie z dotychczasową kadencją.

### P1 — Model sklepu pod pilotaż `[P0]` ✅ (2026-09-14)
- ✅ `Store`: **`LogoUrl`**, **`Latitude`/`Longitude`** (migracja `Catalog_StoreBrandingLocation`); w `StoreDto` + create/update; walidacja zakresu współrzędnych.
- ✅ Panel „Sklepy": logo (URL) + współrzędne (na razie ręcznie; auto z adresu → P4) + miniatura logo/GPS w tabeli.
- ✅ Apka: logo na kartach sklepów i w nagłówku (`StoreLogo` z fallbackiem do ikony marki). Testy: `StoreProfileTests` (3).
- ↪ **DeliveryMethod** świadomie pominięty jako osobne pole — to **plan rozliczeń A/B** (jedno źródło prawdy); wystawimy klientowi z planu przy **P4**.

### P2 — Multi-lokalizacja Rapacza `[P0]` ✅ (2026-09-14)
- ✅ `ICurrentUser.StoreIds` (zbiór) + `ManagesStore(storeId)`; autoryzacja per-sklep po zbiorze w Catalog/Ordering/Payments i endpointach hosta (token już niósł wiele `store_id`).
- ✅ „**Dodaj lokalizację**": `POST /api/merchant/stores` tworzy kolejny `Store` i przypisuje bieżącego użytkownika jako `StoreEmployee` (`IdentityService.AssignStoreEmployeeAsync`). Panel: „+ Lokalizacja" + odświeżenie tokenu (nowy sklep w claimach) + wybór lokalizacji. Przełącznik lokalizacji istniał (filtr wg `storeIds`).
- ✅ Model lekki (wspólny admin wielu sklepów); pełny „Merchant/Organization" odłożony. Testy: `MultiLocationTests` (3).
- ↪ Edycja profilu lokalizacji (logo/GPS/godziny) przez samego sprzedawcę — w **P3** (kreator), dziś logo/GPS ustawia Admin w „Sklepy".

### P3 — Onboarding sklepu + samodzielna rejestracja `[P0]` 🔧 (2026-09-14: rdzeń ✅)
- ✅ Zakładka **„Start"** (`onboarding.ts`) — **checklista gotowości** (`GET /api/stores/{id}/readiness`: logo, lokalizacja, produkty, dokumenty, płatność, strefa, termin, publikacja; `readyToSell` = wymagane kroki), **Publikuj** (status Otwarty+aktywny).
- ✅ **Samodzielna edycja profilu** przez sprzedawcę: logo, współrzędne, status (PATCH `/catalog/stores/{id}`).
- ✅ **Strefy i terminy dostaw** — dodawanie w panelu (endpointy istniały, brakowało UI): `POST /ordering/stores/{id}/zones|slots`.
- ↪ Bramka płatnicza (mock, sandbox przed wizytą) i dokumenty — konfigurowane w zakładce Integracje (checklista linkuje).
- ⬜ **P3b**: publiczny **„Załóż sklep"** (self-service konto właściciela + pierwszy sklep, chroniony captchą P6) + dopracowanie UX rejestracji klienta (→ też P9).

### P4 — Sklepy wg odległości `[P0]` 🔧 (2026-09-14: rdzeń ✅)
- ✅ **Sortowanie wg dystansu**: `GET /catalog/stores?lat=&lng=` — Haversine, `distanceKm` w DTO, najbliższe na górze (bez współrzędnych na koniec).
- ✅ **Lokalizacja klienta**: `geolocator` (GPS/przeglądarka), baner „Pokaż sklepy najbliżej Ciebie" (łagodny fallback bez zgody) + **dystans na karcie** + „W Twojej okolicy". Uprawnienia w AndroidManifest.
- ⬜ **P4b — geokoder** (adres → `lat/lng`, provider **konfigurowalny w panelu**; Nominatim/OSM na pilotaż) do auto-uzupełniania współrzędnych sklepu (dziś współrzędne wpisuje sprzedawca w zakładce „Start").

### P5 — Uwagi w aplikacji (feedback) `[P0]` ✅ (2026-09-14)
- ✅ Nowy **moduł Feedback** (schema `feedback`, migracja `Feedback_Initial`): `FeedbackItem` (typ Bug/Idea/Other, treść, kontekst ekran/wersja/platforma, zgłaszający, status New/InProgress/Closed).
- ✅ Endpointy: `POST /api/feedback` (**publiczne** — id klienta dołączany jeśli zalogowany), `GET`/`PATCH /api/feedback` (Admin=wszystkie, pracownik=uwagi swoich sklepów).
- ✅ Apka: ekran „**Zgłoś uwagę**" (typ + treść + e-mail kontaktowy) — wejście w „Konto". Panel: zakładka „**Uwagi**" (lista + zmiana statusu). Testy: `FeedbackTests` (4).

### P6 — Captcha na formularzach `[P0]` 🔧 (2026-09-14: rdzeń ✅)
- ✅ **Cloudflare Turnstile konfigurowalny w panelu** (Konfiguracja → Integracje platformy): provider + **site key** (jawny) + **secret** (szyfrowany IDataProtector, **write-only**). `/config/public` udostępnia site key aplikacji.
- ✅ Weryfikacja **po stronie backendu** (`ICaptchaVerifier` → Cloudflare siteverify; **no-op gdy captcha wyłączona** — brak tarcia w dev) wpięta w `POST /identity/register` i `POST /feedback`.
- ✅ Apka: widget `cloudflare_turnstile` na **rejestracji** i **„Zgłoś uwagę"** (renderowany tylko gdy captcha włączona; token wymagany przed wysłaniem).
- ⬜ **Weryfikacja na żywo** z prawdziwymi kluczami Turnstile (urządzenie/PWA) — nie dało się przetestować lokalnie. **Nie włączaj captchy w panelu, dopóki apka z widgetem nie jest wdrożona.** Opcjonalnie: kontakt/reset hasła.

### P7 — Branding i treści edytowalne + seed mockami `[P0]` 🔧 (2026-09-14: rdzeń ✅)
- ✅ **Seed pilotażu** `POST /api/admin/seed/pilot` (Admin, idempotentny po slug): **Rapacz — Rynek** + **Lewiatan — Podwawelskie** z logo, 4 produktami ze **zdjęciami**, strefą „Centrum" i terminem dostawy — gotowe do sprzedaży, widoczne wg odległości (współrzędne Krakowa). Opis oznacza je jako **demo do podmiany**.
- ✅ Grafiki serwowane z API (`wwwroot/mock/{stores,food}/*`, `UseStaticFiles`); przycisk „Zasiej sklepy demo" w panelu (Konfiguracja).
- ✅ Apka: nagłówek sklepu pokazuje logo, kafelek produktu pokazuje **zdjęcie** (`Image.network` + fallback). Zweryfikowane na żywo (logo Rapacza wczytane).
- ↪ Edytowalne przez sklep: logo/GPS (P1/P3), `imageUrl` produktu (API/seed). ⬜ pole zdjęcia w formularzu produktu w panelu + upload.

### P8 — Instalacja na telefonach `[P0]` 🔧 (2026-09-14: PWA ✅)
- ✅ **PWA gotowa do instalacji**: ikony (192/512 + maskable + favicon) i ikony launchera Androida **przegenerowane z nowego logo** (sharp/Node); manifest + `theme_color` #14b9ba + nazwa Dowózka.pl; `sw.js` (offline‑powłoka). Tester: „Dodaj do ekranu głównego" (Android Chrome / iOS Safari).
- ✅ Android przygotowany: `android:label="Dowózka.pl"`; ikony launchera; `applicationId` `pl.zipzap.zipzap` (techniczny, zgodny z klientem Google OAuth + SHA-1). Przewodnik build/podpis/dystrybucja w `DEPLOY.md`.
- ⬜ **Realny build APK/AAB** — na maszynie z Android SDK (ten komputer nie ma toolchainu). iOS **po pilotażu**.

### P9 — Twarda ścieżka E2E + poprawa tarcia `[P0]` 🔧 (2026-09-14: rdzeń ✅)
- ✅ **Test E2E** (`CustomerJourneyE2ETests`): rejestracja klienta → sklep widoczny **wg odległości** → koszyk (dodanie produktu) → **checkout ze zgodą** (suma 2×6 + 8 dostawa = 20 zł) → śledzenie zamówienia → **potwierdzenie przez sklep** (Placed→Confirmed). Deterministyczny (read‑model Ordering seedowany).
- ✅ UX rejestracji: przełącznik „Załóż konto" + podpowiedź „Hasło min. 6 znaków"; puste/błędne/offline stany już są (EmptyView/ErrorView/connectivity banner z R2).
- ⬜ Płatność **realną bramką Rapacza (sandbox)** w ścieżce — tuż przed wizytą (mock działa: `PaymentsWebhookTests`). ⬜ pełny przegląd tarcia na urządzeniu.

### P10 — Gotowość operacyjna do wizyty u Rapacza `[P0]`
- **Deploy publiczny** (HTTPS): backend + DB + web (dane deployu masz gotowe).
- **Konto Rapacza** utworzone + **instrukcja „1 strona"** dla sklepu (jak dodać lokalizacje, podpiąć płatności, opublikować).
- Monitoring `/health/ready`, zbieranie uwag włączone, szybka pętla poprawek podczas pilotażu.

---

## 4. Zależności (co po czym)
```
P1 (model: logo/geo/metoda) ─┬─> P4 (odległość: potrzebuje lat/lng)
                             └─> P7 (branding: potrzebuje LogoUrl)
P2 (multi-lokalizacja) ──────────> P3 (kreator dodaje kolejne lokalizacje)
P3 (kreator onboardingu) ────────> P10 (Rapacz sam się konfiguruje)
P6 (captcha) ────────────────────> P9 (rejestracja w ścieżce E2E)
P1..P8 ──────────────────────────> P9 (E2E) ──> P10 (wizyta)
```
Sugerowana kolejność realizacji: **P1 → P2 → P3 → P4 → P5 → P6 → P7 → P8 → P9 → P10**
(model i multi-lokalizacja najpierw, bo od nich zależy reszta).

## 5. Decyzje (✅ = zatwierdzone przez właściciela 2026-09-14)
1. ✅ **Rejestracja — „jedno i drugie"**: dopracować rejestrację **klienta** ORAZ dodać **samodzielną rejestrację sklepu** (self-service merchant) już w pilotażu. → wpięte w **P3** (self-service sklepu) i **P9** (UX rejestracji klienta).
2. ✅ **Multi-lokalizacja — model lekki**: wspólny admin wielu sklepów (`StoreEmployee` wielu lokalizacji) + przełącznik + „Dodaj lokalizację". Pełna „Organizacja" — po pilotażu. → **P2**.
3. ✅ **Płatności — mock teraz, realny sandbox przed wizytą**: UX na mocku; sandbox Przelewy24 Rapacza podpinamy tuż przed wizytą. → **P3/P9**.
4. ❓ **Captcha** — provider? *(rekom.: Cloudflare Turnstile — darmowy, prywatny)* — potwierdzimy przy **P6**.
5. ❓ **Geokoder** — źródło współrzędnych? *(rekom.: Nominatim/OSM na pilotaż; Google/Mapbox produkcyjnie)* — przy **P4**.
6. ❓ **Instalacja** — APK i/czy PWA? *(rekom.: oba)* — przy **P8**.

## 6. Świadomie poza pilotażem
iOS natywny; wielu merchantów/self-service sklepów; opłaty za dostawę wg dystansu; POS/fiskalizacja (GOPOS); pełny model Organizacji; realny adapter Przelewy24 produkcyjnie (chyba że Rapacz wymaga na pilotaż).
