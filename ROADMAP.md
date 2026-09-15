# ZipZap — ROADMAP (jedyne źródło prawdy)

> **Zasada pracy:** cała robota idzie **zgodnie z tym plikiem**. Nowe pomysły/zadania
> **dopisuj TUTAJ** w odpowiedniej fazie (nie twórz osobnych roadmapów). Dokumenty
> analityczne (linki na końcu) są referencją — actionable to‑do żyje w tym pliku.
>
> Legenda: ✅ zrobione · 🔧 w toku · ⬜ do zrobienia · P0/P1/P2 = priorytet.

## Status
- ✅ **MVP** (silnik + aplikacja Flutter + panel Angular + hardening) — Fazy A–G, F1/F2.
- 🔧 **Rozwój do zaawansowanej aplikacji** — Fazy **R1–R11** (Dev·UX·UI·Grafika + deploy + mobile + prawne).
- ➕ **Faza H** (dwa plany rozliczeń + restauracje + **fale dostaw**) — wpięta w **R4** i **R7**.
- 🎨 **Rebranding ZipZap → Dowózka.pl** ✅ (2026-09-14): logo wektorowe (wózek + strzałka „dowozu") w [brand/](brand/), kolor **#14b9ba**, font **Poppins**, nazwa w apce i panelu. Zakres warstwy widocznej; wewnętrzne identyfikatory `ZipZap.*` bez zmian.
- 🎯 **Pilot Rapacz** — sekcja niżej; analiza/kryteria w [PILOT_RAPACZ.md](PILOT_RAPACZ.md). ✅ **żywy deploy (2026-09-15)**: API `dowozka-api.fly.dev` + PWA `dowozka.fly.dev` (Fly.io + Neon).
- 🎯 **PRIORYTET BIEŻĄCY (2026-09-15): Role, rejestracja per‑kanał i modularny panel (web)** — sekcja „🔐" niżej.

---

## 🎯 Pilot Rapacz [P0 — priorytet bieżący]
Cel: **aplikacja na telefonach** + 1 klient pilotażowy **Rapacz** (wiele lokalizacji), test „czy sprzedaż ma sens".
Analiza luk, zależności i decyzje: **[PILOT_RAPACZ.md](PILOT_RAPACZ.md)**. Kolejność: P1→P10.
- ✅ **P1 Model sklepu**: `Store.LogoUrl` + `Latitude/Longitude` (migracja `Catalog_StoreBrandingLocation`), w `StoreDto` + create/update + panel „Sklepy" (logo/GPS, miniatura) + apka (logo na kartach i w nagłówku, `StoreLogo` z fallbackiem). Walidacja zakresu współrzędnych. Testy: `StoreProfileTests` (3). *(Metoda dostawy = plan rozliczeń A/B — bez osobnego pola, by nie dublować źródła prawdy; wystawimy klientowi z planu przy P4.)*
- ✅ **P2 Multi‑lokalizacja Rapacza**: token już niósł wiele `store_id`; `ICurrentUser.StoreIds` (zbiór) + `ManagesStore(storeId)` — autoryzacja po zbiorze w Catalog/Ordering/Payments/host. Endpoint `POST /api/merchant/stores` (właściciel/Admin dodaje **nową lokalizację** przypisaną do siebie) + `IdentityService.AssignStoreEmployeeAsync`. Panel: przełącznik lokalizacji (istniał) + „**+ Lokalizacja**" z odświeżeniem tokenu (`Api.refresh`). Testy: `MultiLocationTests` (3).
- 🔧 **P3 Onboarding + samodzielna rejestracja sklepu**: ✅ zakładka **„Start"** — checklista gotowości (`GET /api/stores/{id}/readiness`, agreguje produkty/dokumenty/płatność/strefy/sloty/status), **samodzielna edycja profilu** (logo/GPS/status), **strefy i terminy dostaw** (dodawanie w panelu — dotąd brak UI), **Publikuj** (Otwarty). Testy: `StoreReadinessTests` (3). ⬜ **P3b**: publiczny „Załóż sklep" (self-service konto+sklep) + dopracowanie UX rejestracji klienta.
- 🔧 **P4 Sklepy wg odległości**: ✅ backend — `GET /catalog/stores?lat=&lng=` liczy odległość (Haversine, `distanceKm` w DTO) i sortuje od najbliższego (bez współrzędnych → koniec/alfabetycznie); ✅ apka — lokalizacja klienta (`geolocator`, GPS/przeglądarka), baner „Pokaż sklepy najbliżej Ciebie" + dystans na kartach; uprawnienia w AndroidManifest. Testy: `StoreDistanceTests` (2) + mobile distance (2). ⬜ **P4b**: geokoder (adres→współrzędne, konfig. w panelu — OSM/Nominatim) do auto-uzupełniania współrzędnych sklepu.
- ✅ **P5 Uwagi w aplikacji** (feedback): nowy **moduł Feedback** (schema `feedback`, migracja) — `POST /api/feedback` (publiczne, typ Błąd/Pomysł/Inne + kontekst: ekran/wersja/platforma), `GET`/`PATCH` (Admin=wszystko, pracownik=swoje sklepy). Apka: ekran „Zgłoś uwagę" (Konto → wejście). Panel: zakładka **„Uwagi"** (lista + zmiana statusu New/InProgress/Closed). Testy: `FeedbackTests` (4).
- 🔧 **P6 Captcha na formularzach**: ✅ **Cloudflare Turnstile konfigurowalny w panelu** (provider + site key jawny + **secret szyfrowany, write-only**; `GET/PUT /admin/config/integrations`, `/config/public` udostępnia site key). ✅ Weryfikacja backendu (`ICaptchaVerifier`→siteverify, **no-op gdy wyłączona**) wpięta w `POST /identity/register` i `POST /feedback`. ✅ Apka: widget Turnstile (`cloudflare_turnstile`) na rejestracji i „Zgłoś uwagę" (pokazywany, gdy captcha włączona). Testy: `CaptchaConfigTests` (3). ⬜ **weryfikacja na żywo** z prawdziwymi kluczami Turnstile (urządzenie/PWA) + ewentualnie kontakt/reset hasła.
- 🔧 **P7 Branding edytowalny + seed mockami**: ✅ **seed pilotażu** `POST /api/admin/seed/pilot` (Admin, idempotentny) — tworzy **Rapacz** i **Lewiatan** z logo, produktami ze zdjęciami, strefą i terminem dostawy; grafiki demo serwowane z API (`wwwroot/mock/*`, `UseStaticFiles`). ✅ Panel: „Zasiej sklepy demo" (Konfiguracja). ✅ Apka: kafelek produktu renderuje **zdjęcie** (`imageUrl` + fallback). ✅ Edytowalne: logo/GPS sklepu (P1/P3), `imageUrl` produktu przez API. Testy: `PilotSeedTests` (3). ⬜ pole zdjęcia produktu w formularzu panelu (dziś przez API/seed) + upload plików.
- 🔧 **P8 Instalacja na telefonach**: ✅ **PWA gotowa** — ikony (192/512 + maskable + favicon) **przegenerowane z nowego logo Dowózka.pl**, manifest/`theme_color`/nazwa zaktualizowane, `sw.js` offline; „Dodaj do ekranu" działa na Android/iOS. ✅ Android przygotowany: `android:label="Dowózka.pl"`, ikony launchera (mipmap 48–192) z nowego logo; przewodnik build/podpis/dystrybucja w [DEPLOY.md](DEPLOY.md). ⬜ **realny build APK/AAB** — na maszynie z Android SDK (tu brak toolchainu); ⬜ iOS (po pilotażu).
- 🔧 **P9 Twarda ścieżka E2E** + poprawa tarcia: ✅ **test E2E** `CustomerJourneyE2ETests` — rejestracja → sklep **wg odległości** → koszyk → **checkout ze zgodą** → śledzenie → **potwierdzenie przez sklep** (suma 2×6+8=20 zł); ✅ podpowiedź hasła przy rejestracji; puste/błędne/offline stany już są (R2). ⬜ płatność **realną bramką Rapacza (sandbox)** w ścieżce — tuż przed wizytą; ⬜ pełny przegląd tarcia na urządzeniu.
- 🔧 **P10 Gotowość operacyjna**: ✅ **deploy publiczny (HTTPS)** — Fly.io (org `squark`, region `fra`): API **dowozka-api.fly.dev** (Neon Postgres, wolumen `App_Data`, seed Rapacz+Lewiatan) + PWA **dowozka.fly.dev** (instalowalna); tryb `Development` na czas pilotażu mock‑płatności; sekrety w `fly secrets` (nie w repo). Szczegóły [DEPLOY.md](DEPLOY.md), [pilot-live-deploy]. ✅ **panel admina wdrożony** (dowozka-admin.fly.dev), ⬜ konto Rapacza + instrukcja „1 strona", ⬜ monitoring/uptime, ⬜ pętla poprawek.
- ✅ Decyzje (właściciel, 2026-09-14): rejestracja = klient + **self-service sklepu**; multi‑lokalizacja = **lekka** (wspólny admin wielu sklepów); płatności = **mock teraz, sandbox przed wizytą**. Do potwierdzenia przy odpowiednich fazach: captcha (P6), geokoder (P4), APK/PWA (P8).

---

## 🔐 Role, rejestracja per‑kanał i modularny panel (web) [P0 — bieżący]
Cel (właściciel, 2026-09-15): **czytelny podział ról w wersji przeglądarkowej**, rejestracja **per‑kanał** i **modularny panel** (skalowalny na kolejne formularze). Backend ma **już 4 role** (`Identity/Domain/Role.cs`: `Admin`, `StoreEmployee`, `Driver`, `Customer` + `UserRole` ze scope `StoreId`, emitowane jako claim `role`/`store_id` w JWT). Brakuje **warstwy web**: dziś panel (`admin-panel/src/app/app.ts`) rozróżnia tylko `isAdmin()` vs reszta (brak `Driver`, brak `Customer`), nawigacja i `@switch` są **inline w jednym komponencie**, a **rejestracji sklepu/dostawcy w web nie ma** (tylko klient z apki + ręczne tworzenie przez admina).

**Mapowanie ról (nazwa właściciela → enum) i zakres w web:**
- **Administrator serwisu** = `Role.Admin` — cała platforma: sklepy, globalny zespół, konfiguracja/integracje, uwagi, rozliczenia globalne, seed.
- **Administrator sklepu** = `Role.StoreEmployee` (scope: `StoreId[]`) — swój sklep/sklepy: oferta, zamówienia, dostawy, integracje+dokumenty, zespół sklepu, rozliczenia sklepu, onboarding.
- **Dostawca** = `Role.Driver` (scope: `StoreId[]`) — widok dostaw: dostępne/moje, dostępność online/offline, trasa, zarobki (pełny interfejs kierowcy → **R8**; tu minimalny web).
- **Klient** = `Role.Customer` — kanał główny: **apka mobilna/PWA**. W web: co najwyżej konto + historia zamówień (do decyzji: czy web klienta w ogóle, czy tylko mobile).

- 🔧 **P‑Role1 — Uprawnienia i nawigacja per rola (web).** ✅ (2026-09-15) `Api`: `isStoreAdmin()`/`isDriver()`/`isCustomer()`/`roleLabel()`/`hasAnyRole()`; **nawigacja generowana z rejestru i filtrowana po roli** (Admin=wszystko, administrator sklepu=Start/Pulpit/Zamówienia/Dostawy/Oferta/Integracje/Rozliczenia, dostawca=Pulpit/Dostawy); userchip pokazuje właściwą rolę; **placeholder panelu dostawcy** (pełny UI = R8); `ensureVisibleTab()` pilnuje dostępu do zakładek. ⬜ twarde guardy tras w Angular Router (P‑Role3) + realny widok dostawcy (R8).
- 🔧 **P‑Role2 — Rejestracja per‑kanał.** ✅ (2026-09-15): backend `POST /api/register/store` (sklep + właściciel `StoreEmployee`, zwraca auth; **NIP wymagany** — walidacja sumy kontrolnej, `Store.Nip`, migracja `Catalog_StoreNip`), `POST /api/register/driver` (`Driver` **pending/nieaktywny**), `GET /api/admin/drivers/pending` + `POST /api/admin/drivers/{id}/approve` (Admin: przypisz sklep + aktywuj) — captcha-gated. Panel: ekran logowania przełącza **Logowanie / „Załóż sklep" / „Zostań dostawcą"** (rejestracja sklepu loguje i wchodzi w onboarding; dostawca → komunikat „oczekuje na weryfikację"). Testy `RegistrationTests` (3, live-smoke OK). ✅ **panel wdrożony** (dowozka-admin.fly.dev — rejestracja sklepu/dostawcy dostępna w web). ⬜ **UI zatwierdzania dostawców** w panelu (lista „oczekujący" + przypisanie sklepu) + widget captcha w panelu. Kanały:
  - **Apka mobilna = tylko klient** (`POST /identity/register` → `Role.Customer`; utrzymać, że apka nie tworzy innych ról).
  - **Web „Załóż sklep"** — self‑service: konto właściciela (`StoreEmployee` = administrator sklepu) + sklep (realizuje odłożone **P3b**).
  - **Web „Zostań dostawcą"** — self‑service `Driver` (status „do weryfikacji"; aktywacja/przypisanie do sklepu/obszaru przez admina).
  - **Administrator serwisu** — nie self‑service (seed/zaproszenie).
  - Backend: publiczne endpointy rejestracji **sklepu** i **dostawcy** (dziś tylko klient + `AdminCreateUser`), e‑mail weryfikacyjny, moderacja/aktywacja konta.
- 🔧 **P‑Role3 — Modularny panel (skalowalny na kolejne formularze).** ✅ (2026-09-15) **rejestr modułów** `admin-panel/src/app/modules.ts` (`PANEL_MODULES`: `{ id, label, icon, roles[], storeScoped }`) — nawigacja i pusty stan **generowane z rejestru**, filtrowane po roli; dodanie formularza = **jeden wpis** (+ ikona + komponent). Zachowane istniejące komponenty (orders/catalog/deliveries/finance/team/stores/integrations/feedback/settings/onboarding/dashboard). ✅ (2026-09-15) **Angular Router + lazy‑loaded routes** (trasy generowane z rejestru, `app.routes.ts`) **+ `canActivate` per rola** (`role.guard.ts` → redirect do pierwszej dostępnej sekcji) zamiast `@switch`; deep-linkowalne URL-e; `storeId`/szukaj przez query-paramy (`withComponentInputBinding`) zachowywane przy nawigacji; `routerLinkActive`. Zweryfikowane lokalnie (login→sekcje→guard) + wdrożone. ⬜ (opcjonalnie) osobne layouty per‑rola + dashboard z kart/widgetów.

Powiązania: rozszerza **P3b** (self‑service sklepu), zasila **R8** (interfejs kierowcy), korzysta z ról już w `Role`/JWT.

---

## Część I — ✅ Ukończone (MVP)
- **A Hardening fundament** — event bus (in‑proc/RabbitMQ + retry/DLQ), outbox/inbox, koperta błędów + correlation id, readiness, izolacja najemcy.
- **B Identity** — rejestracja/login, JWT + refresh, role, Google, reset hasła, weryfikacja e‑mail, zespół sklepu.
- **C Catalog + Ordering** — sklepy (status/prowizja/min‑order), oferta, koszyk, checkout z idempotencją, strefy + sloty.
- **D Payments** — płatność **webhook‑autorytatywna** (abstrakcja + mock), księga prowizji, rozliczenia, odczyt płatności klienta.
- **E Delivery + Notifications** — workflow kierowcy z izolacją; rozwiązywanie odbiorcy, skrzynka in‑app, abstrakcja push (FCM) + mock.
- **F1 Aplikacja Flutter** — pełny lejek (przeglądanie→koszyk→checkout→płatność→śledzenie) + reset hasła + skrzynka.
- **F2 Panel admina** — zamówienia (płatność+dostawa), sklepy, zespół, rozliczenia, dostawy, **zakres per‑rola**.
- **G Hardening produkcyjny** — config/secrets + guard, testy integracyjne (30/30), audit log, indeksy DB, README.

---

## Część II — 🔧 Plan rozwoju (R1–R11)

### R1 — Design system + fundament wizualny [P0]
- ✅ **Tryb ciemny** [defekt #5]: `ZzPalette` (neutrale light/dark) + `context.zz`, motyw `light()/dark()`, przełącznik **System/Jasny/Ciemny** w Koncie (utrwalany); ⬜ pełne tokeny cień/motion.
- 🔧 Logo: ✅ znak główny + warianty (poziom `ZzLogo` / mono / app‑icon `512²`).
- ✅ **Ikony systemowe** (**34**, batch 1–3: nawigacja/akcje/statusy/formularze) + `ZzIcon` (tint z `IconTheme`); **wpięte** w dolną nawigację, `StatusPill` (ikony statusów), koszyk (stepper −/+/kosz), sklepy, konto, login (pokaż/ukryj hasło). ⬜ ikony **kategorii** (grocery/piekarnia/apteka…) dopięte przy UI kategorii w R3.
- ⬜ Katalog komponentów (Widgetbook Flutter / strona demo Angular).
- 🔧 Pipeline SVG: ✅ `flutter_svg` + bespoke ilustracje (pusty koszyk/pudełko + **błąd/sukces** w `mobile/assets/svg`, podpięte w ErrorView/płatności); ⬜ konwersja `.ai/.eps → .svg` (z `/images`) + SVGO.
- ✅ **Dolna nawigacja** aplikacji (Sklepy/Koszyk/Zamówienia/Konto; Szukaj → R3) [defekt #2].
- ✅ **Fix pl‑PL w panelu** (przecinek dziesiętny, daty dd.MM) [defekt #3].

### R2 — Odświeżenie wizualne aplikacji klienta [P0]
- ✅ Ilustracje SVG: puste/błędne stany + sukces + **oś statusów** (ikony kroków, aktywny pulsuje).
- 🔧 Przebudowa ekranów na komponenty DS: ✅ **hero‑nagłówek sklepu**, ✅ **checkout** (etykiety z ikonami + karty terminów), ✅ **śledzenie** (oś statusów ze znacznikami czasu + **okno dostawy** „Termin: Dziś, 14:00–16:00", full‑stack); ⬜ produkt/koszyk (drobna kosmetyka).
- 🔧 Skeletony (✅ lista sklepów + oferta sklepu + Moje zamówienia) + mikro‑interakcje (✅ animowane wejście paska koszyka) + ✅ **obsługa offline** (globalny pasek „Brak połączenia", `connectivity_plus`); ⬜ dopracowane treści, więcej mikro‑animacji.
- ⬜ Fotografie: obróbka + WebP/AVIF + lazy‑load (sprawdzić licencje).
- ✅ Checkout: CTA nieaktywne dopóki brak adresu/telefonu/strefy/terminu + podpowiedź braków [defekt #4].

### R3 — Onboarding + odkrywanie [P0]
- ⬜ Onboarding (3 ilustracje SVG: wartość → zaufanie/śledzenie → lokalizacja).
- ⬜ **Kategorie + ikony** (górny scroller) — backend ma kategorie, brak UI.
- ⬜ **Wyszukiwarka + filtry** (cena/ocena/czas).
- ⬜ „W pobliżu" + lokalizacja; sekcje promocji.

### R4 — Restauracje + menu + plany rozliczeń + fale dostaw (Faza H cz. 1) [P0]
- ⬜ **Typ merchanta** (sklep/restauracja/pizzeria/inne).
- ⬜ **Menu** restauracji (warianty/dodatki) — zamiast „produktów".
- ⬜ **Dwa plany rozliczeń** (dostawa ZipZap 25 zł bez prowizji / dostawa merchanta z prowizją) — opłatę ustala merchant w obu.
- ⬜ **Fale dostaw ZipZap** — okna o stałych godzinach (np. 10:00/16:00) **ustawiane w panelu admina** → generują sloty dla Planu A (NIE on‑demand jak Glovo).
- ⬜ **Plan B: strategia merchanta** (o godzinie albo na bieżąco); merchant oznacza dostarczenie.
- ⬜ **Widoczność metody + harmonogramu dostawy u klienta** (karta sklepu + checkout) — P0 UX.
- ⬜ **Uogólniony ledger przychodu** (prowizja | opłata‑dostawy‑ZipZap).
- ⬜ Godziny otwarcia / harmonogram sklepu; zasięg (kody pocztowe).

### R5 — Realne płatności [P0]
- ⬜ Adapter **Przelewy24** (BLIK/karta) za `IPaymentProvider` + webhook + sandbox. (Alt: Fiserv/Polcard, Polskie ePłatności.) — warunek prawny podpięcia (dokumenty per‑sklep + zgoda) już ✅ w Części III.
- ⬜ Metody płatności w UI, obsługa błędów, **zwroty**.
- ⬜ **Zapisane adresy dostaw** (CRUD + domyślny).

### R6 — POS (GOPOS) + fiskalizacja [P0 dla gastronomii]
- ⬜ `IPosConnector` (GOPOS): import **menu**, push zamówień, statusy, **fiskalizacja**.
- ⬜ Akceptacja zamówienia przez merchanta (dźwięk/push), realny **FCM**.

### Import/Eksport danych (narzędzia sprzedawcy) [P1]
- ✅ **Import asortymentu z pliku** (CSV): ✅ **szablon** `templates/asortyment-import-szablon.csv` (`nazwa;kategoria;cena;jednostka;dostepny`); ✅ endpoint `POST /catalog/stores/{id}/products/import?commit=` (walidacja wierszy + **podgląd** `commit=false` + **upsert po nazwie**; kategorie dopasowane po nazwie, tworzone gdy brak; BOM/przecinek dziesiętny/duplikaty obsłużone) + UI w „Oferta" (wybór pliku → podgląd z raportem → zatwierdzenie, „Pobierz szablon"). Testy: `CatalogImportTests` (4). ⬜ XLSX później.
- ✅ **Eksport asortymentu** (CSV) — `GET /catalog/stores/{id}/products/export` (StoreEmployee/Admin) zwraca plik `text/csv` z **BOM UTF‑8**, format **identyczny z importem** (przecinek dziesiętny, `tak/nie`, sort kategoria→nazwa, cytowanie RFC 4180) → **round-trip** pobierz–edytuj–wgraj. UI: przycisk „Eksportuj CSV" w „Oferta". Testy: round-trip + 403. ⬜ XLSX później.
- ✅ **Eksport zamówień** (CSV) do księgowości — `GET /ordering/stores/{id}/orders/export?from=&to=&status=` (StoreEmployee/Admin, izolacja najemcy) zwraca `text/csv` z BOM; kolumny `numer;data;status;pozycje;produkty;prowizja;dostawa;suma;waluta` (statusy PL, przecinek dziesiętny, filtr zakresu dat po dacie złożenia). UI: „Eksportuj CSV" w zakładce Zamówienia (honoruje „Data od/do" z filtrów). Testy: totals/nagłówek/403. ⬜ XLSX/PDF później.
- ⬜ **Eksport rozliczeń/faktur** — CSV teraz, **faktura PDF** później (z zakładki Rozliczenia); eksport księgi prowizji.
- ⬜ Wspólne: limit rozmiaru pliku, **UTF‑8 (BOM)**, separator `;` (Excel PL), przecinek dziesiętny, nagłówki PL.

### R7 — Dostawa wg dystansu (Faza H cz. 2) [P1]
- ⬜ `IGeocoder` (mock + realny provider), współrzędne sklepu/klienta.
- ⬜ Dystans (Haversine), cennik `base + perKm`, promień zasięgu, podgląd opłaty po adresie.

### R8 — Aplikacja/PWA kierowcy + mapa śledzenia [P1]
- ⬜ Interfejs kierowcy (dostępne/moje, nawigacja, dostępność online/offline, zarobki).
- ⬜ **Mapa + marker kuriera**, live‑tracking + ETA u klienta.
- ⬜ **Oceny** (zamówienie/sklep/dostawa).

### R9 — Deploy & Launch [P0 przed pilotażem]
- ✅ **PWA instalowalna**: manifest ZipZap (#F97316, ikony 192/512 + maskable + SVG), własny service worker (`sw.js`, offline powłoki), `flutter build web` OK; API konfigurowalne `--dart-define=API_BASE_URL`.
- ✅ **Rozliczenia (model przychodu ZipZap→sklep)**: faktura miesięczna wg planu — **A: dostawa ZipZap = liczba dostaw × stała 25 zł**, **B: kurier sklepu = suma prowizji**; plan per‑sklep + opłata w ustawieniach platformy; endpoint `/invoice?month=` + karta w panelu (Rozliczenia). Klient płaci bramką sklepu — ZipZap nie jest płatnikiem. (rdzeń billingu Fazy H)
- 🔧 **Konfiguracja z kontami**: ✅ `.env.example` + panel **Konfiguracja** (status + checklist) + ✅ **edytowalne ustawienia platformy** + ✅ **per‑store integracja bramki płatniczej** (każdy sklep podpina swoje konto; tokeny **szyfrowane** Data Protection, write‑only, dostęp per‑sklep) + ✅ **routing per‑store w flow płatności** (resolver dostawcy sklepu → fallback mock, gotowe pod realny adapter) + ✅ **integracje platformy w panelu, nie w env**: Google OAuth **Client ID edytowalny w panelu** (`GET/PUT /api/admin/config/integrations`, walidacja `*.apps.googleusercontent.com`; port `IGoogleClientIdProvider` → magazyn panelu, env tylko fallback) + publiczny `GET /api/config/public` (Client ID dla `serverClientId` aplikacji). Env zostaje wyłącznie dla sekretów infrastruktury (JWT, DB). Testy: `PlatformIntegrationsTests` (3). ⬜ **adapter realnej bramki** (Przelewy24 — odszyfrowanie i użycie tokenów) + store ustawień w DB; ⬜ e‑mail/SMTP i inne integracje przenieść do panelu tym samym wzorcem.
- ⬜ Środowiska dev→staging→prod; sekrety z env (guard jest).
- ✅ Deploy **darmowo/Docker**: ✅ `web.Dockerfile` (nginx) + serwis compose `web` + `DEPLOY.md`; ✅ **realny hosting żywy (2026-09-15)** — Fly.io: backend `dowozka-api` (Docker) + web `dowozka` (nginx serwuje prebuilt `build/web`, `web.static.Dockerfile`), DB **Neon** Postgres, publiczne HTTPS. `Program.cs` `UseForwardedHeaders` (https za proxy Fly). ✅ **panel admina** (Angular) wdrożony na Fly (`dowozka-admin`, nginx serwuje prebuilt `dist/admin-panel/browser`; API base → Fly przez heurystykę hosta).
- 🔧 **CI/CD** (GitHub Actions): ✅ build + testy (`.github/workflows/ci.yml` — backend z Postgres service 30/30, mobile analyze+test 17/17); ⬜ krok deploy na main.
- ⬜ Kopie zapasowe DB + odtwarzanie; TLS (reverse proxy); uptime monitor na `/health/ready`.

### R10 — Android + iOS (build/podpis/sklepy) [P0 przed pilotażem mobilnym]
- 🔧 Ikona ✅ (app‑icon + ikony PWA), baseUrl per‑env ✅ (`API_BASE_URL`), **deep‑linki** ✅ (trasa docelowa zachowana przez splash) [defekt #9]; ⬜ splash natywny, wersjonowanie, ekran uprawnień.
- ⬜ **Android**: appbundle + keystore/Play App Signing; Play Console (listing, zrzuty, polityka) → Internal → Production; FCM.
- ⬜ **iOS**: konto Apple Dev, certyfikaty; `build ipa`; App Store Connect + **TestFlight** → review; APNs. (Wymaga macOS/CI np. Codemagic.)

### R11 — Obserwowalność + wsparcie [P1]
- 🔧 Backend: ✅ health `/health`·`/live`·`/ready` (DB), ✅ correlation‑id (nagłówek + zakres logów), ✅ logowanie żądań (metoda/ścieżka/status/ms/cid), ✅ globalny handler wyjątków (ProblemDetails); ⬜ JSON‑logi w prod + agregacja.
- ⬜ Sentry (Flutter/Angular/.NET), metryki/log dashboard, kanał zgłoszeń + FAQ.

---

## Część III — Zgodność prawna [P0 przed publiczną premierą]
- ✅ **Dokumenty prawne per‑sklep + zgoda przy zakupie** (warunek podpięcia bramki): każdy sklep zamieszcza URL‑e (regulamin / polityka prywatności / RODO) i włącza **wymóg akceptacji**. `GET/PUT /api/stores/{id}/legal` (GET publiczny; PUT StoreEmployee/Admin, walidacja URL http/https + przy wymogu obowiązkowe regulamin+polityka). **Twarda blokada checkoutu** (`IStoreLegalPolicyProvider` w Ordering + adapter hosta): brak akceptacji → 400; zgoda zapisywana w **audycie** (`order.consent.accepted`: URL‑e + czas). UI: panel „Integracje i dokumenty" (karta *Dokumenty i zgody*) + apka klienta (linki do dokumentów + wymagany checkbox przed „Złóż zamówienie"). Testy: `StoreLegalTests` (5) + `CheckoutConsentTests` (3).
- ⬜ **Dokumenty platformy ZipZap** (RODO / regulamin / polityka prywatności serwisu) + stopka i fallback; usunięcie/eksport danych, umowy powierzenia.
- ⬜ **Regulamin** (klient + merchant), prawa konsumenta (odstąpienie/reklamacje/zwroty).
- ⬜ **Cookies/consent** (web/panel).
- ⬜ Płatności: zgodność po stronie dostawcy (PCI/PSD2); **alergeny/składniki** (gastronomia); ceny brutto + jawny koszt dostawy.

## Część IV — QA i testy
- ✅ Jednostkowe + integracyjne API (55/55: 37 integracyjnych + 18 jednostkowych) + 23 testy aplikacji mobilnej.
- ⬜ **E2E scenariusze** → [TEST_SCENARIOS.md](TEST_SCENARIOS.md) (klient/merchant/kierowca/admin + brzegowe).
- ⬜ Live bug‑hunt (failed/empty/dark/offline), **device matrix**, E2E w CI.
- **Defekty** (fixy wpięte w R1/R2): patrz rejestr w TEST_SCENARIOS.md.

## Część V — Backlog / odłożone (do wpięcia w fazy gdy dojrzeją)
- Service fee, **subskrypcja „ZipZap+"**, reklama/promowanie merchantów.
- Quick‑commerce / dark stores (późny etap), agregacja flot (DeliGoo/Stava).
- Realne integracje kas/POS poza GOPOS; wielojęzyczność (EN).

---

## Dokumenty referencyjne
[ANALYSIS_TWO_PLANS.md](ANALYSIS_TWO_PLANS.md) · [BUSINESS_MODEL_AND_FEATURES.md](BUSINESS_MODEL_AND_FEATURES.md) ·
[TEST_SCENARIOS.md](TEST_SCENARIOS.md) · [PRODUCTION_SETUP.md](PRODUCTION_SETUP.md) ·
[PAYMENTS.md](PAYMENTS.md) · [FLUTTER_APP_PLAN.md](FLUTTER_APP_PLAN.md) ·
skill: `.claude/skills/zipzap-app-design`.
