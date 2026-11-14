# C. Plan zadań dla Claude Code

Ścieżki względem `mobile/` (aplikacja klienta), o ile nie podano inaczej. Każdy etap kończy raport
`docs/redesign/raporty/etap-N.md` dla Codexa; następny etap zaczyna się po statusie PASS i akceptacji właściciela.
Wspólne testy każdego zadania: `flutter analyze` bez ostrzeżeń, `flutter test` zielone, brak nowych kolorów i
odstępów wpisanych na sztywno poza motywem.

Każde zadanie ma pola: **Problem** · **Cel** · **Pliki** · **Jak** · **Akceptacja** · **Testy** · **Ryzyka** · **Zależy od**.

## Etap 0 — Audyt (te dokumenty)

**E0-1 Audyt i plan.**
- Problem: brak wspólnego obrazu stanu. Cel: dokumenty A–F.
- Pliki: `docs/redesign/*`. Jak: analiza kodu i pomiary na żywo.
- Akceptacja: właściciel zatwierdza kierunek i decyzje F. Testy: Codex sprawdza fakty z audytu (lista D).
- Ryzyka: liczby zdezaktualizują się po kolejnych zmianach. Zależy od: —.

## Etap 1 — Koncepcja wizualna (bez zmian w aplikacji)

**E1-1 Tablica kierunku.**
- Problem: kierunek opisany tylko słowami. Cel: jedna strona z paletą, typografią i kształtami (jasny i ciemny).
- Pliki: `docs/redesign/makiety/kierunek.html`. Jak: statyczny HTML/SVG.
- Akceptacja: właściciel wybiera wariant palety (F-2). Testy: kontrast każdej pary tekst/tło ≥ 4,5:1.
- Ryzyka: różnica między renderem HTML a Flutterem. Zależy od: E0-1.

**E1-2 Specyfikacja Design System.**
- Problem: tokeny istnieją tylko dla koloru i promienia. Cel: komplet tokenów i lista komponentów ze stanami.
- Pliki: `docs/redesign/design-system.md`; aktualizacja `.claude/skills/zipzap-app-design` (usunąć pomarańcz).
- Jak: tabela tokenów z nazwami używanymi w kodzie.
- Akceptacja: każdy komponent z koncepcji ma warianty, stany i wymiary. Testy: przegląd Codexa pod kątem sprzeczności z marką.
- Ryzyka: —. Zależy od: E1-1.

**E1-3 Makiety 8 ekranów** (Start, Sklep, Produkt, Koszyk, Checkout w 3 krokach, Zamówienie, Logowanie, Konto) dla 375 px; w 1280 px Start, Sklep (z koszykiem w panelu) i Zamówienie. **Zmiana kryterium zaakceptowana przez właściciela 2026-11-14:** Produkt, Logowanie, Konto i Śledzenie zamówienia mają na komputerze układ jednokolumnowy wyśrodkowany (maks. 640 px; Produkt jako okno dialogowe) — opisany regułą w `design-system.md` §7 zamiast osobnych makiet 1280 px.
- Pliki: `docs/redesign/makiety/*` (Figma po decyzji F-3).
- Akceptacja: każdy ekran ma stany ładowanie/pusty/błąd/brak internetu (macierz `StanyEkranow.dc.html`; „nie dotyczy" tylko z uzasadnieniem) i oznaczone dane, których dziś nie ma w API.
- Testy: lista danych z makiet porównana z `lib/models`. Ryzyka: makiety obiecują dane wymagające backendu. Zależy od: E1-2.

**E1-4 Ilustracje i ikony.**
- Cel: maskotka, 3 plansze onboardingu, 6 stanów, 12 ikon kategorii, ikony aplikacji.
- Pliki: `assets/svg/**`, `web/app-icons/*`. Jak: ręczne SVG, optymalizacja SVGO.
- Akceptacja: każdy plik ≤ 12 KB, czytelny w trybie ciemnym, spójna paleta; akceptacja właściciela przed użyciem.
- Testy: test renderu wszystkich zasobów (`flutter_svg` nie obsługuje części filtrów). Zależy od: E1-1.

**E1-5 Specyfikacja ruchu.**
- Cel: lista animacji z czasem, krzywą i zachowaniem przy systemowym „ogranicz ruch".
- Akceptacja: żadna animacja nie blokuje obsługi ani nie przekracza 250 ms (start ≤ 1,2 s). Zależy od: E1-2.

## Etap 2 — Fundamenty frontendowe

**E2-1 Tokeny v2.**
- Problem: nazwy `orange*` trzymają turkus; brak odstępów, cieni, ruchu.
- Cel: `ZzColors` z rolami (brand, brandStrong, accent…), `ZzSpace`, `ZzShadow`, `ZzMotion`, nowe `ZzRadius`; stare nazwy jako aliasy `@Deprecated` do końca Etapu 3.
- Pliki: `lib/core/theme/zz_theme.dart` i wszystkie użycia.
- Akceptacja: 0 wystąpień `Color(0x` poza motywem; kontrast przycisku głównego ≥ 4,5:1.
- Testy: test jednostkowy kontrastów tokenów; istniejące 55 testów. Ryzyka: szeroka zmiana mechaniczna. Zależy od: E1-2.

**E2-2 Czcionki w paczce.**
- Problem: 7 pobrań z Google przy starcie. Cel: Poppins i Inter jako zasoby aplikacji.
- Pliki: `pubspec.yaml`, `assets/fonts/*`, `zz_theme.dart` (usunięcie `google_fonts`).
- Akceptacja: 0 żądań do `fonts.gstatic.com` i `googleapis.com`. Testy: pomiar sieci w przeglądarce; testy widgetów.
- Ryzyka: ok. +300 KB w paczce (podzbiór latin-ext). Zależy od: —.

**E2-3 Biblioteka komponentów.**
- Cel: `lib/core/ui/` — przycisk, pole, chip kategorii, karty, licznik, arkusz, komunikat, pasek wyszukiwania; galeria pod `/dev/ui` tylko w trybie debug.
- Akceptacja: ekrany nie definiują własnych przycisków ani kart.
- Testy: test widgetu na komponent (stany, 48 px, etykieta dostępności).
- Ryzyka: dublowanie `core/widgets` — przenosimy, nie kopiujemy. Zależy od: E2-1.

**E2-4 Układ responsywny.**
- Problem: brak punktów łamania. Cel: `ZzBreakpoints` (600/900/1200), `ZzPage` (maks. 1100 px), nawigacja dolna poniżej 900 px, boczna od 900 px.
- Pliki: `core/router/main_scaffold.dart`, `core/ui/layout/*`.
- Akceptacja: 360, 768, 1280 i 1920 px bez rozciągniętych list i poziomego przewijania.
- Testy: testy widgetów w 4 rozmiarach, zrzuty. Zależy od: E2-3.

**E2-5 Ruch i dostępność bazowa.**
- Cel: `ZzMotion` respektuje `MediaQuery.disableAnimations`; włączone drzewo semantyki na web; etykiety w komponentach bazowych; tekst skalowany do 130 % bez ucięć.
- Testy: testy z `disableAnimations: true` i `textScaler 1.3`.
- Ryzyka: semantyka na web obniża wydajność — pomiar przed/po. Zależy od: E2-3.

**E2-6 Nawigacja 5 zakładek.**
- Cel: Start · Szukaj · Koszyk · Zamówienia · Konto; `/stores` zostaje jako alias (stare linki i kody QR działają).
- Pliki: `core/router/app_router.dart`, `core/router/main_scaffold.dart`.
- Testy: `qr_entry_flow_test` i nowe testy przekierowań. Ryzyka: regresja linków z kodów QR. Zależy od: E2-4.

## Etap 3 — Redesign ekranów (kolejność = priorytet)

Każde zadanie: 4 stany (ładowanie/pusty/błąd/offline) i zrzuty 375 oraz 1280 px przed/po w raporcie.

| ID | Ekran | Zakres | Pliki | Kryterium szczególne | Ryzyko |
|---|---|---|---|---|---|
| E3-1 | Start | nagłówek z adresem (zastępuje dwa banery; logika kodu i lokalizacji bez zmian), szukajka, baner, kategorie, zamów ponownie, karty sklepów | `features/stores/*` → `features/home/*` | wybór kodu pocztowego w ≤ 2 dotknięciach; sklepy widoczne bez przewijania na 640 px | zależne od F-5/F-6 — bez nich wariant z logo |
| E3-2 | Sklep i produkty | zakładki kategorii (endpoint istnieje), siatka i wiersze, szukanie po nazwie w sklepie, animacja „+" | `features/catalog/*` | `store_header_test` + testy filtrowania | średnie |
| E3-3 | Koszyk | pusty stan z ilustracją i przyciskiem, próg minimalnego zamówienia jako pasek postępu | `features/cart/*` | `cart_controller` bez zmian, istniejący test przechodzi | niskie |
| E3-4 | Checkout w 3 krokach | te same wywołania API i walidacje, podział Adres → Termin → Podsumowanie | `features/checkout/*` | identyczne żądania sieciowe przed/po; testy okien, rund, trybu testowego | **wysokie** — osobny przegląd Codexa |
| E3-5 | Zamówienia i śledzenie | oś statusu, karta terminu dostawy, animacja zmiany statusu | `features/orders/*` | `order_timeline_test` | niskie |
| E3-6 | Logowanie, rejestracja, reset | układ z ilustracją; pola i walidacje bez zmian | `features/account/*` | `forgot_password_test` | niskie |
| E3-7 | Konto, adresy, powiadomienia, opinie | porządek sekcji, karty | `features/account/*`, `notifications`, `feedback` | — | niskie |
| E3-8 | Start aplikacji i onboarding | animowany ekran startowy; 3 plansze przy pierwszym uruchomieniu, pomijalne | `features/splash/*`, nowe `features/onboarding/*` | wejście z kodu QR pomija onboarding | średnie |
| E3-9 | Komunikaty i mikrointerakcje | jednolite komunikaty, plakietka koszyka, przejścia | `core/ui/*` | wyłączane przy „ogranicz ruch" | niskie |
| E3-10 | Wyszukiwarka globalna | wymaga endpointu (F-7); wcześniej szukanie sklepów po nazwie | nowe `features/search/*`, backend Catalog | testy integracyjne endpointu | zmiana backendu |

## Etap 4 — PWA

E4-1 ikony i manifest · E4-2 instalacja (Android/desktop + instrukcja iOS) · E4-3 aktualizacje i ekran offline ·
E4-4 pamięć podręczna zdjęć · E4-5 waga startu (WebAssembly, zależności, pomiar) · E4-6 ekrany startowe iOS.
Szczegóły i kryteria: [04-plan-pwa.md](04-plan-pwa.md).

## Etap 5 — Testy i optymalizacja

E5-1 przejście `TEST_SCENARIOS.md` na nowym UI · E5-2 zrzuty wzorcowe (golden) kluczowych ekranów ·
E5-3 Lighthouse i pomiar startu przed/po · E5-4 przegląd dostępności (kontrast, fokus, czytnik ekranu) ·
E5-5 przegląd bezpieczeństwa (service worker, pamięć lokalna, linki zewnętrzne).

## Etap 6 — Wdrożenie

E6-1 dokumentacja (`DEPLOY.md`, design system) · E6-2 budowa i wdrożenie na `app.dowózka.pl` z kopią poprzedniej
wersji · E6-3 lista pozostałych problemów · (Budowa APK i publikacja w sklepach — poza zakresem: decyzja właściciela 2026-11-14, tylko aplikacja webowa instalowana z przeglądarki.)

## Poza tym planem (osobna decyzja F-10)

Redesign panelu Angular (dziś 0 testów, brak wersji na telefon) — po Etapie 3, na tych samych tokenach.
