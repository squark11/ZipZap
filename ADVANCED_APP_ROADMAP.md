# ZipZap — Roadmap zaawansowanej aplikacji (Dev · UX · UI · Grafika)

Roadmap wyjścia z „działającego MVP" do **dopracowanego produktu**: nie tylko kod,
ale też doświadczenie (UX), warstwa wizualna (UI) i estetyczna szata graficzna
(ilustracje/ikony SVG). Oparty na obecnym stanie repo, dostarczonej dokumentacji
integracji (`dokumentacje.md`) i zasobach z `/images`.

## 0. Punkt startu (co już jest)
- Backend modularny (8 modułów), płatność webhook-autorytatywna (mock), workflow
  kierowcy, powiadomienia, audyt, testy (30), config/secrets + guard, docs.
- Aplikacja **Flutter** (klient, pełny lejek), panel **Angular** (7 zakładek, zakres per-rola).
- Marka: pomarańcz `#F97316`, zieleń `#22C55E`, grafit `#3A3F4B`; Poppins/Inter (`/branding`).
- **Luki na zaawansowany etap:** brak dopracowanej warstwy wizualnej/ilustracji,
  realnych integracji płatności/POS, dwóch planów rozliczeń, restauracji/menu.

## 1. Wizja (zaawansowany etap)
Marketplace jedzenia i zakupów lokalnych z **dwoma planami** (dostawa ZipZap /
dostawa merchanta), obsługą **sklepów i restauracji**, realnymi płatnościami i
integracją POS — z dopracowanym, spójnym UI i własną biblioteką ilustracji.

## 2. Cztery przeplatające się filary
Każda faza (R1…R8) ma deliverables w czterech warstwach — nie robimy „najpierw kod,
potem grafika", tylko równolegle, z checkpointami:
- **DEV** — backend/API, aplikacja, panel.
- **UX** — przepływy, stany, dostępność, treści.
- **UI** — design system, komponenty, motyw.
- **GFX** — ilustracje/ikony SVG, zdjęcia, motion.

---

## 3. UX — architektura informacji i przepływy

### 3.1 Persony
- **Klient** — przegląda, zamawia, śledzi. Cel: szybko i bez tarcia.
- **Kierowca** — pula dostaw, nawigacja, statusy. (Dziś w panelu; docelowo osobna apka/PWA.)
- **Merchant** (sklep/restauracja) — oferta/menu, zamówienia, dostawy, rozliczenia.
- **Admin** — sklepy, zespół, audyt, konfiguracja.

### 3.2 Główne podróże (do zaprojektowania end-to-end)
1. **Onboarding klienta** — 3 ekrany (wartość + zaufanie + lokalizacja), potem katalog
   bez logowania. (Ilustracje: „food-delivery-onboarding-screens".)
2. **Odkrywanie** — kategorie, wyszukiwarka, filtry, sklepy/restauracje w pobliżu, promocje.
3. **Zamówienie** — koszyk → adres/termin/strefa → **płatność (polling, webhook)** → potwierdzenie.
4. **Śledzenie** — oś statusów + (docelowo) mapa kuriera; powiadomienia push.
5. **Konto** — zamówienia, adresy, powiadomienia, płatności, pomoc.
6. **Merchant** — pulpit, zamówienia, menu/oferta, godziny, strefa/zasięg, rozliczenia.
7. **Kierowca** — dostępne → przyjmij → nawigacja → odbierz → dostarcz.

### 3.3 Stany i mikro-interakcje (dla każdego ekranu)
Loading (skeletony) · Empty (ilustracja + CTA) · Error (ilustracja + retry) ·
Offline (baner + kolejka) · Success (potwierdzenie + kolejny krok). Mikro-animacje:
dodanie do koszyka, zmiana statusu, „pull-to-refresh", przejścia między ekranami.

### 3.4 Dostępność i i18n
- WCAG AA: kontrast ≥ 4.5:1, dotyk ≥ 48px, focus/semantyka, obsługa czytników.
- SVG z `<title>`/`<desc>`; ikony dekoracyjne `aria-hidden`.
- i18n PL (domyślny) + EN; format walut/dat `pl_PL` (już jest w aplikacji).

---

## 4. UI — design system

### 4.1 Tokeny (rozszerzenie `/branding`)
Kolory (marka + semantyczne + neutralne + tła) · **skala typografii** (Poppins nagłówki,
Inter UI) · **spacing** (4-punktowa siatka) · **radius** 6/10/16 · **cienie** · **motion**
(czasy/łatwości) · **z-index**. Wersje **jasna i ciemna** (aplikacja Flutter jest już
theme-aware — rozszerzyć o dark).

### 4.2 Biblioteka komponentów (spójna Flutter ↔ Angular)
Przyciski (primary/secondary/ghost/danger), pola, selecty, chipy/filtry, karty
(sklep/produkt/zamówienie), listy, badge statusu, stepper ilości, koszyk, dolny pasek
akcji, nawigacja (bottom-nav aplikacja / rail panel), toolbar filtrów, modale, toasty,
skeletony, puste/błędne stany, oś statusów, tabela (panel). **Katalog komponentów**
jako żywa dokumentacja (Storybook-like: Widgetbook dla Flutter, strona demo dla Angular).

### 4.3 Zdjęcia i ilustracje — zasady
- **Fotografie** (dostarczone: jedzenie/zakupy) — hero, kategorie, tła sekcji; jednolita
  obróbka (kadr, ciepły ton, overlay grafitowy dla czytelności tekstu). **Sprawdzić licencje.**
- **Ilustracje** — spójny styl (izometryczny/flat) z palety marki; do onboardingu, pustych
  stanów, sukcesu, błędów, sekcji marketingowych.
- **Optymalizacja** zdjęć: WebP/AVIF + rozmiary responsywne; lazy-load; placeholdery (blur).

---

## 5. Grafika / SVG — plan produkcji

### 5.1 Ważne: format dostarczonych wektorów
Paczki w `/images` to **`.ai` / `.eps`** (Illustrator/EPS), **nie SVG** — nie da się ich
wprost użyć w aplikacji. Dwie ścieżki (użyjemy obu):
- **Konwersja** `.ai/.eps → .svg` (Inkscape CLI / Illustrator „Save as SVG" / usługi),
  potem czyszczenie + tokenizacja kolorów marki.
- **Bespoke SVG** — własne ilustracje/ikony spójne z marką (pełna kontrola, lekkie,
  `currentColor`, theme-aware).

### 5.2 Inwentarz zasobów SVG (priorytety)
| Grupa | Elementy | Priorytet |
|---|---|---|
| **Logo** | wariant poziomy/pionowy/monochrom/favicon/app-icon | P0 |
| **Ikony systemowe** | nawigacja, akcje, statusy (spójny zestaw ~40) | P0 |
| **Ikony kategorii** | pieczywo, nabiał, owoce/warzywa, napoje, dania, pizza… (~16) | P1 |
| **Puste stany** | pusty koszyk, brak zamówień, brak wyników, brak sklepów | P0 |
| **Błędy/offline** | 404, błąd sieci, offline | P1 |
| **Onboarding** | 3 ilustracje (wartość, zaufanie, lokalizacja) | P1 |
| **Sukces** | zamówienie złożone / dostarczone | P1 |
| **Oś statusów** | ikony kroków (złożone→…→dostarczone) | P1 |
| **Marker mapy / kurier** | pin, kurier, sklep | P2 |
| **Marketing/hero** | ilustracje sekcji, bannery | P2 |

### 5.3 Standard SVG (obowiązkowy)
- `viewBox` (skalowalność), **bez** osadzonej rastry, minimalne ścieżki.
- Kolory przez `currentColor`/zmienne marki tam, gdzie to możliwe (theme-aware).
- Dostępność: `role="img"` + `<title>`; dekoracyjne `aria-hidden`.
- Optymalizacja **SVGO** (usuwanie metadanych, precyzja, scalanie ścieżek).

### 5.4 Pipeline i integracja
- Repo `/branding/assets/svg/**` (źródło) + eksport do `mobile/assets/svg` i `admin-panel/src/assets/svg`.
- **Flutter**: `flutter_svg` (`SvgPicture.asset`), koloryzacja przez `colorFilter`.
- **Angular**: inline SVG (obecny wzorzec ikon w szynie) lub `<img>` dla ilustracji.
- Skrypt build: SVGO + kopiowanie do targetów (spójne źródło prawdy).

---

## 6. Integracje realne (z `dokumentacje.md`)
Wszystkie za istniejącą abstrakcją, webhook-autorytatywne, sekrety z env (nie commitować).
- **Płatności** — adaptery `IPaymentProvider`:
  - **Przelewy24** (developers.przelewy24.pl) — BLIK/karty/przelewy, weryfikacja transakcji.
  - **Fiserv / Polcard** (docs.apis-fiserv.com) — bramka kartowa.
  - **Polskie ePłatności** (devzone.pep.pl) — online.
  Wybór dostawcy per środowisko/merchant; wspólny model sesji + webhook.
- **POS / gastronomia** — **GOPOS** (app.gopos.io) za portem `IPosConnector`:
  synchronizacja **menu**, przekazywanie zamówień do POS, statusy, **fiskalizacja**.
  Kluczowe dla restauracji (menu zamiast „produktów").

## 7. Faza H — dwa plany + restauracje
Wg [ANALYSIS_TWO_PLANS.md](ANALYSIS_TWO_PLANS.md): typ merchanta (sklep/restauracja/…),
plan dostawa-ZipZap (stałe 25 zł, bez prowizji) vs dostawa-merchanta (prowizja), zasięg
i opłata per merchant, dystans (geokoder). Wpięcie w powyższe R-fazy.

---

## 8. Roadmapa fazowa (Dev · UX · UI · GFX)

### R1 — Fundament wizualny (design system + logo/ikony) ⟶ *najpierw*
- **UI/GFX**: tokeny (light+dark), logo (warianty SVG), zestaw ikon systemowych, katalog
  komponentów. Konwersja/optymalizacja dostarczonych wektorów do SVG.
- **UX**: audyt obecnych ekranów, mapa przepływów, spis stanów.
- **DEV**: `flutter_svg` + pipeline SVG; dark theme w aplikacji.

### R2 — Odświeżenie wizualne aplikacji klienta
- **GFX**: puste/błędne stany, sukcesy, oś statusów (SVG). Fotografie: obróbka + WebP.
- **UI**: przebudowa ekranów (sklepy, produkt, koszyk, checkout, śledzenie) na komponenty DS.
- **UX**: skeletony, mikro-interakcje, offline, dopracowane treści.

### R3 — Onboarding + odkrywanie
- **GFX**: 3 ilustracje onboardingu, ikony kategorii.
- **UX/UI**: onboarding, wyszukiwarka, filtry, sekcje „w pobliżu"/promocje, lokalizacja.
- **DEV**: endpointy wyszukiwania/filtrów; kategorie w aplikacji.

### R4 — Restauracje + menu (Faza H, część 1)
- **DEV**: typ merchanta, model menu (warianty/dodatki), plan rozliczeń (bez geolokalizacji),
  zasięg (kody), przepływ Planu B (merchant oznacza dostarczenie).
- **UI/UX/GFX**: karta restauracji, ekran menu, konfigurator pozycji.

### R5 — Realne płatności
- **DEV**: adapter Przelewy24 (i/lub Fiserv/PeP) za `IPaymentProvider`, webhook, sandbox.
- **UX/UI**: ekran płatności z metodami (BLIK/karta), obsługa błędów/zwrotów.

### R6 — POS (GOPOS) + fiskalizacja
- **DEV**: `IPosConnector` (GOPOS): synchronizacja menu, push zamówień, fiskalizacja.
- **UX**: statusy z POS w panelu merchanta.

### R7 — Dostawa zależna od dystansu (Faza H, część 2)
- **DEV**: `IGeocoder` (mock + realny provider), współrzędne, dystans, cennik base+perKm, promień.
- **UI**: podgląd opłaty po adresie; mapa strefy.

### R8 — Aplikacja/PWA kierowcy + mapa śledzenia
- **DEV/UX/UI/GFX**: dedykowany interfejs kierowcy, mapa i marker kuriera, live-tracking klienta.

## 9. Kolejność i kamienie milowe
R1 (design system) jest bramą dla R2–R3 (wizualny refresh). R4–R7 to rozwój biznesowy
(restauracje, płatności, POS, dystans). R8 domyka logistykę. Każda R = build zielony +
katalog komponentów zaktualizowany + zasoby SVG dodane + osobny commit.

## 10. Ryzyka i zależności
- **Format assetów**: `.ai/.eps` wymagają konwersji do SVG (narzędzie/licencja).
- **Licencje** dostarczonych zdjęć/wektorów — zweryfikować przed publikacją.
- **Integracje**: creds sandbox (Przelewy24/Fiserv/PeP/GOPOS) — bez nich tylko adapter+mock.
- **Spójność DS** między Flutter i Angular — jedno źródło tokenów/ikon.
- **Geokoder** (dystans) — koszt/limity zewnętrznego API.

## 11. Definicja ukończenia (per warstwa)
- **DEV**: build+testy zielone, migracje addytywne, sekrety z env, weryfikacja na żywo.
- **UX**: przepływ pokryty (happy + błędy/empty/offline), a11y sprawdzone.
- **UI**: użyte komponenty DS, light+dark, brak „magicznych" wartości (tylko tokeny).
- **GFX**: SVG zoptymalizowane (SVGO), theme-aware, z a11y title; w katalogu zasobów.

### R9 — Wdrożenie i wydanie (Deploy & Launch)
- **Środowiska**: dev (Docker lokalnie) → **staging** → prod. Sekrety z env (jest guard).
- **Deploy (tymczasowo darmowy / Docker):**
  - Backend (.NET + Postgres + RabbitMQ) — **Fly.io / Render / Railway** (free/low‑tier) albo
    tani **VPS + docker compose**. Postgres zarządzany: **Neon / Supabase / Railway** (free tier).
  - **Panel Angular** i **Flutter web** — **Cloudflare Pages / Netlify / Vercel / GitHub Pages** (free).
  - Domena + **TLS** (reverse proxy: Caddy/Traefik/nginx lub PaaS wbudowany).
- **CI/CD** (GitHub Actions): build + `dotnet test` (z Postgres jako service) + `flutter analyze/test`
  + `ng build`; artefakty; deploy na merge do main.
- **Kopie zapasowe** DB (automatyczne) + procedura odtworzenia; migracje przy starcie.
- **Zdrowie**: `/health/ready` już jest — podłączyć pod uptime monitor.

### R10 — Mobile: Android + iOS (build, podpisywanie, publikacja)
- **Wspólne**: ikona aplikacji + splash (z SVG/logo), wersjonowanie (`pubspec` version+build),
  ekran uprawnień (lokalizacja, powiadomienia) z uzasadnieniem, deep‑linki (powrót z płatności),
  konfiguracja `baseUrl` per środowisko.
- **Android**: `flutter build appbundle`, **keystore** + podpisywanie (Play App Signing),
  Play Console: listing (opis, zrzuty, ikona 512, feature graphic, polityka prywatności),
  **Internal testing** → Closed → Production. Docelowo FCM (push).
- **iOS**: konto Apple Developer (99$/rok), certyfikaty/provisioning, `flutter build ipa`,
  App Store Connect: listing + zrzuty per rozmiar, **TestFlight** → review → Production.
  APNs (push). Uwaga: build iOS wymaga macOS/Xcode (lub CI typu Codemagic/Mac‑in‑cloud).
- **Store readiness**: teksty marketingowe, zrzuty (z prawdziwego UI po R2), kategoria,
  wiek, dane kontaktowe, **link do polityki prywatności i regulaminu**.

### R11 — Obserwowalność i wsparcie
- **Błędy**: Sentry (Flutter + Angular + .NET) — crash/exception tracking.
- **Metryki/log**: strukturalne logi + correlation id (jest); dashboard (Grafana/PaaS logs).
- **Wsparcie**: kanał zgłoszeń (e‑mail/chat), FAQ, statusy incydentów.

## 12. Zgodność prawna (pełnoprawna aplikacja, rynek PL)
- **RODO/GDPR**: polityka prywatności, podstawy przetwarzania, zgody (marketing/push),
  prawo do usunięcia/eksportu danych, rejestr czynności, umowy powierzenia z dostawcami.
- **Regulamin** usługi (klient + merchant), prawa konsumenta (odstąpienie/reklamacje/zwroty).
- **Cookies/consent** (panel/web) — baner zgód, tylko niezbędne domyślnie.
- **Płatności**: zgodność po stronie dostawcy (PCI‑DSS, PSD2/SCA) — nie przechowujemy danych kart;
  webhook‑autorytatywny (jest). Faktury/fiskalizacja (GOPOS) po stronie merchanta.
- **Treści**: informacje o alergenach/składnikach (gastronomia), ceny brutto, koszt dostawy jawnie.

## 13. QA i testy (pełna piramida)
- **Jednostkowe** (domena) — są. **Integracyjne API** (authz/izolacja/webhook) — są.
- **E2E** — scenariusze prawdziwego użytkownika: patrz **[TEST_SCENARIOS.md](TEST_SCENARIOS.md)**
  (klient, merchant, kierowca, admin + przypadki brzegowe i błędy).
- **UI/UX bug‑hunt** — przegląd na żywo (Flutter web + panel) pod kątem stanów, walidacji,
  wyścigów, dostępności; log defektów w TEST_SCENARIOS.md.
- **Device matrix** — Android (mały/duży), iOS, web (desktop/mobile), light/dark, wolna sieć.
- **Automatyzacja** — E2E w CI (Flutter integration_test / Playwright dla panelu) — docelowo.


1. **Start R1** — zbuduję design system + wyprodukuję pierwszy zestaw **SVG** (logo warianty,
   ikony systemowe, puste stany) i wpięcie `flutter_svg`.
2. Najpierw **konwersja** dostarczonych `.ai/.eps → .svg` (jeśli dasz zielone światło na
   narzędzie/licencje) i przegląd, co reużyć vs. narysować od nowa.
3. Wersja **wizualna** tego roadmapu jako interaktywny artefakt (strona), jeśli wolisz.
