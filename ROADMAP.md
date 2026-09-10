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
- 🔧 Ilustracje SVG: ✅ puste/błędne stany + sukces; ⬜ oś statusów.
- ⬜ Przebudowa ekranów (sklepy, produkt, koszyk, checkout, śledzenie) na komponenty DS.
- 🔧 Skeletony (✅ lista sklepów + **oferta sklepu** + **Moje zamówienia**) + mikro‑interakcje (✅ animowane wejście paska koszyka); ⬜ obsługa offline, dopracowane treści, więcej mikro‑animacji.
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
- ⬜ Adapter **Przelewy24** (BLIK/karta) za `IPaymentProvider` + webhook + sandbox. (Alt: Fiserv/Polcard, Polskie ePłatności.)
- ⬜ Metody płatności w UI, obsługa błędów, **zwroty**.
- ⬜ **Zapisane adresy dostaw** (CRUD + domyślny).

### R6 — POS (GOPOS) + fiskalizacja [P0 dla gastronomii]
- ⬜ `IPosConnector` (GOPOS): import **menu**, push zamówień, statusy, **fiskalizacja**.
- ⬜ Akceptacja zamówienia przez merchanta (dźwięk/push), realny **FCM**.

### R7 — Dostawa wg dystansu (Faza H cz. 2) [P1]
- ⬜ `IGeocoder` (mock + realny provider), współrzędne sklepu/klienta.
- ⬜ Dystans (Haversine), cennik `base + perKm`, promień zasięgu, podgląd opłaty po adresie.

### R8 — Aplikacja/PWA kierowcy + mapa śledzenia [P1]
- ⬜ Interfejs kierowcy (dostępne/moje, nawigacja, dostępność online/offline, zarobki).
- ⬜ **Mapa + marker kuriera**, live‑tracking + ETA u klienta.
- ⬜ **Oceny** (zamówienie/sklep/dostawa).

### R9 — Deploy & Launch [P0 przed pilotażem]
- ⬜ Środowiska dev→staging→prod; sekrety z env (guard jest).
- ⬜ Deploy **darmowo/Docker**: backend (Fly.io/Render/Railway lub VPS+compose), DB (Neon/Supabase/Railway), web (Cloudflare Pages/Netlify/Vercel).
- ⬜ **CI/CD** (GitHub Actions): build + testy (Postgres jako service) + deploy na main.
- ⬜ Kopie zapasowe DB + odtwarzanie; TLS (reverse proxy); uptime monitor na `/health/ready`.

### R10 — Android + iOS (build/podpis/sklepy) [P0 przed pilotażem mobilnym]
- ⬜ Ikona + splash (z logo/SVG), wersjonowanie, ekran uprawnień, deep‑linki, baseUrl per‑env.
- ⬜ **Android**: appbundle + keystore/Play App Signing; Play Console (listing, zrzuty, polityka) → Internal → Production; FCM.
- ⬜ **iOS**: konto Apple Dev, certyfikaty; `build ipa`; App Store Connect + **TestFlight** → review; APNs. (Wymaga macOS/CI np. Codemagic.)

### R11 — Obserwowalność + wsparcie [P1]
- ⬜ Sentry (Flutter/Angular/.NET), metryki/log dashboard, kanał zgłoszeń + FAQ.

---

## Część III — Zgodność prawna [P0 przed publiczną premierą]
- ⬜ **RODO**: polityka prywatności, podstawy/zgody, usunięcie/eksport danych, umowy powierzenia.
- ⬜ **Regulamin** (klient + merchant), prawa konsumenta (odstąpienie/reklamacje/zwroty).
- ⬜ **Cookies/consent** (web/panel).
- ⬜ Płatności: zgodność po stronie dostawcy (PCI/PSD2); **alergeny/składniki** (gastronomia); ceny brutto + jawny koszt dostawy.

## Część IV — QA i testy
- ✅ Jednostkowe + integracyjne API (30/30).
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
