# Analiza: dwa plany rozliczeń, restauracje, dostawa wg zasięgu/dystansu

Cel: rozszerzyć ZipZap poza sklepy (restauracje, pizzerie…) i wprowadzić **dwa
plany współpracy**, z opłatą za dostawę i zasięgiem ustawianymi per merchant, z
opcjonalnym kosztem zależnym od dystansu. **To analiza — nic jeszcze nie koduję.**

## 1. Co zmieniamy (intencja)
- **Plan A — dostawa ZipZap:** nasz kurier dostarcza. Klient płaci **stałą opłatę
  za dostawę (np. 25 zł)** → trafia do ZipZap. **Prowizji nie pobieramy.**
- **Plan B — dostawa merchanta:** restauracja/sklep ma własnego kuriera. ZipZap
  pobiera **tylko prowizję** od wartości koszyka. Dostawą ZipZap się nie zajmuje.
- **Opłata za dostawę i zasięg** ustawiane per merchant; koszt dostawy **może
  zależeć od dystansu**.
- **Typ merchanta:** Sklep / Restauracja / Pizzeria / Inne (kategoria + branding).

## 2. Stan obecny (jak jest dziś)
- `Store`: `CommissionRate`, `MinimumOrderValue`, status, adres/miasto. Brak typu
  merchanta, planu, konfiguracji dostawy, współrzędnych.
- **Opłata za dostawę** = `DeliveryZone.DeliveryFee` (stała per strefa, dopasowanie
  po kodach pocztowych). Wybierana na checkoucie.
- **Wycena** (`Order.Place`): `Total = Subtotal + DeliveryFee`,
  `CommissionAmount = Subtotal × commissionRate`. Dostawa **jest już osobną pozycją**.
- **Prowizja** księgowana po dostarczeniu (`CommissionLedger`), i to ona jest dziś
  jedynym śledzonym **przychodem ZipZap**. Opłata za dostawę nie ma osobnego
  „właściciela" w modelu (dziś ZipZap i tak prowadzi dostawę).
- **Dostawa** = pula kierowców ZipZap (moduł Delivery), zlecenie tworzone przy
  „Gotowe do odbioru".

## 3. Model docelowy (jak ma być)
### 3.1 Merchant
- `MerchantType` (enum): `Store`, `Restaurant`, `Pizzeria`, `Other` — kategoria.
- `DeliveryModel` / plan (enum): `PlatformDelivery` (A) | `MerchantDelivery` (B).
- Konfiguracja dostawy: `DeliveryFeeFlat` (kwota), later `DeliveryBaseFee` + `PerKm`
  (dystans), `MaxRangeKm` lub lista kodów pocztowych (zasięg), opcjonalnie
  `Latitude`/`Longitude` (dla dystansu).

### 3.2 Kto ustala opłatę za dostawę
- **Plan A:** stawkę ustala **platforma** (np. 25 zł, docelowo też wg dystansu).
  Przychód z dostawy → ZipZap. Prowizja = 0.
- **Plan B:** stawkę i zasięg ustala **merchant**. Opłata → merchant. Przychód
  ZipZap = prowizja (jak dziś).

### 3.3 Przepływ dostawy
- **Plan A:** istniejący moduł Delivery (kierowca: przyjmij → odbierz → dostarcz).
- **Plan B:** **bez kroku kierowcy ZipZap** — merchant realizuje dostawę własnym
  kurierem i sam oznacza „dostarczone". Cykl: Placed → Confirmed → Picking →
  Ready → Delivered (przez merchanta), bez puli kierowców.

### 3.4 Rozliczenia (przychód platformy)
Dziś ledger śledzi tylko prowizję. Aby raport przychodu był poprawny w obu planach,
uogólniamy `CommissionLedger` → **`PlatformRevenueLedger`** z typem wpisu:
`Commission` (Plan B) | `PlatformDeliveryFee` (Plan A). Sekcja „Rozliczenia" pokaże
przychód wg typu.

## 4. Wpływ na moduły
| Moduł | Zmiana |
|---|---|
| **Catalog.Store** | + `MerchantType`, + `DeliveryModel`, + konfiguracja dostawy (+ współrzędne w fazie dystansu). Rozszerzenie `StoreRegistered`/`StoreUpdated`. Migracja. |
| **Ordering (checkout)** | Źródło opłaty i prowizji zależne od planu: Plan A → opłata platformowa, prowizja 0; Plan B → opłata merchanta, prowizja jak dziś. Read-model `CatalogStoreView` musi nieść plan + konfigurację. Zasięg walidowany na checkoucie. |
| **Delivery** | Zaangażowany tylko dla Planu A. Dla Planu B pomijamy tworzenie zlecenia; merchant sam prowadzi cykl do „Delivered". |
| **Payments** | Uogólniony ledger przychodu; Plan A księguje przychód z dostawy, prowizja 0; Plan B jak dziś. |
| **Panel admina** | Formularz sklepu: typ, plan, konfiguracja dostawy, zasięg. „Rozliczenia": przychód wg typu. |
| **Aplikacja (Flutter)** | Kategoria/typ merchanta i filtr; opłata za dostawę już pokazywana osobno (zgodne). Dystans → podgląd opłaty po podaniu adresu. |

## 5. Dystans — największa nowa zależność
- Wymaga **współrzędnych sklepu** i **adresu klienta → współrzędne** (geokodowanie).
- Geokoder = zewnętrzny provider (Google Geocoding / OSM Nominatim) → **creds,
  limity, koszt**. Zgodnie z zasadami projektu: **abstrakcja + mock + udokumentowany
  krok konfiguracyjny; nie zgadujemy API, nie commitujemy sekretów.**
- Dystans: Haversine (w linii prostej) prosty; realny dystans drogowy = API routingu
  (cięższe).
- **Rekomendacja: fazowo.** MVP = **opłata stała per merchant + zasięg po kodach
  pocztowych** (reużywa dzisiejsze strefy). Dystans + promień = **faza 2** za
  abstrakcją `IGeocoder` (mock zwraca brak/stałą), gotowe pod realny provider.

## 6. Zgodność z zasadami
- „Opłata za dostawę osobno od koszyka" — **zachowana**.
- „Prowizja od wartości koszyka" — teraz **warunkowa** (0 w Planie A).
- Moduły rozmawiają zdarzeniami — rozszerzamy zdarzenia, bez czytania cudzych schematów.
- Integracje zewnętrzne (geokoder) — abstrakcja + mock + config.
- Zgodność wsteczna: istniejące sklepy → domyślny plan (rekomendacja: **Plan B**,
  bo mają już stawki prowizji i realizują dostawę przez pulę).

## 7. Otwarte decyzje (do potwierdzenia)
1. **Plan A — opłata:** platforma ustala (domyślnie 25 zł, dystans później); merchant
   NIE ustala. Plan B — merchant ustala opłatę + zasięg. OK?
2. **Dystans teraz czy faza 2?** (Wymaga geokodera + creds.) Rekomendacja: faza 2.
3. **Zasięg:** kody pocztowe teraz (są), promień km w fazie dystansu. OK?
4. **Plan B bez kierowcy ZipZap** (merchant oznacza dostarczenie) — potwierdzasz?
5. **Ledger:** uogólnić „prowizja" → „przychód platformy" (prowizja | dostawa). OK?
6. **Typy merchantów:** Sklep / Restauracja / Pizzeria / Inne — wystarczy jako
   kategoria, czy ma wpływać na coś więcej (np. „menu" zamiast „produkty")?

## 8. Proponowany plan wdrożenia (fazy)
- **H1 — model i plany (bez geolokalizacji):** typ + plan + opłata stała + zasięg
  (kody). Checkout respektuje plan (prowizja 0 dla A). Payments: uogólniony ledger.
  Delivery: Plan B pomija pulę kierowców. Panel: wybór planu/typu/konfiguracji.
  Testy + weryfikacja na żywo.
- **H2 — geolokalizacja i dystans:** `IGeocoder` (mock + dokumentowany provider),
  współrzędne sklepu, dystans (Haversine), cennik `base + perKm`, promień zasięgu,
  podgląd opłaty w aplikacji.
- **H3 — UX klienta:** kategorie/typy w aplikacji, filtry, ewentualnie „menu" dla
  restauracji.

## 9. Ryzyka
- Zmiana semantyki przychodu (dostawa vs prowizja) — ostrożna migracja ledgera/raportów.
- Geokodowanie = zależność zewnętrzna (creds, limity, koszt).
- Przepływ Planu B bez kierowcy ZipZap — zmiany w maszynie stanów/uprawnieniach
  (merchant oznacza pickup/delivered).
- Domyślny plan dla istniejących sklepów — trzeba ustalić (patrz decyzje niżej).

## 10. Decyzje właściciela (ustalone) — CEL PROJEKTOWY, wdrożenie ODŁOŻONE
> **Status: NIE budujemy teraz.** Zapisane jako cel do przyszłej fazy (H). Bieżący
> build MVP (Faza F2/G) kontynuujemy bez tych zmian.

- **Opłatę za dostawę ustala MERCHANT w obu planach.**
- **Plan „dostawa ZipZap" (Plan A):** kurier ZipZap dostarcza. **ZipZap pobiera stałe
  25 zł od dostawy**; nadwyżkę ponad 25 zł zatrzymuje merchant (np. ustawia 30 zł →
  5 zł dla merchanta). **Brak prowizji od koszyka.**
- **Plan „dostawa merchanta" (Plan B):** własny kurier merchanta. **ZipZap pobiera
  tylko prowizję** od koszyka; merchant zatrzymuje opłatę za dostawę.
- **Status dostawy oznacza ten, kto dostarcza:** w planie A — kurier ZipZap; w planie
  B — merchant (jego kurier).
- **Domyślny plan dla istniejących sklepów: „dostawa ZipZap" (Plan A).**

### Model czasowy dostaw (ważne — „nie startujemy jak Glovo")
- **Plan A (dostawa ZipZap): fale o ustalonych godzinach, NIE on‑demand.** Kurier ZipZap
  jeździ do lokalnych sklepów o **stałych porach** (np. 10:00 i 16:00), **ustawianych
  w panelu admina**; zamówienia są **grupowane** i wiezione w najbliższym oknie.
  → Naturalnie mapuje się na istniejące **sloty czasowe** w Ordering: okna dostaw = sloty
  wybieralne dla Planu A.
- **Plan B (dostawa merchanta): to sklep wybiera strategię** — o konkretnej godzinie
  albo na bieżąco (gdy tylko wpadnie zamówienie).
- **Klient musi jasno widzieć sposób i harmonogram dostawy** sklepu — na karcie sklepu
  i w checkoucie (np. „Dostawa ZipZap: dziś 16:00" vs „Dostawa sklepu: na bieżąco").
  To wymóg **P0 UX** (bez tego klient nie wie, kiedy dostanie zamówienie).

### Do doprecyzowania przed wdrożeniem
1. **Numeracja planów:** w odpowiedzi padło „ZipZap ma stałe 25 zł od dostawy w
   planie drugim", a jednocześnie „Plan A = dostawa ZipZap". Mechanika 25 zł + nadwyżka
   dla merchanta opisuje **plan, w którym dostarcza ZipZap** — potwierdzić, że tak
   (niezależnie od numeru).
2. **Opłata < 25 zł w planie ZipZap:** co, gdy merchant ustawi dostawę poniżej 25 zł
   (minimalna 25 zł? dopłata merchanta?).
3. **Typy merchantów:** czy Sklep/Restauracja/Pizzeria wpływają na coś poza kategorią
   (np. „menu" zamiast „produkty").

### Konsekwencja dla modelu przychodu
Ledger przychodu platformy musi rozróżniać dwa źródła: **stała opłata za dostawę
(25 zł, Plan A)** oraz **prowizja (Plan B)** — uogólnienie `CommissionLedger` →
`PlatformRevenueLedger { type: Commission | PlatformDeliveryFee }`.

## 11. Następny krok
Kontynuujemy bieżące MVP (F2/G). Powyższe realizujemy w **Fazie H** po jej
zaplanowaniu — najpierw model+plany (H1, bez geolokalizacji), potem dystans (H2).
