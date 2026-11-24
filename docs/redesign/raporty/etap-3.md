# Raport Etapu 3 — Redesign ekranów (Claude Code → ChatGPT Codex)

Etap rozpoczęty 2026-11-24 po PASS Etapu 2 (Codex, `6ef4b1e`) i akceptacji właściciela. Raport rośnie z każdym
zadaniem. Status wg Claude Code: **E3-1a i E3-1b gotowe do weryfikacji** — nie uznaję zadania ani etapu za zaliczone.
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

---

## E3-1b — Okładka i sposób dostawy (F-5), kategorie platformy (F-6)

Baza: `f4a63cf`. Status wg Claude Code: **gotowe do weryfikacji**. Zmiana obejmuje serwer, bazę danych, panel
i aplikację. **Nic nie wdrożono**; migracji nie uruchamiano na produkcji (wymaga zgody właściciela i kopii bazy).

### Zakres

| Część | Co |
|---|---|
| Baza (migracja `20261124215822_Catalog_StoreCoverAndPlatformCategories`) | `catalog.stores`: `CoverUrl` (500), `DeliveryNote` (80); `catalog.categories`: `PlatformCategoryKey` (32); nowa tabela `catalog.platform_categories` (klucz, nazwa, kolejność, widoczność) z 11 wierszami startowymi. **Tylko dodaje** — nie zmienia ani nie usuwa istniejących danych; `Down` cofa wszystko |
| API | `StoreDto` + `coverUrl`, `deliveryNote`, `platformCategories` (klucze kategorii, w których sklep ma **dostępny** produkt; jedno dodatkowe zapytanie na całą listę); `CategoryDto` + `platformCategoryKey`; `GET /api/catalog/platform-categories` (publiczne, tylko widoczne); `GET/PATCH /api/catalog/admin/platform-categories[/{key}]` (administrator, wpis w audycie); `PUT /api/catalog/categories/{id}/platform-category` (obsługa tego sklepu albo administrator); `PATCH /stores/{id}` + `coverUrl` (tylko `http(s)://`), `deliveryNote` (≤ 80 znaków); `null` = bez zmian, `""` = wyczyść |
| Panel | nowa strona administratora „Kategorie" (nazwa, kolejność, widoczność; klucza nie zmienia się — to nazwa ikony w aplikacji); profil sklepu: „Okładka" z podglądem i „Sposób dostawy"; „Oferta": przy każdej kategorii sklepu wybór „W aplikacji pod", także przy dodawaniu kategorii. `styles.css`: `.formgrid` bez rozpychania kolumn (wcześniej 6 pól profilu dawało przewijanie w bok) |
| Aplikacja | `Store` + nowe pola (stare API bez nich → puste); `PlatformCategory` (nieznany klucz → ikona ogólna); Start: rząd chipów „Wszystko + kategorie" **tylko z kategorii obecnych w widocznych sklepach** (klient nie trafia na pustą listę; bez przypiętych kategorii rzędu nie ma), wybór filtruje sklepy, drugie dotknięcie wraca do wszystkich; `ZzStoreCard`: okładka nad treścią (16:9, najwyżej 140 px; nie wczyta się → znika) i linia „sposób dostawy" |

Kompatybilność wdrożenia: nowa aplikacja na starym API — brak pól i 404 na liście kategorii → karta z logo, bez
chipów (test „model…", provider łapie błąd). Stare aplikacje na nowym API — dodatkowe pola są ignorowane.

Celowo poza zakresem: import/eksport CSV nie niesie przypięcia do kategorii platformy (kategorie z importu
przypina się w „Ofercie"); nowych kategorii platformy nie dodaje się w panelu (nowa = nowa ikona w aplikacji).
Nowe obietnice biznesowe: brak — „sposób dostawy" to tekst sklepu, termin nadal tylko z rund.

### Wyniki

| Polecenie | Wynik |
|---|---|
| `dotnet test backend/ZipZap.sln` | **289/289** (integracyjne 225 — w tym 5 nowych w `StoreCoverAndPlatformCategoryTests`; Ordering 38, Identity 17, Payments 9) |
| `cd admin-panel && npx ng build` | bez błędów |
| `cd mobile && flutter analyze` | No issues found |
| `cd mobile && flutter test` | **124/124** (119 + 5 nowych w `home_screen_test.dart`) |
| `verify-e2.mjs` (lokalne API z migracją, `SIZES=360x780,1280x900`) | `googleRequests: 0`, przewijanie w bok 0 px; zrzuty `zrzuty-etap-3/e31b/` |

Nowe testy serwera: lista 11 kategorii w kolejności; sklep pod kategorią tylko z dostępnym produktem (ukrycie
produktu, przepięcie, odpięcie); nieznany klucz → 400, pracownik innego sklepu → 403, anonim → 401; tylko
administrator zmienia kategorie, ukryta znika z listy publicznej; okładka i opis — ustawienie, walidacja
(`javascript:` → 400, 81 znaków → 400), brak pola = bez zmian, `""` = wyczyść.

### Sprawdzenie na żywo (lokalnie)

API z migracją na **świeżej, tymczasowej bazie** (`zipzap_e31b`, usunięta po sprawdzeniu), seed pilotażu,
dane ustawione przez API, potem panel w przeglądarce:

- administrator → „Kategorie": 11 kategorii, „Zapisz" nieaktywny bez zmian; zmiana nazwy „Warzywa" → „Warzywa
  i zioła" zapisana;
- testowe konto sklepu (utworzone w tej bazie) → profil: okładka z podglądem, sposób dostawy, bez przewijania
  w bok; „Oferta": przepięcie „Spożywcze" na „Warzywa i zioła" → `GET /api/catalog/stores` zwraca
  `Rapacz — Rynek: warzywa`;
- aplikacja (`zrzuty-etap-3/e31b/start-360x780.png`, `start-1280x900.png`): chipy „Wszystko · Owoce · Nabiał",
  karta Rapacza z okładką, obie karty z linią sposobu dostawy.

Poprawki po tym sprawdzeniu (w tym commicie): okładka 16:9 w kolumnie 1280 px miała ~285 px i robiła kartę dwa
razy wyższą od sąsiedniej → limit 140 px; pola profilu w panelu wychodziły poza ekran → osobny wiersz + `min-width: 0`.

### Wdrożenie produkcyjne — do decyzji właściciela

1. Kopia bazy produkcyjnej → migracja (automatycznie przy starcie nowego API) → próba na kopii.
2. Dopiero potem nowa aplikacja (kolejność nie jest krytyczna — patrz „Kompatybilność").
3. Sklep pilotażowy przypina swoje kategorie w „Ofercie" i ustawia okładkę — bez tego Start wygląda jak w E3-1a.

### Pliki (`git diff --name-status f4a63cf..HEAD`)

- Nowe: `backend/src/Modules/ZipZap.Modules.Catalog/Domain/PlatformCategory.cs`, migracja `…_Catalog_StoreCoverAndPlatformCategories.cs` + `.Designer.cs`,
  `backend/tests/ZipZap.Api.IntegrationTests/StoreCoverAndPlatformCategoryTests.cs`, `admin-panel/src/app/platform-categories.ts`,
  `mobile/lib/models/platform_category.dart`, `docs/redesign/raporty/zrzuty-etap-3/e31b/{_wynik.json,start-360x780.png,start-1280x900.png}`,
  `docs/redesign/raporty/etap-2-weryfikacja-2026-11-24.md` (raport Codexa z PASS Etapu 2 — dołączony bez zmian)
- Zmienione: Catalog `Api/CatalogEndpoints.cs`, `Api/CatalogRequests.cs`, `Application/CatalogDtos.cs`,
  `Application/CatalogService.cs`, `Domain/Category.cs`, `Domain/Store.cs`, `Infrastructure/CatalogDbContext.cs`,
  `Infrastructure/Migrations/CatalogDbContextModelSnapshot.cs`; panel `api.ts`, `app.routes.ts`, `catalog.ts`,
  `modules.ts`, `onboarding.ts`, `styles.css`; aplikacja `core/ui/zz_store_card.dart`,
  `features/catalog/catalog_repository.dart`, `features/home/{home_providers,home_screen}.dart`, `models/store.dart`,
  `test/home_screen_test.dart`; `docs/redesign/05-decyzje.md`; ten raport
