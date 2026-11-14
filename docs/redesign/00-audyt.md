# A. Audyt projektu Dowózka.pl (Etap 0)

Stan na 2026-11-14, kod: `main` @ `c294a6c` (worktree `pilot-outbox-deadletter`). Sprawdzone w kodzie i na żywo
(`https://app.dowózka.pl`, widok 375×640, pomiar transferu `curl` z gzip).

## 1. Architektura (stan faktyczny)

| Warstwa | Technologia | Rozmiar | Testy |
|---|---|---|---|
| Aplikacja klienta | **Flutter 3 (Dart)**, Riverpod, go_router, dio — jeden kod na web/Android/iOS; web wdrożony na lh.pl | 70 plików, 6 830 linii | 14 plików, 55 testów |
| Panel (admin/sklep/kierowca) | Angular 20, komponenty standalone, własny CSS | 6 084 linii TS, `styles.css` 236 linii | **0** plików `.spec.ts` |
| Backend | .NET 8, modularny monolit (9 modułów), EF Core, Postgres (Neon), outbox | 136 endpointów | 220 integracyjnych + 64 jednostkowe, CI zielone |
| Strona główna | statyczny HTML (`web-landing/`) | — | — |

Hosting: API na Render (plan darmowy), baza Neon, pliki statyczne na lh.pl przez FTPS.

**Wniosek dla briefu:** aplikacja klienta nie jest w TypeScripcie ani HTML/CSS — to Flutter renderowany na `<canvas>`
(CanvasKit). „CSS animations" i komponenty HTML nie mają tu zastosowania; animacje robi się w Flutterze. To dobra
baza pod wymaganie „najpierw PWA, później Google Play i App Store" — ten sam kod buduje się na Androida i iOS.

## 2. Co jest dobrze i zostaje

- Podział na funkcje (`features/*`), repozytoria, kontrolery Riverpod — czytelny, mały, bez długu.
- Tokeny w jednym miejscu (`core/theme/zz_theme.dart`: `ZzColors`, `ZzRadius`, `ZzPalette` jasny/ciemny).
- Wspólne stany: `skeleton.dart`, `states.dart` (EmptyView/ErrorView), pasek offline (`connectivity_banner.dart`).
- Routing: stałe linki `/s/{slug}` z kodów QR, powrót po logowaniu, ochrona tras, zliczanie źródeł.
- PWA już częściowo: `manifest.json`, własny `sw.js` (network-first, API niecache'owane), ekran ładowania w
  `index.html`, podpowiedź instalacji (`install_hint.dart`), `.htaccess` z `no-cache` i HTTPS.
- Backend: transakcyjny checkout, limity, outbox, izolacja sklepów, testy.
- Zasady biznesowe w UI: opłata za dostawę osobno, rundy zakupowe, bramka zgód.

## 3. Problemy (od najważniejszych)

### P1 — pierwsze wrażenie i wydajność
1. **Start aplikacji waży ok. 3–4 MB**: `main.dart.js` 1,09 MB (gzip; 3,74 MB surowe) + CanvasKit 2,06 MB
   (Chrome) / 2,92 MB (Safari i inne). Na słabym telefonie i LTE to kilka–kilkanaście sekund kręciołka.
2. **Lista sklepów po 9 s nadal pokazywała szkielety** — API na darmowym Renderze usypia po 15 min i budzi się
   30–60 s. Dla klienta z kodu QR to wygląda jak awaria. To problem hostingu, nie kodu.
3. **Czcionki pobierane z Google w trakcie działania** (7 żądań do `fonts.gstatic.com`; pakiet `google_fonts`):
   opóźnienie, migotanie tekstu, brak offline, kwestia RODO (IP użytkownika trafia do Google).

### P2 — UX / hierarchia informacji
4. **Brak strony głównej** — start to goła lista sklepów. Nie ma wyszukiwarki, kategorii, „zamów ponownie",
   promocji ani powitania. Ikony `search`, `heart`, `filter`, `tag` istnieją w zasobach, ale nie są użyte.
5. **Dwa pełnoszerokie banery** (kod pocztowy + lokalizacja) zajmują górę ekranu i wyglądają jak komunikaty
   systemowe, a nie jak wybór adresu dostawy (wzorzec „Dostawa do: …" z inspiracji).
6. **Karta sklepu bez zdjęcia**: logo 48 px + tekst + pigułka. Model `Store` nie ma okładki, oceny, czasu ani
   sposobu dostawy — nie da się zbudować karty „obrazkowej" bez zmiany backendu.
7. **Oferta sklepu to płaska lista** — `categoryId` i endpoint kategorii istnieją, ale UI ich nie używa; brak
   szukania w sklepie, brak karty produktu ze zdjęciem (jest tylko wiersz z miniaturą).
8. **Checkout to jeden długi ekran (616 linii)** z ręcznym wyborem strefy z listy rozwijanej, terminem, rundą,
   zgodami — za dużo decyzji naraz dla osoby nietechnicznej.
9. **Puste stany bez ilustracji na żywo** — pusty koszyk pokazał sam tekst (plik `empty_cart.svg` jest w repo,
   ale się nie wyrenderował) i nie ma przycisku „Przeglądaj sklepy".
10. **Brak onboardingu i brak animacji** (poza szkieletami, `AnimatedSwitcher` w jednym miejscu i osią statusu).

### P3 — wygląd
11. **Kontrast**: biały tekst na turkusie `#14B9BA` ma ok. 2,4:1 (wymóg WCAG 4,5:1) — dotyczy każdego głównego
    przycisku; nieaktywny przycisk logowania jest prawie nieczytelny.
12. Wygląd „systemowy": szare tło, cienkie ramki, promień 10–16, brak koloru poza turkusem, brak ilustracji —
    bliżej panelu niż aplikacji konsumenckiej.
13. 21 kolorów wpisanych na sztywno poza motywem (np. `0xFF128040`, `0xFFFEECEC`); tokeny nazywają się
    `orange*`, a trzymają turkus — mylące przy rozbudowie.
14. Brak skali odstępów, cieni, czasów animacji jako tokenów (są tylko kolory i 3 promienie).

### P4 — responsywność i dostępność
15. **Brak układu na tablet/komputer**: w całej aplikacji jest jedno użycie `maxWidth`. Na monitorze lista
    rozciąga się na całą szerokość, a nawigacja zostaje na dole — sprzeczne z celem „wygodnie na komputerze".
16. `Semantics` użyte w 3 plikach; Flutter web wystawia drzewo dostępności dopiero po włączeniu — czytniki
    ekranu praktycznie nie działają. Brak obsługi `prefers-reduced-motion` w aplikacji (jest tylko w `index.html`).

### P5 — panel i dokumentacja
17. Panel: 0 testów, 1 zapytanie `@media` w CSS (nieużywalny na telefonie), wygląd spójny, ale czysto użytkowy.
18. Skill projektowy `.claude/skills/zipzap-app-design` nadal opisuje **pomarańczowy** `#F97316` — sprzeczny z
    marką (turkus). Główny katalog repo jest na starej gałęzi `pilot-s0-rounds`, lokalne `main` nieaktualne.

## 4. Bezpieczeństwo (w zakresie redesignu)
Bez krytycznych uwag: refresh token w `flutter_secure_storage`, SW nie cache'uje API, trasy chronione, sekrety
poza repo. Do pilnowania przy PWA: nie cache'ować odpowiedzi API ani danych konta; `theme`/ustawienia można.
Ryzyko otwarte z wcześniejszych etapów: tryb Development na produkcji (CORS dla każdego originu).

## 5. Ryzyka redesignu
- Flutter web: waga startu i dostępność mają twardy sufit — da się poprawić (czcionki lokalnie, WebAssembly,
  mniej zależności), nie da się zejść do poziomu strony HTML. Decyzja F-1.
- Część nowego UI (okładki sklepów, kategorie globalne, wyszukiwarka, ulubione, promocje) wymaga **nowych danych
  z backendu i panelu** — to zmiany funkcjonalne, nie tylko wygląd. Decyzje F-5…F-8.
- Zdjęcia produktów pochodzą od sklepów — bez nich „obrazkowe" karty będą puste; potrzebny dobry placeholder.
