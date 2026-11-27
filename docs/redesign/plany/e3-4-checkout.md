# Plan E3-4 — Checkout w 3 krokach (Adres → Termin → Podsumowanie)

Status: **zatwierdzony przez Codexa** (10 rund, 2026-11-27; rundy 6–10 po dodaniu wymagania właściciela „lejek bez
przewijania", pkt 3a). Przebieg: `raporty/e3-4-plan-codex.md`. Przed kodowaniem. Zadanie oznaczone w planie jako **wysokie ryzyko — osobny
przegląd Codexa**. Tylko aplikacja (`mobile/`); serwer, panel i baza bez zmian.

## 1. Cel

Dziś `features/checkout/checkout_screen.dart` (610 linii) to jeden długi ekran: runda, adres, telefon, strefa
(lista rozwijana), termin, dokumenty, zgoda, podsumowanie — za dużo decyzji naraz (audyt P2-8). Dzielimy go na
3 kroki z paskiem postępu. **Kształt i wartości żądania zamówienia, odczyty i walidacje serwera — bez zmian.** Klient
dostaje dodatkowo ostrzejsze bramki (pkt 2.10, 2.11): nie wyśle zamówienia z nieaktualnym lub niedostępnym wyborem
i nie pozwoli zmienić danych po wysyłce o niepewnym wyniku. Innych różnic w zachowaniu nie ma (układ bez przewijania z pkt 3a
zmienia tylko prezentację).

## 2. Niezmienniki (blokujące — lista kontrolna Etapu 3 w `03-weryfikacja-codex.md`)

1. **To samo żądanie zamówienia**: `POST /ordering/carts/{cartId}/checkout`, nagłówek `Idempotency-Key`, ciało
   `{token, deliveryZoneId, timeSlotId, deliveryAddress, contactPhone, consentAccepted, expectedRoundStartsAtUtc?}` —
   te same pola, te same źródła wartości, `trim()` adresu i telefonu jak dziś. `OrderingRepository.checkout` **bez zmian**.
2. **Ten sam klucz idempotencji** na całe wejście na ekran (generowany raz w `initState`), także przy przechodzeniu
   między krokami i ponowieniu po błędzie.
3. **Te same odczyty i ten sam moment**: strefy, sloty (po wybraniu strefy), dokumenty sklepu, podgląd rundy,
   konfiguracja publiczna — te same providery (`zonesProvider`, `slotsProvider`, `storeLegalProvider`,
   `roundPreviewProvider`, `publicConfigProvider`), bez nowych endpointów. Providery są `autoDispose`, więc **ekran
   (nadrzędny `build`) obserwuje je wszystkie niezależnie od widocznego kroku** — runda i dokumenty od załadowania
   stref, sloty od wybrania strefy, jak dziś; ukrycie kroku nie zwalnia ich i nie zmienia czasu ani liczby żądań.
   `expectedRoundStartsAtUtc` odczytywane w chwili wysyłki z `roundPreviewProvider` (jak dziś), nie zapamiętywane
   w kroku 2.
4. **Te same warunki złożenia** (`_missingReason`) w tej samej kolejności i z tymi samymi komunikatami, **plus**
   bramka aktualności z pkt 2.10 (wstawiona tam, gdzie dziś „Wybierz termin dostawy"): tester
   w pilotażu, runda (ładowanie / błąd / niedostępna), adres, telefon, strefa, termin, termin przed rundą
   (`RoundPreview.allowsSlot`), zgoda gdy `legal.requiresAcceptance`. Krok blokuje „Dalej" tylko warunkami swojego
   kroku; przycisk „Złóż zamówienie" — wszystkimi (jak dziś).
5. **Zgoda**: checkbox tylko w kroku 3, domyślnie odznaczony; bez zgody (gdy wymagana) zamówienia nie da się złożyć
   (blokuje: „zamówienie możliwe bez wymaganej zgody").
6. **Osobna linia opłaty za dostawę** i **informacja o terminie dostawy** w podsumowaniu (blokujące w liście Codexa).
   Tryb testowy: „Wartość zamówienia" + „Opłata: bez opłaty — zamówienie testowe" jak dziś.
7. **Po sukcesie**: `clearAfterCheckout()` i przejście wg `order.isTestOrder` (`/orders/{id}` albo `/pay/{id}`) —
   decyduje odpowiedź serwera, nie lokalna konfiguracja.
8. **409**: serwer zwraca każdy konflikt z tym samym kodem (`Error.Conflict` — termin graniczny rundy, limit
   zamówień testowych, koszyk już złożony, rezerwacja terminu przy dużym ruchu; `OrderingService.cs` 228/271/295/340),
   więc klient **nie zgaduje przyczyny ze statusu**: zostaje w kroku 3, pokazuje komunikat serwera i — jak dziś —
   odświeża rundę i sloty. Jeśli po odświeżeniu wybrany termin zniknął albo jest przed rundą, wybór jest czyszczony
   (pkt 10), a w podsumowaniu termin pokazuje „Wybierz termin ponownie" z przyciskiem przejścia do kroku 2.
10. **Aktualność wyboru** (świadomie ostrzejsza bramka po stronie klienta — serwer i tak odrzuca): termin liczy się
   jako wybrany tylko, gdy lista slotów wybranej strefy jest **załadowana i nie jest w trakcie odświeżania**
   (`hasValue && !isLoading && !isRefreshing && !hasError` — w Riverpod 2.6 stan błędu po nieudanym odświeżeniu zachowuje poprzednią wartość, więc sam `hasValue` nie wystarcza), id jest na tej liście, slot ma wolne miejsca
   (`remainingCapacity > 0`, jak blokada karty dziś) i `allowsSlot`. Podczas odświeżania „Dalej"/„Złóż" są
   zablokowane z komunikatem „Sprawdzamy dostępne terminy…" (stary wynik providera nie odblokowuje przejścia). Po
   odświeżeniu: brak id albo brak miejsc → `_slotId = null` + komunikat „Wybrany termin nie jest już dostępny —
   wybierz inny".
   Tak samo strefa: brak id na odświeżonej liście → czyszczenie strefy i terminu.
   **Tak samo runda**: podgląd rundy liczy się tylko, gdy `roundPreviewProvider` jest załadowany, nie jest w trakcie
   ładowania/odświeżania (np. po unieważnieniu po 409) i nie ma błędu (`!hasError`) — do tego czasu „Dalej" w kroku 2 i „Złóż" są zablokowane
   z komunikatem „Sprawdzamy najbliższą rundę zakupową…" (ten sam tekst co dziś przy ładowaniu); `_submit` przerywa,
   jeśli runda lub sloty są odświeżane albo w stanie błędu, więc `expectedRoundStartsAtUtc` nigdy nie pochodzi ze starego podglądu. Dziś nieaktualne id slotu
   przechodziło bramkę (`_missingReason` sprawdza `allowsSlot` tylko dla znalezionego slotu).
11. **Wysyłka bez duplikatów**: `_submit` zaczyna się od `if (_submitting) return;` (nie tylko wyłączony przycisk).
   **Niepewny wynik** = brak odpowiedzi (`ApiException.code == 'network'`, brak statusu), przekroczony czas **albo
   odpowiedź 5xx** — serwer zatwierdza transakcję przed zwróceniem zamówienia (`OrderingService.cs` 323), a endpoint
   potem jeszcze zapisuje zgodę w audycie (`OrderingEndpoints.cs` 120), więc 500 może przyjść po utworzeniu
   zamówienia. W takim przypadku serwer mógł już przyjąć zamówienie — ponowienie z tym samym kluczem zwraca zamówienie z *pierwszymi* danymi. Dlatego
   po niepewnym wyniku ekran zapamiętuje dokładne argumenty tej próby, blokuje edycję pól i wyborów i proponuje
   „Ponów wysyłanie (te same dane)" (ten sam klucz, te same argumenty) oraz „Sprawdź w Zamówieniach". Dziś klient
   mógł zmienić dane i wysłać ponownie z tym samym kluczem — dostawał po cichu zamówienie ze starymi danymi.
   Edycja jest odblokowana tylko po odpowiedzi **4xx** (walidacja, brak uprawnień, konflikt — odrzucenie przed
   utworzeniem zamówienia; ponowienie z tym samym kluczem i tak zwraca istniejące zamówienie, bo serwer sprawdza klucz
   idempotencji najpierw — `OrderingService.cs` ok. 215).
9. Zmiana strefy czyści wybrany termin (jak dziś). Zgoda i pola formularza są zachowane przy przechodzeniu wstecz.

## 3. Kroki

| Krok | Treść | „Dalej" aktywne, gdy |
|---|---|---|
| 1. Adres | adres dostawy, telefon kontaktowy (pola z widoczną etykietą, nie tylko podpowiedź), strefa dostawy jako lista kart wyboru (nazwa + opłata) zamiast listy rozwijanej | nie tester-zablokowany, adres, telefon, strefa |
| 2. Termin | ramka rundy (zakupy w sklepie, termin graniczny, najwcześniejsza dostawa, „czas polski") + wybór slotu (karty, jak dziś, z oznaczeniem „za wcześnie") | runda dostępna, slot wybrany i `allowsSlot` |
| 3. Podsumowanie | produkty (liczba, wartość), adres + telefon + strefa + termin z przyciskami „Zmień" (skok do kroku), dokumenty sklepu + zgoda, podsumowanie kwot, przycisk złożenia, komunikat o płatności / trybie testowym | wszystkie warunki z pkt 2.4 |

- Pasek postępu „Krok 2 z 3 · Termin" (`ZzStepProgress`), z etykietą dla czytnika ekranu.
- Wstecz: przycisk „Wstecz" w krokach 2–3 oraz systemowe „wstecz" (`PopScope`) cofa o krok, w kroku 1 wychodzi
  z ekranu (do koszyka). Stan pól zachowany między krokami (jeden `State`, kroki to widoki, nie osobne trasy —
  odświeżenie strony zaczyna od kroku 1, jak dziś zaczynał się formularz).
- Przejście między krokami: przenikanie ≤ 250 ms, przy „ogranicz ruch" 0 ms.
- Szerokość: `ZzPage` 640 px jak dziś.

## 3a. Lejek bez przewijania (wymaganie właściciela 2026-11-27)

„Klient nie musi przewijać, żeby kliknąć przycisk złożenia zamówienia albo wypełnić formularz." Rozumienie
**zaakceptowane przez właściciela 2026-11-27** (wraz z wyjątkiem dla powiększonego tekstu i otwartej klawiatury,
poniżej): **klient nigdy nie musi przewijać ręcznie, żeby dotrzeć do przycisku akcji ani do
kolejnego pola**. Obowiązuje checkout (blokujące dla E3-4); w sklepie i koszyku — widoczność głównej akcji (listy
produktów i tak się przegląda).

- **Ekran odniesienia: 360 × 600 px CSS** (najmniejszy popularny Android z paskami Chrome; telefon właściciela
  360 × 691). Tekst 100 %.
- **Przycisk akcji przyklejony do dołu** w każdym kroku („Dalej"; w kroku 3 „Złóż zamówienie"; „Wstecz" obok),
  poza przewijaną treścią; przy otwartej klawiaturze stoi nad nią (`resizeToAvoidBottomInset`) — w widocznym
  obszarze, nie tylko w granicach okna.
- **Klawiatura schowana: cała treść kroku nad przyciskiem, `maxScrollExtent == 0`.** Klawiatura otwarta: aktywne
  pole odsłania się samo (fokus → `ensureVisible`, przejście „Dalej" na klawiaturze do następnego pola), bez ręcznego
  przewijania. Tekst 130 %: treść może się przewijać, przycisk akcji zawsze widoczny.
- Układ kroków — **wszystkie opcje widoczne bez żadnego gestu** (bez arkuszy, bez przewijania w bok). Budżet
  wysokości przy 360 × 600: pasek aplikacji 56 + pasek postępu 40 + pasek akcji 76 → ok. 428 px na treść kroku.
  - Krok 1: adres i telefon jako pola z etykietą wewnątrz (pływająca etykieta, wysokość 56 px — etykieta wliczona),
    strefa: **każda aktywna strefa jako wiersz wyboru 48 px** (nazwa + opłata), **bez automatycznego wyboru** —
    klient dotyka strefy sam, jak dziś (aplikacja nie zna kodów stref, a serwer nie sprawdza zgodności kodu z adresem).
    Rachunek: odstęp górny 16 + adres 56 + 12 + telefon 56 + 16 + etykieta „Strefa dostawy" 20 + 8 + 4 × 48 + 3 × 4
    = 388 px < 428 px → **mieszczą się 4 strefy** (5 strefa: 440 px > 428). Pilot Rapacza ma 1 strefę na lokalizację.
    **Tryb testowy pilotażu:** pełny `TestOrderBanner` nie stoi w kroku 1 — w pasku postępu (te same 40 px) pojawia
    się etykieta „Zamówienie testowe" (z pełną informacją dla czytnika), a pełny opis trybu jest w kroku 3 obok
    linii „Opłata: bez opłaty — zamówienie testowe". Tester **zablokowany** (tryb testowy bez roli Tester/Admin):
    zamiast formularza kroku 1 pełny baner blokady z wyjaśnieniem, „Dalej" nieaktywne (formularza i tak nie da się
    złożyć — bramka z pkt 2.4).
    Sklep z ponad 4 strefami — poza zakresem pilota: znane ograniczenie zapisane w raporcie (wtedy lista stref się
    przewija; do decyzji przy pierwszym takim sklepie). 0 stref → stan pusty „Sklep nie ma jeszcze stref dostaw".
  - Krok 2: runda jedną linią („Zakupy dziś 16:00 · zamów do 15:30"), **dni w siatce** (do 8 dni w 2 rzędach po 4,
    chip ok. 76 × 48 px), **godziny wybranego dnia w siatce po 3 w rzędzie** (do 12 godzin w 4 rzędach). Treść kroku:
    40 + 104 + 4 × 52 ≈ 352 px < 428 px. Więcej niż 8 dni / 12 godzin — poza zakresem pilota (Rapacz: 2 terminy
    dziennie), znane ograniczenie.
  - Krok 3: zwinięte podsumowanie: „3 produkty · 26,37 zł" (pełna lista w arkuszu tylko do podglądu — nic do
    wyboru), adres + telefon jedną linią, strefa i termin jedną linią (każde z „Zmień" → skok do kroku), opłata za
    dostawę osobną linią, suma, zgoda (checkbox + linki w jednym akapicie), długie teksty skracane wielokropkiem
    z pełnym tekstem w etykiecie dla czytnika.
- `maxScrollExtent == 0` dotyczy **głównego pionowego przewijania kroku**; w kroku nie ma innych przewijanych
  obszarów.
- Testy przy 360 × 600 dla każdego kroku i najgorszych przypadków: puste pola, 0 stref (brak przejścia dalej i brak wywołania `checkout`), 1 / 4 strefy (żądanie
  zawiera id strefy wybranej dotknięciem), tryb testowy z 4 strefami (etykieta w pasku postępu, krok mieści się),
  tester zablokowany (baner zamiast formularza, „Dalej" nieaktywne), 8 dni × 12 godzin (wybór ostatniego dnia i ostatniej godziny bez przewijania),
  wymagana zgoda + 3 dokumenty, długie nazwy sklepu/adresu, tryb testowy (dłuższe podsumowanie): przycisk w całości
  w oknie, dół ostatniego elementu ≤ góra paska akcji, `maxScrollExtent == 0`; z klawiaturą (symulowany
  `viewInsets.bottom` 260 px) — przycisk w widocznym obszarze nad klawiaturą, aktywne pole widoczne; 130 % —
  przycisk widoczny, a każde pole osiągalne przejściem „Dalej" na klawiaturze (fokus kolejnego pola odsłania je bez
ręcznego przewijania) — test przechodzi przez wszystkie pola kroku 1 przy 130 %. Sklep i koszyk: przycisk główny
widoczny przy 360 × 600.

## 4. Komponenty (lista E2-3 → E3-4)

`_SlotCard` → `ZzSlotCard`, `_RoundCard` → `ZzRoundNotice` (na `ZzNotice`), `_SummaryRow` → `ZzSummaryRow` (z E3-3),
`_ZoneDropdown` → `ZzChoiceCard` (karta wyboru z radiem, 48 px), `_SlotPicker` → kompozycja w ekranie na
`ZzSlotCard`, `_LegalLinks` → `ZzLinkList`, `_Label` → `ZzFieldLabel`. Po E3-4 w `features/checkout` zostaje tylko
stan ekranu. Nowe: `ZzStepProgress`.

## 5. Testy i dowody

1. **Test żądania** (`checkout_flow_test.dart`): podstawiony `OrderingRepository` zapisuje argumenty `checkout(...)`;
   przejście 3 kroków → asercja dokładnych wartości (adres i telefon po `trim`, id strefy i slotu, `consentAccepted`,
   `expectedRoundStartsAtUtc` = `round.startsAtUtc`, klucz idempotencji niezmienny przy ponowieniu).
2. Bramki kroków: każdy brak z pkt 2.4 blokuje właściwy krok z tym samym komunikatem; tester w pilotażu; zgoda.
3. 409 → zostaje krok 3, komunikat serwera, odświeżenie rundy/slotów; termin zniknął po odświeżeniu → wybór
   wyczyszczony + „Wybierz termin ponownie"; strefa zniknęła → strefa i termin wyczyszczone.
3a. Podwójne szybkie dotknięcie „Złóż" → jedno wywołanie `checkout`. Brak sieci, przekroczony czas i 500 (niepewny
   wynik) → pola i wybory zablokowane, „Ponów wysyłanie" wysyła identyczne argumenty i ten sam klucz; 400/403/409 →
   edycja możliwa.
3b. Bramka aktualności: id spoza odświeżonej listy, slot pełny po odświeżeniu (`remainingCapacity` 0), lista
   w trakcie odświeżania (stary wynik) — każde blokuje „Dalej" i „Złóż"; po zakończeniu odświeżania wybór
   wyczyszczony z komunikatem. Runda w trakcie odświeżania po 409 (stary podgląd dostępny jako poprzednia wartość)
   blokuje „Złóż" i `_submit` nie woła `checkout`; po nowym podglądzie `expectedRoundStartsAtUtc` = nowa wartość.
   Odświeżenie slotów albo rundy kończy się błędem przy zachowanej poprzedniej wartości → „Dalej" i „Złóż"
   zablokowane, widoczny błąd z „Spróbuj ponownie", `checkout` niewywołany; po udanym ponowieniu odblokowane.
4. Tryb testowy: tytuł, linie podsumowania, przejście do `/orders/{id}`; tryb zwykły → `/pay/{id}`.
5. Wstecz/PopScope, zachowanie stanu, zmiana strefy czyści termin.
6. 360 px / 130 % tekstu bez przepełnień, pola ≥ 48 px, „ogranicz ruch" = 0 ms.
7. Istniejące `order_window_test`, `purchasing_round_test`, `test_order_mode_test`, `time_slot_label_test` — bez
   zmian i zielone.
8. **Porównanie żądań sieciowych przed/po** (kryterium planu): lokalne API; **przed każdym przebiegiem baza jest
   tworzona od zera i seedowana identycznie** (usunięcie bazy tymczasowej, start API z migracjami, ten sam seed, te
   same strefy/sloty), oba przebiegi w tym samym oknie tej samej rundy (przed tym samym terminem granicznym), więc
   pierwszy przebieg nie zmienia stanu drugiego; ta sama ścieżka (seed → koszyk → checkout) w kompilacji `f3aeab4`
   (przed) i nowej (po); zapis żądania `checkout` przez CDP
   (metoda, ścieżka, nagłówki bez wartości tokenów, ciało) → diff po normalizacji (id koszyka, token, klucz
   idempotencji). **`expectedRoundStartsAtUtc` porównywane dosłownie**, nie normalizowane: oba przebiegi na tej samej
   bazie i w tym samym oknie rundy (przed tym samym terminem granicznym), a wartość musi być równa `round.startsAtUtc`
   z odpowiedzi podglądu rundy zapisanej w tym samym śladzie. Oczekiwanie: identyczny kształt i wartości pól.
9. Zrzuty 375 i 1280 px każdego kroku + stany: brak stref, brak slotów, błąd i ponowienie dla stref / slotów /
   rundy / dokumentów, brak sieci (pasek offline + błąd z ponowieniem), niepewny wynik wysyłki.
10. Testy widżetów tych stanów: pusty (brak stref, brak slotów), błąd + ponowienie dla każdego z 4 odczytów, powrót
   wstecz zachowuje pola, strefę, termin i zgodę.
11. **Przejście ręczne end-to-end** na lokalnym API (lista kontrolna Etapu 3): wejście z kodu QR → sklep → koszyk →
   logowanie (konto testowe w tej lokalnej bazie) → checkout 3 kroki → zamówienie testowe (tryb testowy z lokalnej
   konfiguracji, bez operatora płatności) → śledzenie; zrzuty każdego kroku w raporcie.

### Bezpieczeństwo dowodów

Ślady CDP i zrzuty tylko z **danych syntetycznych** (fikcyjny adres i telefon, konto testowe lokalnej bazy). Przed
zapisaniem w repozytorium redagowane: token koszyka (`token` w ciele), nagłówek `Authorization`, wartość
`Idempotency-Key`, adres, telefon (zastąpione znacznikami, porównanie kształtu i pozostałych wartości). Zrzuty
sprawdzone pod kątem danych osobowych. Żadnych połączeń z produkcją ani zewnętrznym operatorem płatności.

## 6. Ryzyka i odstępstwa (jawnie)

- Pkt 2.10 i 2.11 to **ostrzejsze** zachowanie klienta niż dziś (nieaktualny termin, zablokowana edycja po niepewnym
  wyniku) — kształt i wartości żądania bez zmian, serwer bez zmian.

- Kroki jako widoki w jednym `State` — świadomie bez tras `/checkout/1..3`, żeby odświeżenie nie zostawiało klienta
  w kroku 3 bez danych formularza.
- Dłuższa ścieżka (3 ekrany zamiast 1) — kompensujemy podsumowaniem z „Zmień" i tym, że krok 1 pamięta wybór strefy.
- Brak zmian serwera = brak migracji; push na `main` sprawdzany pod kątem `backend/` (wdrożenie automatyczne).
