# Raport Etapu 3 — Redesign ekranów (Claude Code → ChatGPT Codex)

Etap rozpoczęty 2026-11-24 po PASS Etapu 2 (Codex, `6ef4b1e`) i akceptacji właściciela. Raport rośnie z każdym
zadaniem. Status wg Claude Code: **E3-1a gotowe do weryfikacji** — nie uznaję zadania ani etapu za zaliczone.
Nowej wersji aplikacji **nie wdrożono** (app.dowózka.pl to nadal stara wersja).

## E3-1a — Start (część aplikacji, bez zmian serwera)

Baza: `181d281`. Zakres E3-1 z planu dzielę na dwie części:

| Część | Zakres | Stan |
|---|---|---|
| **E3-1a** | nagłówek z adresem zamiast dwóch banerów, powitanie, szukajka, baner najbliższej rundy, karty sklepów z terminem | ten commit |
| E3-1b | okładka sklepu i sposób dostawy na karcie (F-5), wspólne kategorie platformy (F-6) | następny krok: nowe pola w API i panelu + migracja (lokalnie; na produkcję tylko za zgodą i z kopią bazy) |
| — | „Zamów ponownie" i baner promocyjny | czeka na decyzję F-8 |

Plan dopuszcza wariant z logo bez F-5/F-6 („zależne od F-5/F-6 — bez nich wariant z logo").

### Co zmieniono

| Element | Przed | Po |
|---|---|---|
| Wybór kodu pocztowego | baner „Podaj kod pocztowy…" → okno z polem | nagłówek „Dostawa do: …" → arkusz: zapisane adresy (1 dotknięcie), pole kodu, „Najbliższe sklepy na górze", „Pokaż wszystkie sklepy" |
| Sortowanie wg odległości | osobny baner | pozycja w tym samym arkuszu; nagłówek pokazuje „najbliższe sklepy na górze" |
| Logika filtrów | `postalFilterProvider`, `myLocationProvider`, auto-uzupełnienie z domyślnego adresu | **bez zmian** (przeniesione do `home_providers.dart`); doszła tylko nazwa adresu do wyświetlenia (`postalLabelProvider`) |
| Szukajka | brak na Starcie | pole tylko do odczytu → zakładka Szukaj |
| Termin | brak na Starcie | baner najbliższej rundy + linia na karcie: „Zamów jutro do 11:30 · dostawa od jutro, 13:00" — z publicznego `GET /ordering/stores/{id}/purchasing-round`; jedno zapytanie na sklep (karta i baner czytają ten sam wpis); maks. 12 sklepów |
| Karta sklepu | status z prawej (zabierał szerokość) | status pod nazwą; nazwa maks. 2 linie |
| Plik ekranu | `features/stores/stores_screen.dart` | `features/home/home_screen.dart` (plan: `features/stores/*` → `features/home/*`) |

Termin graniczny zawsze z dniem: pierwszy zrzut pokazał wieczorem „Zamów do 11:30" przy rundzie jutro — na Starcie
nie ma obok daty rundy, więc wyglądało to jak dziś. Teraz: „Zamów do 11:30" (dziś), „Zamów jutro do 11:30",
„Zamów do 27.11, 11:30" (`cutoffPhrase`, test).

Nowe obietnice biznesowe: **żadne** — teksty pokazują tylko to, co zwraca serwer (termin graniczny, najwcześniejsza
dostawa). Bez rundy (`available: false`) nie ma banera ani linii terminu.

### Kryteria szczególne E3-1

| Kryterium | Wynik | Dowód |
|---|---|---|
| wybór kodu w ≤ 2 dotknięciach | zapisany adres: 2 (nagłówek → adres) | test „zalogowany: … inny zapisany adres w 2 dotknięciach" |
| sklepy widoczne bez przewijania na 640 px | 360×640: dwie karty nad dolną nawigacją | `zrzuty-etap-3/start-360x640.png`; test „360 × 640" |
| 4 stany | ładowanie: szkielet (1 zegar, E2-5); pusty: ilustracja „brak sklepów" z treścią zależną od filtra; błąd: `ErrorView` z „Spróbuj ponownie"; offline: globalny pasek „Brak połączenia" + błąd z ponowieniem | testy „pusty…”, „błąd…” w `home_screen_test`; szkielet i pasek offline — testy E2 |

### Pomiar na telefonie (zaległość z E2 — semantyka i wydajność)

Realme RMX3311, Android, Chrome 158, okno 360×691 CSS px, kompilacja release z localhost przez `adb reverse`, API
Render (tylko publiczne odczyty). Surowe dane: `zrzuty-etap-3/_telefon.json`.

| Seria | odpowiedź listy sklepów (ms) | odpowiedź rundy (ms) | praca skryptów (s) | pamięć JS (MB) |
|---|---|---|---|---|
| zimny start (dane strony wyczyszczone), 3× | 1876 / 1322 / 1220 | 2086 / 1539 / 1435 | 1,47 / 0,93 / 0,90 | 22,5 / 26,7 / 37,9 |
| ciepły start, 3× | 1472 / 1240 / 1155 | 1686 / 1352 / 1374 | 1,02 / 0,85 / 0,80 | 24,4 / 26,7 / 37,2 |

Ograniczenia: pliki aplikacji szły po USB, więc pomiar **nie** pokazuje czasu pobierania przez sieć komórkową
(to E4-5/E5-3); katalog ma 2 sklepy. Pierwsza próba (47 s do pierwszego obrazu) była przy zablokowanym ekranie
telefonu — odrzucona. `overflowPx = 0` we wszystkich przebiegach.

Zrzut z telefonu (`telefon-start.png`, `adb screencap`, obcięty pasek stanu): tryb ciemny systemu, wszystko czytelne.
Uwaga: systemowy pasek gestów pod aplikacją jest biały w trybie ciemnym — do E4 (kolor motywu w manifeście).

### Zrzuty przed/po

- Przed: `zrzuty-etap-2/start-360.png`, `start-1280.png` (stary Start z dwoma banerami).
- Po: `zrzuty-etap-3/start-360x640.png`, `start-375x812.png`, `start-1280x900.png`, `telefon-start.png`.
  `_wynik.json`: 0 żądań do Google, przewijanie w bok 0 px.

### Wyniki

| Polecenie | Wynik |
|---|---|
| `cd mobile && flutter analyze` | No issues found |
| `cd mobile && flutter test` | **119/119** (108 + 11 w `home_screen_test.dart`) |
| `SIZES="360x640,375x812,1280x900" node docs/redesign/verify-e2.mjs <wyniki> <build>` | kod 0; `googleRequests: 0`; `horizontalOverflowAt: []` |

`verify-e2.mjs`: doszła zmienna `SIZES` (dowolne rozmiary okna); bez niej działa jak w Etapie 2.

### Pliki (`git diff --name-status 181d281..HEAD`)

- Nowe: `mobile/lib/features/home/{home_screen,home_providers,delivery_place_sheet}.dart`,
  `mobile/lib/core/ui/zz_place_header.dart`, `mobile/test/home_screen_test.dart`, ten raport,
  `docs/redesign/raporty/zrzuty-etap-3/{_wynik.json,_telefon.json,start-360x640.png,start-375x812.png,start-1280x900.png,telefon-start.png}`
- Zmienione: `mobile/lib/core/router/app_router.dart`, `mobile/lib/core/ui/{ui,zz_basics,zz_store_card}.dart`
  (`ZzSearchField.onTap`, `ZzStoreCard.roundLine`), `mobile/lib/features/search/search_screen.dart` (import),
  `docs/redesign/verify-e2.mjs`
- Usunięte: `mobile/lib/features/stores/stores_screen.dart` (`_PostalBanner`, `_LocationBanner` — przypisane do E3-1
  w zmianie kryterium E2-3; zastąpione przez `ZzPlaceHeader` z `core/ui` i arkusz)

Bez zmian: backend, panel, `cart_controller.dart`, repozytoria, modele.

Lokalne widżety w `features/home`: `_PlaceHeader` (łączy dostawców danych z `ZzPlaceHeader`), `_NextRoundNotice`
(wybiera rundę, rysuje `ZzNotice`), `_StoreGrid` (układ kolumn `ZzStoreCard`) — to złożenia z komponentów `core/ui`,
nie własne karty. `DeliveryPlaceSheet` to arkusz ekranu (publiczny, w `features/home`).
