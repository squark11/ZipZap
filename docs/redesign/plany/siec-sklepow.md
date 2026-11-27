# Plan: sieć sklepów — jedna oferta dla klienta, lokalizacja dobierana automatycznie

Status: **zatwierdzony przez Codexa** (4 rundy, 2026-11-27; przebieg: `raporty/siec-sklepow-plan-codex.md`). Przed kodowaniem. Decyzje właściciela 2026-11-27 (wiążące):

1. Klient wybiera **jeden sklep „Rapacz Market"**; nie musi wiedzieć, z której lokalizacji jest zamówienie. Która
   lokalizacja je przygotowuje — informacja **dla sklepów** (panel).
2. **Wspólna oferta całej sieci** (jeden katalog i cennik); klient przegląda produkty przed podaniem adresu.
3. **Przydział:** lokalizacja, której strefa dostaw obejmuje kod pocztowy klienta; przy kilku — **najbliższa wg
   położenia klienta, gdy je znamy** (GPS / zapisany adres ze współrzędnymi), **bez położenia — kolejność lokalizacji
   ustawiona przez sieć w panelu**; żadna — „Rapacz nie dowozi jeszcze pod ten kod". Bez geokodowania zewnętrznego.
   Kod QR ze sklepu nie zmienia przydziału — decyduje adres dostawy.
4. **Wspólny harmonogram rund** (jak dziś, globalny); każda lokalizacja ma własne rundy i kompletację.
5. **Stare APK:** zamówienie do sieci bez nowych danych → serwer odrzuca z komunikatem „Zaktualizuj aplikację, żeby
   zamówić w tym sklepie"; sklepy pojedyncze działają w starej wersji jak dotąd.

Zakres: serwer (Catalog, Ordering, Identity — zakresy uprawnień, Payments, Delivery, rozliczenia), panel, aplikacja.
**Migracje bazy** — na produkcję tylko za zgodą właściciela, po kopii bazy i próbie na kopii (jak E3-1b).

## 1. Model (opcja B)

- **Witryna sieci** = istniejący `Store` („Rapacz Market"): katalog (kategorie, produkty, ceny), logo, okładka,
  minimum zamówienia, dokumenty prawne, bramka płatności, rozliczenia. Jedyny sklep sieci widoczny dla klienta.
- **Lokalizacja** = `Store` z `ParentStoreId` = witryna: adres, współrzędne, strefy, terminy, zespół, rundy,
  kompletacja, dostawy. Bez własnego katalogu. **Niewidoczna dla klienta** (pkt 4).
- Ograniczenia: FK `ParentStoreId → stores.Id`, `ParentStoreId <> Id`, **jeden poziom** (rodzic nie może mieć
  rodzica — walidacja w serwisie + test), rodzic musi być witryną (nie lokalizacją). **`ParentStoreId` niezmienne
  po utworzeniu** (ustawiane tylko przy „+ Lokalizacja"; próba zmiany → 400, test) — autoryzacja przez projekcję
  (`ManagesViaChain`) nie może więc zostawić dostępu poprzedniemu właścicielowi. Nowe pole `FulfillmentPriority int`
  (kolejność z panelu, pkt 3 decyzji). Sklep pojedynczy: `ParentStoreId = null`, brak dzieci — zachowanie jak dziś.
- Zamówienie: **`orders.StoreId` = witryna** (bez zmiany znaczenia — koszyk, produkty, dokumenty, płatność, księga
  prowizji, faktura, `OrderDto.StoreId`), nowe **`orders.FulfillmentStoreId uuid null`** = lokalizacja realizująca.
  Stare zamówienia: backfill `FulfillmentStoreId = StoreId` w tej samej migracji, potem `ALTER … SET NOT NULL`
  (każde zamówienie ma lokalizację realizującą; dla sklepu pojedynczego = `StoreId`).
- Migracje addytywne: Catalog (`ParentStoreId`, `FulfillmentPriority`), Ordering (`orders.FulfillmentStoreId` +
  backfill + indeks; projekcja `catalog_stores` + `ParentStoreId`, `Latitude`, `Longitude`, `FulfillmentPriority`).
- Zdarzenia: `StoreRegistered/StoreUpdated` + `ParentStoreId`, `Latitude`, `Longitude`, `FulfillmentPriority`
  (nowe pola opcjonalne, wersja agregatu jak dziś) → po wdrożeniu `POST /api/catalog/admin/resync-projections`
  (istniejący) odświeża projekcję. Kontrakt zdarzeń zamówienia: `OrderPlaced` niesie `StoreId` = witryna (płatność,
  księga) **i** `FulfillmentStoreId`; `OrderReadyForPickup` i `OrderDelivered` niosą `FulfillmentStoreId` — Delivery
  zapisuje dostawę pod lokalizacją (tablica dostaw i uprawnienia kierowców lokalizacji jak dziś).
- **Prowizja po dostarczeniu:** handler `OrderDelivered` w Payments bierze sklep do księgi z **`Payment.StoreId`**
  (rekord płatności zamówienia = witryna), nie z `e.StoreId` zdarzenia dostawy. Brak płatności dla zamówienia → jak
  dziś (zachowanie bez zmian dla sklepów pojedynczych, test). **Stare zdarzenia z outboxu** bez `FulfillmentStoreId`
  (sprzed wdrożenia): konsumenci przyjmują `FulfillmentStoreId ?? StoreId` (dla nich witryna = lokalizacja).

## 2. Przydział i checkout (serwer, Ordering)

- `FulfillmentRouter.Resolve(storefrontId, postalCode, lat?, lng?)` na projekcji:
  1. kandydaci: aktywne i otwarte lokalizacje witryny (witryna bez dzieci → sama witryna);
  2. strefa: aktywna strefa kandydata obejmuje kod. **Strefa z pustą listą kodów („wszędzie") w lokalizacji sieci
     jest zabroniona** (walidacja przy tworzeniu/edycji strefy lokalizacji + panel), żeby nie łapała każdego kodu;
  3. kilka: najbliższa wg `lat/lng` klienta (Haversine do współrzędnych lokalizacji), bez współrzędnych — najniższy
     `FulfillmentPriority`, potem `Id` (deterministycznie);
  4. wynik: lokalizacja albo `not_served`.
- Publiczny `GET /api/ordering/storefronts/{storefrontId}/delivery-options?postalCode=&lat=&lng=` →
  `{ served, zones:[{id, deliveryFee}], round, slots:[...] }` — **bez nazwy, adresu i id lokalizacji**; strefy bez
  nazwy lokalizacji (nazwa strefy klienta: „Dostawa"); `id` strefy i slotu to zwykłe id encji (nie ujawniają
  lokalizacji — z nich nie da się odczytać adresu, bo publiczne odczyty lokalizacji są zamknięte, pkt 4).
- Checkout `POST /ordering/carts/{cartId}/checkout` (koszyk na witrynie): nowe pola **`postalCode`** (00-000)
  i opcjonalne `latitude/longitude`. Adres jest strukturą po stronie aplikacji: osobne pola „Ulica i numer",
  „Kod pocztowy", „Miejscowość"; aplikacja składa `deliveryAddress` jako „{ulica}, {kod} {miejscowość}", więc kod
  w tekście i w polu jest ten sam z konstrukcji. Serwer **sprawdza format kodu** (bez geokodowania — nie potwierdza
  fizycznego położenia, nie udaje, że to robi), liczy przydział ponownie, sprawdza, że wybrana strefa należy do
  **tej** lokalizacji i obejmuje kod, a **termin należy do wybranej strefy** (warunek z dzisiejszego kodu zostaje),
  inaczej 409 „Zmieniła się lokalizacja realizująca — wybierz termin ponownie"; runda = lokalizacji; zapisuje
  `FulfillmentStoreId` i kod w zamówieniu.
- Witryna sieci bez `postalCode` w żądaniu (stare APK) → 400 `update_required` z komunikatem o aktualizacji (decyzja 5).
  Sklep pojedynczy bez `postalCode` → jak dziś (strefa/slot sklepu, `FulfillmentStoreId = StoreId`).
- Rundy i kompletacja: `EnsureRoundAsync(FulfillmentStoreId, …)`; `RoundPickingService` filtruje po
  `FulfillmentStoreId`; zamienniki szukane w katalogu witryny (`Product.StoreId == order.StoreId`), nie lokalizacji.
- Płatność: bez zmian klucza — `Payment.StoreId` = witryna (bramka i księga sieci).

## 3. Uprawnienia (Identity + serwisy)

- Zakresy egzekwowane na serwerze, nie tylko w UI:
  - **pracownik witryny** (= właściciel sieci, np. Kinga i Paweł Rapacz): oferta sieci (katalog, import,
    kategorie), ustawienia witryny, **pełna obsługa wszystkich swoich lokalizacji** — odczyt i zapisy operacyjne
    (kompletacja, przydział kierowcy, statusy) oraz konfiguracja (tworzenie lokalizacji, strefy, terminy,
    kolejność). **Zamierzone** — to jego sklepy; decyzja zapisana jawnie;
  - **pracownik lokalizacji**: zamówienia, rundy, kompletacja, dostawy, strefy i terminy **swojej** lokalizacji;
    **bez** edycji oferty sieci.
- Implementacja: `ICurrentUser.ManagesStore(id)` bez zmian dla bezpośrednich przypisań; nowe
  `ManagesViaChain(locationId)` = użytkownik zarządza witryną-rodzicem lokalizacji (rozwiązywane przez projekcję/
  port Catalog, bez dopisywania claimów dzieci do tokenu). Odczyty i zapisy operacyjne oraz konfiguracja lokalizacji:
  `ManagesStore(location) || ManagesViaChain(location)`; edycja katalogu i ustawień witryny: tylko
  `ManagesStore(witryna)` (pracownik lokalizacji — 403); katalog lokalizacji nie istnieje (404).
- `GET /api/catalog/admin/store-tree`: admin — wszystko; pracownik witryny — witryna + jej lokalizacje; **pracownik
  lokalizacji — tylko jego lokalizacja(e)** (+ nazwa witryny tylko do odczytu, jako kontekst w panelu).
- Testy autoryzacji dla każdej pary rola × zasób (pkt 7).

## 4. Ukrycie lokalizacji przed klientem

Zamknięte wszystkie publiczne ścieżki, nie tylko lista:
- `GET /catalog/stores` i `/stores/serving` — tylko witryny i sklepy pojedyncze (witryna jest „serving", gdy
  router zwraca lokalizację dla kodu);
- `GET /catalog/stores/{idOrSlug}` dla lokalizacji → **odpowiedź witryny** (to samo co wejście na witrynę; adres
  lokalizacji nie wycieka); `/categories` i `/products` lokalizacji → 404 (lokalizacja nie ma katalogu);
- `GET /ordering/stores/{locationId}/zones|slots|purchasing-round` dla lokalizacji: **anonimowo i dla obcych → 404**
  (klient korzysta z `delivery-options` witryny); **zalogowany pracownik z zakresem** (`ManagesStore(location) ||
  ManagesViaChain(location)`) → dane jak dziś (panel: onboarding, strefy, terminy). Dla sklepów pojedynczych bez
  zmian (stara aplikacja). Testy: anonim 404, obcy pracownik 404, pracownik lokalizacji 200, właściciel sieci 200;
- `GET /catalog/products/{id}` dla produktu należącego do lokalizacji → 404 (lokalizacje nie mają katalogu; dotyczy
  produktów sprzed przepięcia) — test na istniejącym produkcie lokalizacji;
- stare QR `/s/{slugLokalizacji}` → aplikacja dostaje witrynę (jak wyżej) i otwiera „Rapacz Market"; wejście
  zliczane dla lokalizacji z kodu (statystyka dla sklepu — informacja dla sklepów), przydział i tak wg adresu;
- `GET /api/stores/{storeId}/legal` (publiczny) dla lokalizacji → **dokumenty witryny** (spójnie ze szczegółami
  sklepu); test;
- **odpowiedzi zamówień dla klienta bez lokalizacji:** `checkout`, `GET /ordering/orders/mine`, `GET /ordering/orders/{id}`
  zwracają `CustomerOrderDto` **bez** `FulfillmentStoreId` i nazwy/adresu lokalizacji (osobny DTO, nie „pole
  ignorowane przez aplikację"); panel dostaje `StoreOrderDto` z `FulfillmentStoreId` i nazwą lokalizacji
  („Realizuje"). Test: odpowiedź JSON dla klienta nie zawiera tych pól;
- panel (zalogowany) dostaje drzewo przez `GET /api/catalog/admin/store-tree` (pkt 3) zamiast publicznej listy.

## 5. Panel

- Zakładki lokalizacji (Zamówienia, Rundy, Dostawy, Strefy/terminy, Zespół) — per lokalizacja, przełącznik
  „Rapacz Market / Raba Wyżna / …". Witryna: Oferta, Profil, Dokumenty, Płatności, Rozliczenia, Lokalizacje
  (lista, „+ Lokalizacja", kolejność przydziału, ostrzeżenie o nakładających się strefach i o strefie bez kodów).
- Zamówienia w witrynie: wszystkie lokalizacje + kolumna „Realizuje: Raba Wyżna" (informacja dla sklepów).
- Oferta w lokalizacji: „Oferta jest wspólna dla sieci — edytuj w Rapacz Market".

## 6. Aplikacja (razem z E3-4 — plan E3-4 do aktualizacji przed kodowaniem)

- Start/Szukaj/QR: witryna „Rapacz Market"; linia terminu z `delivery-options`, gdy w nagłówku jest kod.
- Sklep: oferta witryny, bez adresu lokalizacji.
- Checkout: krok 1 — adres + **osobne pole kodu pocztowego** (00-000; walidacja formatu; wstępnie z nagłówka Startu,
  jeśli podany) + telefon; po kodzie `delivery-options` daje strefy/terminy/rundę; krok 2 — terminy; nigdzie nazwy
  lokalizacji. Współrzędne wysyłane tylko, jeśli klient sam włączył lokalizację na Starcie. `not_served` →
  komunikat w kroku 1. `update_required` nie dotyczy nowej aplikacji.
- Plan E3-4: niezmiennik 2.1 (żądanie bez zmian) zostaje zmieniony świadomie decyzją właściciela — nowe pola
  `postalCode`, `latitude/longitude`; niezmiennik 2.3 — nowy odczyt `delivery-options` zastępuje `zones`/`slots`/
  `purchasing-round` dla witryny sieci. Aktualizacja planu E3-4 + ponowny przegląd Codexa przed kodowaniem.

## 6a. Dane Rapacza

`seed-rapacz.mjs` przebudowany na model sieci: witryna „Rapacz Market" (katalog i kategorie **tylko w witrynie**,
logo; opłata 9,90 zł w strefach lokalizacji) + 4 lokalizacje podrzędne (adresy, współrzędne, strefy bez nakładania,
terminy, kolejność przydziału). Idempotentny; po przebiegu lokalizacje nie są publicznymi sklepami (test: lista
klienta = tylko witryna). Wyszukiwanie istniejących lokalizacji przez **`GET /api/catalog/admin/store-tree`** (lista
publiczna ich nie zwraca) — kolejne uruchomienie nie tworzy duplikatów. Stare samodzielne sklepy `rapacz-*`
z poprzedniej wersji skryptu: przepinane jako lokalizacje (rodzic ustawiany raz, przy przepięciu — jedyny wyjątek od
niezmienności, wykonywany przez administratora przed pierwszym użyciem) **tylko jeśli nie mają zamówień ani płatności**
— wtedy usuwany jest ich przykładowy katalog. Sklep z zamówieniami → skrypt przerywa z komunikatem „Sklep ma
historię zamówień — przepięcie wymaga osobnej migracji danych". Historycznych zamówień i księgi skrypt nie migruje;
dotyczy wyłącznie baz lokalnych/testowych.

## 7. Testy

- Serwer: model (FK, jeden poziom, rodzic nie-lokalizacja, strefa bez kodów zabroniona w lokalizacji); router
  (1 pasuje; 2 pasują + współrzędne → najbliższa; 2 pasują bez współrzędnych → priorytet; zamknięta pominięta;
  żadna → not_served; sklep pojedynczy → sam); checkout (kod niezgodny z adresem → 400; strefa/slot innej
  lokalizacji/witryny → 409/404; zmiana przydziału → 409; stary klient bez `postalCode` do sieci → 400
  `update_required`; sklep pojedynczy bez `postalCode` → jak dziś; `FulfillmentStoreId` zapisany; płatność z
  `StoreId` witryny; księga i faktura sieci); kompletacja i zamienniki z katalogu witryny; dostawa pod lokalizacją;
  widoczność (lista, szczegóły i dokumenty lokalizacji → witryna, produkty/kategorie lokalizacji → 404, strefy/sloty/rundy lokalizacji → 404 dla anonima i obcego pracownika,
  200 dla pracownika lokalizacji i właściciela sieci,
  odpowiedzi zamówień klienta bez `FulfillmentStoreId`); prowizja po dostarczeniu w księdze witryny (z `Payment.StoreId`);
  stare zdarzenie bez `FulfillmentStoreId` przetworzone (`?? StoreId`); po migracji `FulfillmentStoreId` NOT NULL;
  uprawnienia (pracownik lokalizacji: swoje zamówienia tak, cudze nie, oferta nie; pracownik witryny: odczyt
  wszystkich lokalizacji tak, oferta tak; obcy sklep nie); migracje addytywne + backfill; projekcja po resync.
- Panel: build + test ręczny drzewa i zamówień z kolumną „Realizuje".
- Aplikacja: lista bez lokalizacji, checkout z kodem → strefy z `delivery-options`, brak nazwy lokalizacji w UI,
  stary QR lokalizacji otwiera witrynę.
- E2E lokalnie na danych Rapacza (witryna + 4 lokalizacje, strefy bez nakładania).

## 8. Kolejność i ryzyka

1. Serwer: model + migracje + zdarzenia/projekcja + router + `delivery-options` + checkout + uprawnienia + ukrycie
   (testy). 2. Panel. 3. Plan E3-4 zaktualizowany → przegląd → aplikacja. 4. `seed-rapacz.mjs` na model sieci (opłata za dostawę 9,90 zł w każdej strefie — decyzja właściciela 2026-11-27).
5. Kopia bazy + próba na kopii + resync projekcji → wdrożenie za zgodą.

Ryzyka: wyciek danych lokalizacji przez nieobjęty endpoint (lista kontrolna z pkt 4 w testach); stare APK u klientów
(komunikat aktualizacji); projekcja bez resync (router bez współrzędnych/rodzica) — krok wdrożenia; nakładające się
strefy (ostrzeżenie w panelu, deterministyczny priorytet).
