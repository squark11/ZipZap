# Plan: sieć sklepów — jedna oferta dla klienta, lokalizacja dobierana automatycznie

Status: **projekt do przeglądu Codexa (runda 2)**. Decyzje właściciela 2026-11-27 (wiążące):

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
  rodzica — walidacja w serwisie + test), rodzic musi być witryną (nie lokalizacją). Nowe pole `FulfillmentPriority int`
  (kolejność z panelu, pkt 3 decyzji). Sklep pojedynczy: `ParentStoreId = null`, brak dzieci — zachowanie jak dziś.
- Zamówienie: **`orders.StoreId` = witryna** (bez zmiany znaczenia — koszyk, produkty, dokumenty, płatność, księga
  prowizji, faktura, `OrderDto.StoreId`), nowe **`orders.FulfillmentStoreId uuid null`** = lokalizacja realizująca.
  Stare zamówienia: backfill `FulfillmentStoreId = StoreId` w migracji (dla nich witryna = lokalizacja); po migracji
  pole wymagane w kodzie dla nowych zamówień.
- Migracje addytywne: Catalog (`ParentStoreId`, `FulfillmentPriority`), Ordering (`orders.FulfillmentStoreId` +
  backfill + indeks; projekcja `catalog_stores` + `ParentStoreId`, `Latitude`, `Longitude`, `FulfillmentPriority`).
- Zdarzenia: `StoreRegistered/StoreUpdated` + `ParentStoreId`, `Latitude`, `Longitude`, `FulfillmentPriority`
  (nowe pola opcjonalne, wersja agregatu jak dziś) → po wdrożeniu `POST /api/catalog/admin/resync-projections`
  (istniejący) odświeża projekcję. Kontrakt zdarzeń zamówienia: `OrderPlaced` niesie `StoreId` = witryna (płatność,
  księga) **i** `FulfillmentStoreId`; `OrderReadyForPickup` i `OrderDelivered` niosą `FulfillmentStoreId` — Delivery
  zapisuje dostawę pod lokalizacją (tablica dostaw i uprawnienia kierowców lokalizacji jak dziś).

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
  i opcjonalne `latitude/longitude`. Serwer: normalizuje kod, sprawdza, że **kod występuje w tekście adresu**
  (spójność adresu i kodu — odrzuca 400 „Kod pocztowy nie zgadza się z adresem"), liczy przydział ponownie,
  sprawdza, że wybrana strefa i termin należą do **tej** lokalizacji (inaczej 409 „Zmieniła się lokalizacja
  realizująca — wybierz termin ponownie"), runda = lokalizacji, zapisuje `FulfillmentStoreId` i kod w zamówieniu.
- Witryna sieci bez `postalCode` w żądaniu (stare APK) → 400 `update_required` z komunikatem o aktualizacji (decyzja 5).
  Sklep pojedynczy bez `postalCode` → jak dziś (strefa/slot sklepu, `FulfillmentStoreId = StoreId`).
- Rundy i kompletacja: `EnsureRoundAsync(FulfillmentStoreId, …)`; `RoundPickingService` filtruje po
  `FulfillmentStoreId`; zamienniki szukane w katalogu witryny (`Product.StoreId == order.StoreId`), nie lokalizacji.
- Płatność: bez zmian klucza — `Payment.StoreId` = witryna (bramka i księga sieci).

## 3. Uprawnienia (Identity + serwisy)

- Zakresy egzekwowane na serwerze, nie tylko w UI:
  - **pracownik witryny**: oferta sieci (katalog, import, kategorie), ustawienia witryny; **odczyt** zamówień,
    rund i dostaw wszystkich lokalizacji (z kolumną „Realizuje"); zarządzanie lokalizacjami (tworzenie, strefy,
    terminy, kolejność) — bo to właściciel sieci;
  - **pracownik lokalizacji**: zamówienia, rundy, kompletacja, dostawy, strefy i terminy **swojej** lokalizacji;
    **bez** edycji oferty sieci.
- Implementacja: `ICurrentUser.ManagesStore(id)` bez zmian dla bezpośrednich przypisań; nowe
  `CanReadAsChain(locationId)` = użytkownik zarządza witryną-rodzicem lokalizacji (rozwiązywane przez projekcję/
  port Catalog, bez dopisywania claimów dzieci do tokenu). Wszystkie odczyty zamówień/rund/dostaw lokalizacji
  akceptują `ManagesStore(location) || CanReadAsChain(location)`; zapisy operacyjne (kompletacja, przydział kierowcy)
  — `ManagesStore(location) || ManagesStore(parent)`; edycja katalogu lokalizacji — zabroniona (katalog jest w witrynie).
- Testy autoryzacji dla każdej pary rola × zasób (pkt 7).

## 4. Ukrycie lokalizacji przed klientem

Zamknięte wszystkie publiczne ścieżki, nie tylko lista:
- `GET /catalog/stores` i `/stores/serving` — tylko witryny i sklepy pojedyncze (witryna jest „serving", gdy
  router zwraca lokalizację dla kodu);
- `GET /catalog/stores/{idOrSlug}` dla lokalizacji → **odpowiedź witryny** (to samo co wejście na witrynę; adres
  lokalizacji nie wycieka); `/categories` i `/products` lokalizacji → 404 (lokalizacja nie ma katalogu);
- publiczne `GET /ordering/stores/{locationId}/zones|slots|purchasing-round` dla lokalizacji → 404 (dostęp tylko
  przez `delivery-options` witryny); dla sklepów pojedynczych bez zmian (stara aplikacja);
- stare QR `/s/{slugLokalizacji}` → aplikacja dostaje witrynę (jak wyżej) i otwiera „Rapacz Market"; wejście
  zliczane dla lokalizacji z kodu (statystyka dla sklepu — informacja dla sklepów), przydział i tak wg adresu;
- panel (zalogowany) dostaje drzewo przez nowy `GET /api/catalog/admin/store-tree` (pracownik witryny: witryna +
  dzieci; admin: wszystko) zamiast publicznej listy.

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

## 7. Testy

- Serwer: model (FK, jeden poziom, rodzic nie-lokalizacja, strefa bez kodów zabroniona w lokalizacji); router
  (1 pasuje; 2 pasują + współrzędne → najbliższa; 2 pasują bez współrzędnych → priorytet; zamknięta pominięta;
  żadna → not_served; sklep pojedynczy → sam); checkout (kod niezgodny z adresem → 400; strefa/slot innej
  lokalizacji/witryny → 409/404; zmiana przydziału → 409; stary klient bez `postalCode` do sieci → 400
  `update_required`; sklep pojedynczy bez `postalCode` → jak dziś; `FulfillmentStoreId` zapisany; płatność z
  `StoreId` witryny; księga i faktura sieci); kompletacja i zamienniki z katalogu witryny; dostawa pod lokalizacją;
  widoczność (lista, szczegóły lokalizacji → witryna, produkty/kategorie/strefy/sloty lokalizacji → 404);
  uprawnienia (pracownik lokalizacji: swoje zamówienia tak, cudze nie, oferta nie; pracownik witryny: odczyt
  wszystkich lokalizacji tak, oferta tak; obcy sklep nie); migracje addytywne + backfill; projekcja po resync.
- Panel: build + test ręczny drzewa i zamówień z kolumną „Realizuje".
- Aplikacja: lista bez lokalizacji, checkout z kodem → strefy z `delivery-options`, brak nazwy lokalizacji w UI,
  stary QR lokalizacji otwiera witrynę.
- E2E lokalnie na danych Rapacza (witryna + 4 lokalizacje, strefy bez nakładania).

## 8. Kolejność i ryzyka

1. Serwer: model + migracje + zdarzenia/projekcja + router + `delivery-options` + checkout + uprawnienia + ukrycie
   (testy). 2. Panel. 3. Plan E3-4 zaktualizowany → przegląd → aplikacja. 4. `seed-rapacz.mjs` na model sieci.
5. Kopia bazy + próba na kopii + resync projekcji → wdrożenie za zgodą.

Ryzyka: wyciek danych lokalizacji przez nieobjęty endpoint (lista kontrolna z pkt 4 w testach); stare APK u klientów
(komunikat aktualizacji); projekcja bez resync (router bez współrzędnych/rodzica) — krok wdrożenia; nakładające się
strefy (ostrzeżenie w panelu, deterministyczny priorytet).
