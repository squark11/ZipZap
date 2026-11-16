# Raport zbiorczy — Etap 1 (koncepcja wizualna) i Etap 2 (fundamenty frontendowe)

Data: 2026-11-16. Autor: Claude Code. Zastępuje jako punkt wejścia raporty `etap-1.md` i `etap-2.md`
(zostają w repozytorium jako historia rund). Zawiera: ocenę raportu Codexa z Etapu 2, ponowną weryfikację założeń
obu etapów względem planu (`02-plan-zadan-claude.md`) i listy kontrolnej (`03-weryfikacja-codex.md`), poprawki
wykonane po tej weryfikacji oraz stan otwartych spraw.

**Status wg Claude Code:** Etap 1 — zaliczony przez Codexa (runda 3, materiały PASS). Etap 2 — **gotowy do ponownej
weryfikacji**; nie uznaję go za zaliczony (wynik Codexa: FAIL, trzy warunki — wszystkie obsłużone poniżej).
Nowej wersji aplikacji **nie wdrożono** na produkcję.

Baza Etapu 2: `86ee2c9`. Commity etapu: `1d99044` (WIP, nie kompiluje się), `8fce729`, `ea08e69` oraz commit
z poprawkami tej rundy (HEAD).

---

## 1. Weryfikacja raportu Codexa z Etapu 2 (`etap-2-codex.md`, wynik FAIL)

Każde ustalenie Codexa sprawdziłem w repozytorium.

| Ustalenie Codexa | Moja ocena | Dowód | Działanie |
|---|---|---|---|
| Raport nie wymienia `mobile/pubspec.lock` i `raporty/etap-1-codex.md` (blokada z listy D) | **słuszne** | `git diff --stat 86ee2c9..ea08e69`: oba pliki w diffie. `pubspec.lock` zmienił się przez usunięcie `google_fonts`; `etap-1-codex.md` to edycja samego Codexa, którą dołączyłem do commita bez wpisu w raporcie | pełny inwentarz w §6 tego raportu i w `etap-2.md` (runda 2) |
| `git diff --check`: pusta linia na końcu `store_detail_screen.dart` | **słuszne** | `store_detail_screen.dart:382: new blank line at EOF` | usunięta; `git diff --check` czysty |
| Zrzuty głównego widoku `/stores` — NOT TESTED | **słuszne** (w moim raporcie były tylko 375 i 1280 px z panelu podglądu) | — | zrzuty 320 / 360 / 768 / 1280 / 1920 px z czystej przeglądarki (Edge bez okna): `raporty/zrzuty-etap-2/`; skrypt `docs/redesign/verify-e2.mjs` |
| Pomiar wydajności semantyki — NOT TESTED | **słuszne** | — | pomiar wykonany, §4 |
| Żądania do Google przy starcie — NOT TESTED (Codex przyjął mój wynik „0") | **mój wynik był błędny** | w czystym profilu przeglądarki: `www.gstatic.com/flutter-canvaskit/…/canvaskit.js` + `.wasm` oraz `fonts.gstatic.com/s/roboto/v32/….woff2`. Panel podglądu, którym mierzyłem, miał te pliki w pamięci podręcznej | naprawione, §3 poz. 1 |
| Fonty: 8 plików, 310 200 B | **zgodne** (mój raport podawał „ok. 320 KB" — to suma z plikami licencji: 318 960 B) | `ls -l mobile/assets/fonts` | liczba poprawiona |
| E2-1 … E2-6 — PASS | **częściowo się nie zgadzam: Codex ocenił za łagodnie E2-1, E2-3 i E2-5** | §3 poz. 2–6 | naprawione lub jawnie odłożone |
| Zmiana asercji w `qr_entry_flow_test.dart` nie osłabia testów | zgodne | 3 asercje: `'Koszyk (1)'` → `textContaining('1 produkt · ')` | — |

Wniosek: FAIL Codexa był zasadny. Dodatkowo własny przegląd wykazał 7 usterek, których Codex nie wskazał.

---

## 2. Etap 1 — ponowna weryfikacja założeń

Sprawdzone dziś na HEAD (nie przepisane z wcześniejszych raportów).

| Zadanie | Kryterium z planu | Stan faktyczny | Ocena |
|---|---|---|---|
| E1-1 Tablica kierunku | jedna strona: paleta, typografia, kształty (jasny i ciemny); kontrast każdej pary ≥ 4,5:1; właściciel wybiera paletę | artboard „Kierunek wizualny — paleta, typografia, komponenty" w `makiety/Main.dc.html`; paleta przyjęta 2026-11-14 (F-2). Kontrast par jest teraz liczony automatycznie w `redesign_foundation_test.dart` (15 par × 2 motywy, wszystkie ≥ 4,5:1; najniższa: sukces na swoim tle 4,54) | spełnione. Odstępstwo: plan podawał plik `makiety/kierunek.html` — jest artboard na płótnie, nie osobny plik |
| E1-2 Design System | komplet tokenów + każdy komponent z wariantami, stanami, wymiarami; skill bez pomarańczu | `design-system.md`: 8 rozdziałów, 17 komponentów w tabeli §4; `SKILL.md`: 0 wystąpień pomarańczu, paleta turkusowa | spełnione |
| E1-3 Makiety | 8 ekranów w 375 px; 1280 px dla Startu, Sklepu, Zamówienia; reszta regułą §7 (zmiana przyjęta przez właściciela); 4 stany każdego ekranu; dane spoza API oznaczone | 16 plików `*.dc.html`, 17 artboardów w `canvas.json`; macierz `StanyEkranow.dc.html` 8 × 4; reguła jednokolumnowa w `design-system.md` §7 | spełnione (Codex, runda 3: PASS) |
| E1-4 Ilustracje i ikony | maskotka, 3 plansze, 6 stanów, 12 ikon kategorii, ikony aplikacji; każdy plik ≤ 12 KB; czytelne w trybie ciemnym; akceptacja właściciela | 10 ilustracji + 12 ikon; największy plik 1 911 B (`onboarding_3_dostawa.svg`); 24 testy parsowania w zestawie; ikony aplikacji w `mobile/web/app-icons/` (5 PNG + SVG); przyjęte z kierunkiem (F-12) | spełnione. Odstępstwa: bez SVGO (pliki generuje `gen-svg.mjs`, są już minimalne); grafitowe elementy ilustracji na ciemnym tle mają 1,25:1 (dekoracyjne, z turkusową obwódką) — do oceny przy użyciu w Etapie 3 |
| E1-5 Ruch | lista animacji z czasem i krzywą; nic > 250 ms (start ≤ 1,2 s); zachowanie przy „ogranicz ruch" | `design-system.md` §6: 9 animacji, wszystkie ≤ 250 ms; połysk szkieletu = przebieg 250 ms powtarzany co 1 s | spełnione warunkowo: interpretacja „limit dotyczy jednego przebiegu wskaźnika czekania" nadal **czeka na potwierdzenie właściciela** |

Stan użycia materiałów Etapu 1 w kodzie: z 10 ilustracji używana jest 1 (`state_no_stores.svg`, zakładka Szukaj),
ikony kategorii tylko w komponencie `ZzCategoryChip` (galeria). Reszta wchodzi w Etapie 3 — zgodnie z planem.

Otwarte z Etapu 1: Figma (F-3); komunikat dla błędu 429 jest propozycją (API nie zwraca tekstu); zasada
„kwota po skompletowaniu" nie jest obiecywana na makietach (niezatwierdzona biznesowo).

---

## 3. Etap 2 — ponowna weryfikacja założeń i usterki znalezione przy niej

### 3.1 Usterki (wszystkie poza raportem Codexa) i poprawki

| # | Usterka | Kryterium | Skutek dla klienta | Poprawka |
|---|---|---|---|---|
| 1 | Start łączył się z Google: silnik graficzny CanvasKit z `www.gstatic.com` i czcionka zapasowa Roboto z `fonts.gstatic.com` | E2-2: „0 żądań do fonts.gstatic.com i googleapis.com" | wolniejszy pierwszy start; po instalacji aplikacja nie uruchomiłaby się bez dostępu do Google | `build-app.ps1`: `--no-web-resources-cdn` (CanvasKit z paczki; katalog `canvaskit/` i tak był wgrywany na hosting, lh.pl podaje `.wasm` z gzip); `pubspec.yaml`: rodzina `Roboto` wskazuje na `Inter-Regular.ttf` (0 dodatkowych bajtów). Po poprawce w czystej przeglądarce: hosty żądań tylko `localhost` i API |
| 2 | Znak „⋮" w instrukcji instalacji (Android) nie istnieje w dołączonych czcionkach | E2-2 | przy otwarciu instrukcji pobranie czcionki z Google albo pusty kwadrat | tekst: „Stuknij menu (trzy kropki) w prawym górnym rogu." Sprawdziłem wszystkie znaki spoza ASCII w `lib/` (28): poza komentarzami w kodzie każdy ma glif |
| 3 | Biały tekst (`Colors.white`) na wypełnieniu `brandStrong` — w trybie ciemnym to biel na `#3FD3D4`, kontrast ok. 1,8:1 | E2-1 / WCAG 4,5:1 | w trybie ciemnym nieczytelne: plakietka koszyka i powiadomień, „Dodaj adres", kółka ładowania na przyciskach (6 ekranów), ikona bieżącego kroku zamówienia, pasek „Brak połączenia" | 14 miejsc → `context.zz.onBrand`; nowy test zakazuje `Colors.white` poza 4 plikami o stałym tle; test kontrastu rozszerzony o `onBrand` na sukcesie i błędzie oraz biel na graficie |
| 4 | Koszyk miał własny licznik `_QtyStepper` z polami dotyku 32 × 32 px | E2-3 („ekrany nie definiują własnych…"), E2-5 (48 px) | trudne trafianie w „−/+" w koszyku; brak etykiet dla czytnika ekranu | koszyk używa `ZzQtyStepper`; wspólny licznik dostał `onValueTap` (wpisanie ilości z klawiatury — funkcja zachowana), pole liczby ≥ 48 px; nowy test |
| 5 | Pulsowanie bieżącego kroku zamówienia (pętla 1 500 ms) działało przy „ogranicz ruch"; pasek „Brak połączenia" miał czas wpisany na sztywno (220 ms) | E2-5 | ruch mimo ustawienia systemowego | puls wyłączony przy „ogranicz ruch"; pasek przez `ZzMotion.of()` |
| 6 | Na szerokim ekranie karty sklepów były wyśrodkowane, a banery nad nimi wyrównane do lewej | E2-4 (spójność układu) | nierówny układ ekranu Start od ok. 1200 px | `Column(crossAxisAlignment: stretch)`; widać na `start-1920.png` |
| 7 | Podpis „Zamówienia" w dolnej nawigacji łamał się na dwa wiersze przy 360 px | E2-4 („360 px bez…") | „Zamówieni / a" | podpis 11 px bez odstępu między literami; `start-360.png` |

### 3.2 Kryteria po poprawkach

| Zadanie | Kryterium z planu | Stan faktyczny | Ocena Claude Code |
|---|---|---|---|
| E2-1 Tokeny v2 | role kolorów, `ZzSpace`, `ZzShadow`, `ZzMotion`, `ZzRadius`; stare nazwy jako `@Deprecated`; 0 × `Color(0x` poza motywem; przycisk ≥ 4,5:1 | `zz_theme.dart`; 0 × `Color(0x` (test); kontrast przycisku 5,36 (jasny) / 8,36 (ciemny); poza motywem zostały: `Colors.white` w 3 plikach o stałym tle (pasek koszyka na graficie, połysk szkieletu, logo jednobarwne) i `Colors.transparent` | spełnione z dwoma odstępstwami: brak aliasów `@Deprecated` (wszystkie użycia zmienione od razu), cienie w `ZzPalette` zamiast `ZzShadow` (różnią się w trybie ciemnym) |
| E2-2 Czcionki w paczce | `google_fonts` usunięte; 0 żądań do Google | 8 plików TTF, 310 200 B, licencje OFL; po poprawkach 1–2: 0 żądań (pomiar w czystej przeglądarce, 5 szerokości) | spełnione. Uwaga: tekst z danych sklepu spoza zestawu znaków (np. emoji w nazwie produktu) nadal spowoduje pobranie czcionki zapasowej — zestaw obejmuje łacinę i polskie znaki |
| E2-3 Biblioteka komponentów | `core/ui`: przycisk, pole, chip kategorii, karty, licznik, arkusz, komunikat, pasek wyszukiwania; galeria `/dev/ui` w debug; ekrany bez własnych przycisków i kart; test na komponent | jest: licznik, pasek koszyka, karta sklepu + chip statusu, komunikat, nagłówek sekcji, pole wyszukiwania, chip kategorii, `ZzPage`. Przycisk, pole tekstowe i arkusz są ustawione centralnie w motywie, bez osobnych klas. Galeria pod `kDebugMode` | **częściowo**. Nadal lokalne w ekranach: `_SlotCard`, `_RoundCard`, `_SummaryRow` (zamówienie), `_AddressCard` (adresy), `_ProductRow` (sklep), `_StatusPill`, `_InfoChip` (nagłówek sklepu), `_InfoBox` (reset hasła), `_PostalBanner`, `_LocationBanner` (Start). To elementy ekranów przebudowywanych w Etapie 3 (karta produktu, wybór terminu, kroki zamówienia są w `design-system.md` §4, ale nie w kodzie). Testów nie mają osobno: `ZzSectionHeader`, `ZzSearchField` (pokryte pośrednio testem `/search`) |
| E2-4 Układ responsywny | punkty 600/900/1200; `ZzPage`; nawigacja dolna < 900, boczna ≥ 900; 360/768/1280/1920 bez rozciągniętych list i poziomego przewijania; testy + zrzuty | testy 4 szerokości (typ nawigacji, szerokość kart ≤ 560 px, brak błędów układu); zrzuty 5 szerokości, w każdej `scrollWidth − innerWidth = 0` | spełnione. Odstępstwo: pliki `core/ui/zz_layout.dart` zamiast `core/ui/layout/*`. Poza kryterium: przy 320 px podpis „Zamówienia" i długa nazwa sklepu łamią się w środku wyrazu (`start-320.png`) |
| E2-5 Ruch i dostępność | `disableAnimations` zeruje czasy; semantyka na web; etykiety; tekst 130 % bez ucięć; pomiar wydajności semantyki | testy: czasy = 0, licznik i 4 komponenty przy 130 % w 320 px; semantyka włączona (11–17 węzłów na ekranie Start); pomiar w §4 | spełnione dla komponentów. Niesprawdzone: całe ekrany przy tekście 130 % (test obejmuje komponenty, nie pełne ekrany) |
| E2-6 Nawigacja 5 zakładek | Start · Szukaj · Koszyk · Zamówienia · Konto; `/stores` i linki QR działają; nowe testy przekierowań | 10 testów `qr_entry_flow_test.dart` zielonych; nowy test bezpośredniego wejścia na `/search` z filtrowaniem | spełnione. Szukaj obejmuje tylko sklepy (produkty: decyzja F-7, zadanie E3-10) |

---

## 4. Pomiar wydajności semantyki na web (ryzyko z E2-5)

Dwie kompilacje produkcyjne różniące się tylko wywołaniem `SemanticsBinding.ensureSemantics()`. Edge bez okna,
375 × 800, czysty profil, 3 przebiegi na wariant naprzemiennie. Scenariusz: wejście na `/stores` → dotknięcie
pierwszego sklepu → 50 przewinięć listy produktów. Dane z `Performance.getMetrics` (CDP).

| Miara (mediana z 3) | z semantyką | bez | różnica |
|---|---|---|---|
| Praca przeglądarki do wyświetlenia listy sklepów | 1,06 s | 1,13 s | w granicach rozrzutu (0,98–1,77 s) |
| Praca przeglądarki w scenariuszu (ok. 11 s) | 1,27 s | 1,15 s | +0,12 s (ok. +10 %) |
| w tym skrypty | 1,11 s | 1,03 s | +0,08 s |
| Pamięć JS po wczytaniu | 26,8 MB | 19,4 MB | +7,4 MB |
| Węzły DOM (Start / Sklep) | 130 / 137 | 92 / 92 | +38 / +45 |

Wniosek: koszt jest mały i nie zmienia odczuwalnej płynności na komputerze. **Ograniczenie:** pomiar na laptopie,
nie na telefonie; ekrany miały 2 sklepy i 4 produkty. Przy asortymencie Rapacza (setki produktów) pomiar trzeba
powtórzyć na telefonie — proponuję w Etapie 3 przy ekranie Sklepu.

---

## 5. Wyniki testów (HEAD)

| Polecenie | Wynik |
|---|---|
| `cd mobile && flutter analyze` | No issues found |
| `cd mobile && flutter test` | **99/99** (79 sprzed Etapu 2 + 16 z `ea08e69` + 4 nowe: zakaz `Colors.white`, start bez serwerów Google, licznik z wpisaniem ilości, wejście na `/search`) |
| `git diff --check 86ee2c9` | czysty |
| `grep -rn "Color(0x" mobile/lib` poza `core/theme` | 0 |
| `google_fonts` w `pubspec.yaml`, `pubspec.lock`, `lib/` | 0 |
| Hosty żądań przy starcie (5 szerokości, czysty profil) | tylko serwer aplikacji i API; 0 × `gstatic`, 0 × `googleapis` |
| Poziome przewijanie 320 / 360 / 768 / 1280 / 1920 | 0 px w każdej |
| Sekrety w diffie | brak (wzorce kluczy, pliki `.env`) |

Backend i panel bez zmian — `dotnet test` i testy panelu niepotrzebne. W istniejących testach zmieniono tylko
3 asercje tekstu paska koszyka (opisane w §1); nic nie usunięto ani nie wyłączono.

Zrzuty: `raporty/zrzuty-etap-2/start-{320,360,768,1280,1920}.png`, `sklep-375.png`. Odtworzenie:
`flutter build web --release --no-web-resources-cdn --dart-define=API_BASE_URL=<api> -o build/web-sem`, serwer
plików z przekierowaniem na `index.html` na porcie 8091, potem
`SHOTS_ONLY=1 node docs/redesign/verify-e2.mjs <katalog> http://localhost:8091 x`. Zrzuty używają
publicznego API produkcyjnego wyłącznie do odczytu listy sklepów.

---

## 6. Pełny inwentarz plików Etapu 2 (`git diff --name-status 86ee2c9..HEAD`, 66 plików: 30 nowych, 36 zmienionych)

**Nowe (30):**
- `docs/redesign/build_fonts.py`, `docs/redesign/verify-e2.mjs`
- `docs/redesign/raporty/etap-2.md`, `etap-2-codex.md` (plik Codexa), `etap-1-2-zbiorczy.md` (ten raport)
- `docs/redesign/raporty/zrzuty-etap-2/` — `start-320.png`, `start-360.png`, `start-768.png`, `start-1280.png`, `start-1920.png`, `sklep-375.png`
- `mobile/assets/fonts/` — `Inter-{Regular,Medium,SemiBold,Bold}.ttf`, `Poppins-{Regular,Medium,SemiBold,Bold}.ttf`, `OFL-Inter.txt`, `OFL-Poppins.txt`
- `mobile/lib/core/ui/` — `ui.dart`, `zz_basics.dart`, `zz_cart_bar.dart`, `zz_layout.dart`, `zz_qty_stepper.dart`, `zz_store_card.dart`
- `mobile/lib/features/dev/ui_gallery_screen.dart`, `mobile/lib/features/search/search_screen.dart`
- `mobile/test/redesign_foundation_test.dart`

**Zmienione (36):**
- `docs/redesign/raporty/etap-1-codex.md` (edycja Codexa z rundy 3), `docs/redesign/design-system.md` (licznik 48 px, podpis nawigacji 11 px)
- `mobile/pubspec.yaml`, `mobile/pubspec.lock` (usunięcie `google_fonts` i jego zależności), `mobile/hosting/build-app.ps1`
- `mobile/lib/main.dart`, `core/router/app_router.dart`, `core/router/main_scaffold.dart`, `core/theme/zz_theme.dart`
- `mobile/lib/core/widgets/` — `cart_button`, `connectivity_banner`, `install_hint`, `notifications_bell`, `skeleton`, `states`, `store_logo`, `test_order_banner`, `zz_logo`
- `mobile/lib/features/account/` — `account_screen`, `addresses_screen`, `change_password_screen`, `forgot_password_screen`, `login_screen`, `verify_email_card`
- `mobile/lib/features/` — `cart/cart_screen`, `catalog/store_detail_screen`, `catalog/store_header`, `checkout/checkout_screen`, `feedback/feedback_screen`, `notifications/notifications_screen`, `orders/my_orders_screen`, `orders/order_track_screen`, `payment/payment_screen`, `splash/splash_screen`, `stores/stores_screen`
- `mobile/test/qr_entry_flow_test.dart`

Bez zmian (sprawdzone diffem): `cart_controller.dart`, wszystkie `*_repository.dart`, `lib/models/*`, `backend/`,
`admin-panel/`. Zmiany w ekranach zamówienia, płatności i koszyka dotyczą wyłącznie kolorów, liczników i komunikatów —
żadnych wywołań API ani ładunków.

---

## 7. Otwarte sprawy

**Do decyzji właściciela**
1. Połysk szkieletów: czy limit 250 ms liczymy dla jednego przebiegu (tak jest zrobione)? To samo pytanie dotyczy
   pulsowania bieżącego kroku zamówienia (pętla 1 500 ms; wyłączona przy „ogranicz ruch").
2. F-7: wyszukiwanie produktów we wszystkich sklepach (wymaga zmiany serwera).
3. Czy lokalne karty wymienione w §3.2 (E2-3) mają zostać przeniesione do `core/ui` jeszcze w Etapie 2, czy przy
   przebudowie ekranów w Etapie 3. Moja rekomendacja: Etap 3 — te elementy zmienią wygląd, więc przenoszenie ich
   teraz to praca do wyrzucenia.

**Do oceny Codexa**
4. Trzy warunki ponownej weryfikacji: inwentarz (§6), zrzuty (§5), pomiar semantyki (§4).
5. Czy „częściowo" przy E2-3 blokuje etap, czy jest akceptowalne z przeniesieniem do Etapu 3.
6. Odstępstwa od planu: brak aliasów `@Deprecated`, cienie w `ZzPalette`, `zz_layout.dart` zamiast katalogu.

**Znane ograniczenia**
7. Szerokość 320 px (poniżej minimum 360 z planu): łamanie w środku wyrazu w nawigacji i na karcie sklepu.
8. Pełne ekrany nie są testowane przy tekście 130 % — tylko komponenty.
9. Pomiar wydajności tylko na laptopie i małym asortymencie.
10. Ekran Start ma jeszcze stary układ (banery kodu pocztowego i lokalizacji) — przebudowa to E3-1.
11. Commit `1d99044` w historii `main` nie kompiluje się (zapis pracy w toku); kompletny stan etapu = HEAD.
