# Raport Etapu 3 — Redesign ekranów (Claude Code → ChatGPT Codex)

Etap rozpoczęty 2026-11-24 po PASS Etapu 2 (Codex, `6ef4b1e`) i akceptacji właściciela. Raport rośnie z każdym
zadaniem. Status wg Claude Code: **E3-1a, E3-2 i E3-3 gotowe do weryfikacji; E3-1b przywrócone na `main` 2026-11-27 po kopii bazy (patrz rozdział E3-1b)** — nie uznaję zadania ani etapu za zaliczone.
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

> **Cofnięte z `main` (2026-11-24).** Commit `a486d3c` został wypchnięty na `main`, co uruchomiło automatyczne
> wdrożenie API na Render. Render zdążył wdrożyć `a486d3c` — **migracja wykonała się na bazie produkcyjnej**
> (tylko dodaje: 3 puste kolumny i tabela 11 kategorii). Właściciel wybrał cofnięcie: `ff3f30c` (revert) wdrożony
> ~1 min później; produkcja działa na starym kodzie (`/api/catalog/platform-categories` = 404, `/health/ready` = 200).
> Kolumny i tabela zostają w bazie nieużywane; po kopii bazy i zgodzie właściciela kod wraca przez `git revert ff3f30c`
> (migracja jest już zapisana w historii, nie wykona się drugi raz). Do weryfikacji Codexa: kod w `a486d3c`.
>
> **Przywrócone 2026-11-27 za zgodą właściciela.** Przed przywróceniem: kopia bazy produkcyjnej
> `neondb-przed-e31b-20261127-213835.dump` (129 256 B, SHA256 `A33412FD…ADF5`, poza repozytorium), odtworzona lokalnie
> (63 tabele, liczby wierszy identyczne z produkcją we wszystkich tabelach). Uwaga dla odtwarzania na zwykłym
> PostgreSQL: zrzut z Neona zawiera 7 poleceń `ALTER … SET (pg_attver=…)` dla kolumn dodanych tą migracją — trzeba je
> pominąć (`pg_restore -f` → `grep -v "SET (pg_attver="` → `psql`), inaczej trzy tabele się nie utworzą. Próba
> generalna: API z kodem E3-1b na odtworzonej kopii — `/health/ready` 200, żadna migracja nie była do wykonania,
> dane bez zmian. Kod wrócił jako commit cofający `ff3f30c` na `1c08301` (konflikty tylko z formatowaniem
> i `ZzStatusChip(status:)` z E3-2), po `dart format`; `dotnet test` 289/289, `flutter test` 148/148, panel buduje się.

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

---

## E3-2 — Sklep i produkty

Baza: `ff3f30c` (`main` po cofnięciu E3-1b). Status wg Claude Code: **gotowe do weryfikacji**. Zmiana **tylko
w aplikacji** (bez serwera, panelu i bazy); nie wdrożono.

### Co zmieniono

| Element | Przed | Po |
|---|---|---|
| Nagłówek | karta z ogólną notką „opłatę i termin wybierzesz przy zamówieniu" | ten sam układ na komponentach `core/ui`; notka = termin z najbliższej rundy („Zamów jutro do 11:30 · dostawa od jutro, 13:00", ten sam publiczny endpoint co Start); bez rundy — dawna notka |
| Kategorie | brak (endpoint istniał, UI go nie używało) | przyklejone zakładki „Wszystko + kategorie sklepu" (`GET /catalog/stores/{id}/categories`), **tylko kategorie z produktami**; drugie dotknięcie wraca do wszystkich; błąd zapytania = oferta bez zakładek |
| Szukanie | brak | „Szukaj w {sklep}" — po nazwie, bez wielkości liter i polskich znaków, słowa w dowolnej kolejności („ser zolty" → „Żółty ser"); działa razem z zakładką; pusty wynik → „Nic nie znaleźliśmy" + „Pokaż wszystkie produkty". Bez zapytań do serwera (filtr na pobranej ofercie). Decyzja F-7: szukanie tylko w sklepie |
| Układ | lista kart (`Card`) | telefon: wiersze `ZzProductRow`; treść ≥ 600 px: siatka `ZzProductTile` (kolumny ≤ 240 px, wysokość kafelka liczona ze skali tekstu); obie budowane leniwie (`SliverList`/`SliverGrid`) |
| „+" | przycisk „Dodaj" ↔ licznik bez przejścia | „+ Dodaj" → licznik: przenikanie + skala, 200 ms (`ZzMotion.base`); „ogranicz ruch" → 0 ms. Logika koszyka bez zmian (koszyk przy pierwszym dodaniu, pytanie przy innym sklepie, wybór jednostki) — przeniesiona 1:1 do `ProductAddButton` |
| Sklep zamknięty | `StoreClosedNotice` (własna ramka) | `ZzNotice` (ostrzeżenie), ten sam tekst |
| Nie ma sklepu | `_StoreNotFound` | `EmptyView` z ilustracją i przyciskiem „Zobacz sklepy" (`EmptyView` dostał opcjonalny `action`) |

Lokalne widżety z listy E2-3 przypisane do E3-2: `_ProductRow` → `ZzProductRow`/`ZzProductTile` (+ `ProductAddButton`
w `features/catalog` — akcja z logiką koszyka, nie karta); `_StatusPill` → `ZzStatusChip(status:)` (także
„Niedostępne"); `_InfoChip` → `ZzInfoChip`; `_StoreNotFound` → `EmptyView(action:)`. Zostają prywatne, bo nie są
kartami: `_Frame` (Scaffold z przyciskami paska), `_StoreOffer` (stan: wyszukiwanie i wybrana zakładka),
`_TabsHeader` (delegat przyklejonego paska, nie widżet). Nowe w `core/ui`: `ZzProductRow`, `ZzProductTile`,
`ZzProductImage`, `ZzFilterPill`, `ZzInfoChip`; w `core/util`: `normalizeSearch`, `matchesSearch` (Szukaj sklepów
korzysta z tej samej funkcji — dawna lokalna `_norm` usunięta).

### Zmiana w istniejącym teście (jawnie)

`qr_entry_flow_test.dart`, 2 miejsca: przed `tap('Dodaj')` dodane `ensureVisible`. Powód: nagłówek z terminem
i pole szukania wydłużyły stronę — w oknie testu 800×600 pierwszy przycisk był na 608 px i stuknięcie trafiało
poza ekran (ostrzeżenie testu „would not hit test"). Test nadal sprawdza to samo (dodanie z wejścia QR,
odtworzenie koszyka, powrót po logowaniu); nic nie osłabiono.

### Wyniki

| Polecenie | Wynik |
|---|---|
| `cd mobile && flutter analyze` | No issues found |
| `cd mobile && flutter test` | **133/133** (119 + 14 w `store_offer_test.dart`); `store_header_test` bez zmian i przechodzi |
| `verify-e2.mjs` — lokalne API, sklep mały (4 produkty) i duży (2004 produkty, 9 kategorii z importu CSV), 360×780 i 1280×900 | `googleRequests: 0`, przewijanie w bok 0 px; węzły dostępności na dużym sklepie 37 (360) / 50 (1280) — buduje się tylko widoczna część listy; zrzuty `zrzuty-etap-3/e32/` |

Nowe testy (`store_offer_test.dart`): zakładki bez pustych kategorii i w kolejności sklepu; szukanie (polskie
znaki, kolejność słów, spacje, z zakładką); 360 px = wiersze, wybór zakładki filtruje; szukanie + pusty wynik +
powrót; termin z rundy w nagłówku; błąd kategorii = oferta bez zakładek; lista/siatka bez przepełnień przy
840/640/599/360 px i tekście 100/130 %; 2000 produktów — na ekranie < 20 wierszy, szukanie znajduje „Produkt 1999";
„ogranicz ruch" = przejście 0 ms; zwykły ruch ≤ 250 ms.

`verify-e2.mjs`: doszły zmienne `PAGE` (dowolna strona), `WAIT_API` (na które zapytanie czekać) i `PREFIX`
(nazwy plików); bez nich działa jak wcześniej. Uwaga: w Git Bash `PAGE=/stores/…` zamienia się na ścieżkę
Windows — uruchamiać z PowerShell albo z `MSYS_NO_PATHCONV=1`.

### Otwarte

1. **Pomiar na telefonie przy dużym asortymencie** — telefon był odłączony (adb bez urządzeń). Dane i skrypt są
   gotowe; zrobię po podłączeniu (2004 produkty, lokalne API przez `adb reverse`).
2. Na komputerze pasek zakładek przewija się w bok (przy 9 kategoriach ostatnia jest ucięta) — myszą trzeba
   Shift+kółko; do rozważenia strzałki albo zawijanie w E3-9.
3. Nagłówek sklepu na telefonie zajmuje ~⅔ pierwszego ekranu (opis + 3 chipy + termin) — pierwszy produkt ok.
   500 px od góry przy 780 px. Do oceny właściciela; można zwinąć opis i telefon.

### Pliki (`git diff --name-status ff3f30c..HEAD`)

- Nowe: `mobile/lib/core/ui/zz_product.dart`, `mobile/lib/core/util/search_text.dart`,
  `mobile/lib/features/catalog/product_add_button.dart`, `mobile/lib/models/store_category.dart`,
  `mobile/test/store_offer_test.dart`, `docs/redesign/raporty/zrzuty-etap-3/e32/{_wynik-maly.json,_wynik-duzy.json,sklep-maly-360x780.png,sklep-maly-1280x900.png,sklep-duzy-360x780.png,sklep-duzy-1280x900.png}`
- Zmienione: `mobile/lib/core/ui/{ui,zz_store_card}.dart`, `mobile/lib/core/widgets/states.dart`,
  `mobile/lib/features/catalog/{catalog_repository,store_detail_screen,store_header}.dart`,
  `mobile/lib/features/search/search_screen.dart`, `mobile/test/qr_entry_flow_test.dart`,
  `docs/redesign/verify-e2.mjs`, `docs/redesign/05-decyzje.md` (F-5/F-6: stan po cofnięciu), ten raport (także przywrócony rozdział E3-1b)

---

## E3-3 — Koszyk

Baza: `357a76e`. Status wg Claude Code: **gotowe do weryfikacji**. Zmiana **tylko w aplikacji**; nie wdrożono.

### Co zmieniono

| Element | Przed | Po |
|---|---|---|
| Pusty koszyk | ikona/tekst bez akcji | ilustracja `state_empty_cart.svg`, „Przeglądaj sklepy"; gdy klient oglądał sklep — także „Wróć do: {sklep}" (zapamiętany sklep, ta sama ścieżka co po logowaniu) |
| Minimum zamówienia | ramka z tekstem pod sumą | `ZzMinOrderProgress`: pasek postępu + „Brakuje 3,61 zł do minimum 20,00 zł" / „Minimum zamówienia osiągnięte (20,00 zł)"; czytnik ekranu dostaje tekst i procent; pasek rośnie z animacją 250 ms, przy „ogranicz ruch" 0 ms. Blokada przejścia dalej — bez zmian (`subtotal < minimum`) |
| Pozycje | wiersze z separatorem, kosz 28 px | `ZzCartItem`: nazwa + wartość, „2 × 4,99 zł / kg", licznik (z wpisaniem ilości z E2), „Usuń" jako przycisk 48 × 48 z etykietą „Usuń: {produkt}"; dwa rzędy — w jednym rzędzie przy 360 px i tekście 130 % przelewało się o 65 px |
| Sklep | brak | „Rapacz — Rynek" + „Dodaj więcej" (oferta sklepu) |
| Termin | brak | `ZzNotice` „Najbliższa runda zakupów": „Zakupy w sklepie: Jutro, 12:00. Zamów jutro do 11:30. Godzinę dostawy wybierzesz w następnym kroku." — z publicznego endpointu rund; bez rundy — brak ramki (checkout i tak sprawdza) |
| Podsumowanie | `_CartFooter` (lokalny) | stopka na `ZzSummaryRow`: „Dostawa — po wyborze adresu", „Razem bez dostawy"; przycisk bez zmian („Przejdź do dostawy i płatności") |

`cart_controller.dart` i `cart_controller_test.dart` — **bez zmian** (kryterium planu); ekran woła te same metody
(`setQuantity`, `remove`). Lokalny `_CartFooter` (E2-3 → E3-3) usunięty; nowe w `core/ui`: `ZzMinOrderProgress`,
`ZzCartItem`, `ZzSummaryRow` (posłuży też w checkoucie E3-4 zamiast `_SummaryRow`).

Poprawki po zrzutach (w tym commicie): wiersz „Produkty" usunięty — przed wyborem adresu miał tę samą kwotę co
„Razem bez dostawy" (niższa stopka na 375 px); `ZzSummaryRow` przy 1280 px trzymał wartość w połowie szerokości
(`Expanded` + `Flexible`) → luźne `Flexible` + `spaceBetween`, test położenia.

### Wyniki

| Polecenie | Wynik |
|---|---|
| `cd mobile && flutter analyze` | No issues found |
| `cd mobile && flutter test` | **143/143** (133 + 10 w `cart_screen_test.dart`); `cart_controller_test` bez zmian i przechodzi |
| `verify-e2.mjs` — lokalne API (seed pilotażu), dodanie 2 produktów kliknięciami (`CLICKS`), 375×812 i 1280×900; pusty koszyk 375/1280 | `googleRequests: 0`, przewijanie w bok 0 px; zrzuty `zrzuty-etap-3/e33/` |

Testy (`cart_screen_test.dart`; kontroler podstawiony, zapisuje wywołania): pusty → „Przeglądaj sklepy" prowadzi
do listy; pusty po wizycie → „Wróć do: Lewiatan" prowadzi do `/s/lewiatan`; poniżej minimum — brakująca kwota
i zablokowany przycisk; minimum osiągnięte — przejście do `/checkout`; „Usuń" ≥ 48 px i licznik wołają kontroler
(`remove`, `set … 2`); tekst rundy; 360 i 768 px przy tekście 130 % bez przepełnień; „ogranicz ruch" — pasek 0 ms;
wartość podsumowania przy prawej krawędzi (640 px).

`verify-e2.mjs`: doszła zmienna `CLICKS="x,y;x,y"` (kliknięcia co 2 s przed zrzutem); bez niej jak wcześniej.

### Pliki (`git diff --name-status 357a76e..HEAD`)

- Nowe: `mobile/lib/core/ui/zz_cart.dart`, `mobile/test/cart_screen_test.dart`,
  `docs/redesign/raporty/zrzuty-etap-3/e33/{koszyk-375x812.png,koszyk-1280x900.png,koszyk-pusty-375x812.png,koszyk-pusty-1280x900.png,_wynik-koszyk-375.json,_wynik-koszyk-1280.json,_wynik-pusty.json}`
- Zmienione: `mobile/lib/core/ui/ui.dart`, `mobile/lib/features/cart/cart_screen.dart`, `docs/redesign/verify-e2.mjs`,
  ten raport
