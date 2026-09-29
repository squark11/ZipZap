# Pilotaż — model biznesowy sprzedaży (do decyzji właściciela)

> **Status: dokument decyzyjny, NIE porada prawna ani podatkowa.** Zebrane tu warianty,
> konsekwencje i pytania służą do rozmowy z **księgowym/doradcą podatkowym i prawnikiem**
> przed włączeniem realnych płatności, fakturowania i fiskalizacji. Do czasu tej decyzji
> pilotaż działa w **trybie testowym bez realnego obciążania klienta**.
>
> Uwaga terminologiczna — **dwie różne osie**, nie mylić:
> - **Model sprzedaży (ten dokument): A = marketplace/pośrednictwo vs B = odsprzedaż przez operatora.** Kto jest *sprzedawcą towaru*.
> - **Plan dostawy (osobny, [ANALYSIS_TWO_PLANS.md](ANALYSIS_TWO_PLANS.md)): „dostawa ZipZap" vs „dostawa merchanta".** Kto *dowozi*.
> Te osie są niezależne. Poniżej rozstrzygamy tylko oś sprzedaży.

## 0. Decyzje właściciela (2026-09-29) i sprawy otwarte

**Decyzje biznesowe właściciela (obowiązujące założenia projektu):**
1. **Sprzedawcą towaru jest sklep** (wariant A — marketplace, §2).
2. **Każdy sklep korzysta z własnego konta Przelewy24**; płatność klienta trafia **bezpośrednio do sklepu**.
3. **Platforma nie przyjmuje pieniędzy klientów** na zwykłe konto i **nie buduje własnego harmonogramu wypłat**
   do sklepów.
4. Dowózka.pl **osobno rozlicza ze sklepem** prowizję i uzgodnioną usługę dostawy (technicznie istnieje już
   miesięczne zestawienie/faktura dla sklepu w panelu; forma dokumentu — do potwierdzenia z księgowym).
5. Na początku **operator robi zakupy w dwóch zaplanowanych rundach dziennie** (§1); **docelowo zamówienia
   kompletuje sklep**.

**Do potwierdzenia przez księgowego / prawnika — NIE są rozstrzygnięte prawnie ani podatkowo:**
- **paragon / dokument za usługę dostawy** — kto go wystawia, na kogo i kiedy (szczególnie gdy klient płaci
  całość przez P24 sklepu);
- **dokumentowanie zamówień przy zakupie zbiorczym** — jak sklep dokumentuje sprzedaż osobno dla każdego
  klienta, gdy operator kupuje pozycje wielu zamówień w jednej wizycie w rundzie (§5 pkt 1–2).

**Kierunek rekomendowany do zaprojektowania — NIE zatwierdzony do produkcji i NIE wdrożony:** klient widzi
**kwotę końcową po kompletacji** i po swojej **zgodzie na zamienniki**, a następnie płaci **przez P24 sklepu
przed dostawą**. To propozycja do projektu (UX, statusy, obsługa różnicy kwot) i do konsultacji — nie decyzja
o przepływie płatności.

**Stan techniczny (bez zmian w tym kroku):** panel pozwala zapisać dane P24 sklepu, ale **adaptera Przelewy24
nie ma** — żadna realna płatność nie jest możliwa. Obowiązuje **W1** (§9): zamknięta grupa testerów, bez
pobierania pieniędzy.

## 1. Założenie operacyjne pilotażu (kontekst)

Operator Dowózka.pl **nie jedzie do sklepu po każdym zamówieniu**. Na start robi zakupy i
kompletuje zamówienia w **dwóch zaplanowanych rundach zakupowych dziennie: 12:00 i 16:00**
(strefa `Europe/Warsaw`, godziny konfigurowalne). Zamówienia zebrane do rundy są kupowane
**łącznie podczas jednej wizyty w sklepie**, ale **każda pozycja nadal należy do konkretnego
zamówienia i klienta**.

**Runda zakupowa ≠ okno dostawy.** Runda = kiedy operator *kupuje/kompletuje*. Okno dostawy =
kiedy klient *dostaje*. Klient musi widzieć oba oraz **nie wolno obiecywać dostawy natychmiastowej**.

### 1.1. Jak działają rundy w systemie (S1a — zaimplementowane lokalnie, niewdrożone)

- **Harmonogram jest trwały** (tabela `ordering.purchasing_schedule`, edycja: panel → Konfiguracja →
  „Rundy zakupowe”, tylko admin, zmiana audytowana). Nie zależy od pamięci procesu ani od `App_Data`.
- **Wartości startowe — DO POTWIERDZENIA ze sklepem:** rundy **12:00 i 16:00**, zamówienia przyjmowane do
  **30 min przed rundą** (cutoff), rundy **pon–sob** (niedziela wyłączona), okno dostawy może zacząć się
  najwcześniej **60 min po starcie rundy**.
- Godziny są **lokalne `Europe/Warsaw`**; konkretna runda jest zapisywana w **UTC** i liczona osobno dla
  każdej daty (zmiana czasu jest uwzględniona; godzina nieistniejąca → runda pominięta, podwójna →
  wcześniejsze wystąpienie).
- Zamówienie trafia do **najbliższej rundy, której cutoff jeszcze nie minął** (dni aktywne, sklep otwarty).
  Jeśli klient widział rundę, której cutoff minął przed potwierdzeniem, serwer **odrzuca** zamówienie (409)
  z nową godziną zamiast po cichu przenosić je do kolejnej rundy.
- Klient **przed zamówieniem** widzi rundę, cutoff i najwcześniejszy początek dostawy; terminy dostawy
  wcześniejsze niż runda + 60 min są niedostępne (również po stronie serwera). Sklep zamknięty / brak rundy →
  czytelny komunikat i brak możliwości złożenia zamówienia.
- Rundy są **niezależne** od terminów dostaw (`time_slots`) i od „fal dostaw” w ustawieniach platformy.

### 1.2. Lista zakupów i kompletacja (S1b — zaimplementowane lokalnie, niewdrożone)

- Panel → **Zakupy (rundy)**: dostęp ma **admin** i **pracownik przypisanego sklepu**; kierowca i klient — brak
  dostępu (odmowa na poziomie polityki i sklepu). Widok: bieżące/nadchodzące rundy (+3 ostatnie dni), stan rundy
  (przyjmuje zamówienia / zamknięta — czeka na zakupy / zakupy w toku / skompletowana / brak zamówień), postęp.
- **Lista zakupów** sumuje ten sam produkt (i jednostkę) z wielu zamówień, ale każda suma rozwija się na
  zamówienia i klientów — agregacja niczego nie zaciera. Klient jest widoczny jako **pseudonimowy identyfikator**
  (np. `K-3F2A91`) obok kodu zamówienia; w widoku kompletacji nie ma imienia, telefonu ani adresu (to dane
  dostawy — S1c). To **pseudonimizacja, nie anonimizacja**: w powiązaniu z zamówieniem identyfikator nadal może
  być daną osobową (RODO), więc obowiązują te same zasady dostępu i poufności co dla danych zamówienia.
- **Kompletacja pozycji:** oczekuje / kupiono (z faktyczną ilością) / niedostępne / zastąpiono (produkt
  zastępczy z katalogu sklepu + ilość + notatka). Oryginalna pozycja zamówienia **nie jest zmieniana**; stan
  kompletacji i pełna historia zmian (kto, kiedy, co) są w osobnych tabelach. Poprawki są możliwe do przekazania
  zamówienia kierowcy (później stan jest tylko do podglądu).
- **Bez nadpisywania:** każda zmiana niesie wersję, którą operator widział; nowsza zmiana innej osoby nie
  zostanie nadpisana (konflikt → odświeżenie), a podwójne kliknięcie tej samej zmiany nie tworzy duplikatu.
- **Kompletacja nie zmienia statusu zamówienia, cen, kwot ani rozliczeń** i nie wysyła zdarzeń dostawy
  (w tym `OrderDelivered`). Statusy zamówienia zmienia się jak dotąd — przyciskami w „Zamówieniach”.
- **Które zamówienia trafiają na listę zakupów:** testowe (W1) od złożenia; płatne dopiero po potwierdzeniu
  płatności. Anulowane i nieopłacone są widoczne z powodem, ale poza sumami.

### 1.3. Przygotowanie dostaw i przekazanie kierowcy (S1c — zaimplementowane lokalnie, niewdrożone)

- **Kiedy powstaje dostawa:** gdy sklep oznaczy zamówienie jako „gotowe do odbioru” (Potwierdzone → Kompletowane →
  Gotowe). Dostawa niesie okno dostawy wybrane przez klienta (data + godziny) — system go nie zmienia.
- **Panel → Dostawy** (pracownik sklepu; admin z przełącznikiem sklepu): filtr dnia i statusu, nieprzypisane dostawy
  z wyborem **aktywnego kierowcy tego sklepu**, trasa każdego kierowcy w oknie z **ręczną kolejnością** (↑/↓ + zapis),
  link „Otwórz trasę w Mapach Google” (przystanki w ustalonej kolejności), historia zmian (kto, kiedy).
  Bez automatycznej optymalizacji tras i bez płatnych API map.
- **Kierowca → Moje dostawy:** adres i telefon **wyłącznie** dla dostaw przypisanych jemu i w realizacji
  (przypisana / w drodze), po sprawdzeniu **w bazie** przy każdym żądaniu: konto aktywne + przypisanie do sklepu
  dostawy (token wydany przed dezaktywacją/odpięciem nie wystarcza). Po dostarczeniu adres znika.
  Samodzielne branie dostaw z puli jest **wyłączone** — przydziela operator (inaczej kierowca mógłby „zbierać”
  adresy wszystkich klientów sklepu).
- **Statusy:** nieprzypisana → przypisana → w drodze → dostarczona. `OrderDelivered` powstaje wyłącznie przy
  poprawnym zakończeniu przypisanej dostawy (dokładnie raz, także przy wielokrotnym kliknięciu); bezpośrednie
  oznaczanie zamówienia jako dostarczone w module zamówień jest zablokowane.
- **Administrator nie ma wyjątku** w akcjach kierowcy („odebrano”/„dostarczono” — tylko kierowca przypisany do tej
  dostawy, z aktywnym przypisaniem do sklepu). W sytuacji awaryjnej (np. telefon kierowcy nie działa) admin używa
  osobnej akcji **„Awaryjnie…”**: wymagany powód (10–300 znaków, bez danych klienta), te same reguły przejść (nie
  pomija przypisania), wpis w historii dostawy z powodem i w audycie.
- **Równoległe zmiany:** dwa przypisania tej samej dostawy → wygrywa jedno (drugie 409); kolejność trasy zapisuje się
  w całości albo wcale (wersje przystanków + blokada trasy).
- **Prywatność:** link do map budowany w przeglądarce i otwierany z `noreferrer` (adresy trafiają tylko do aplikacji
  map, nie do naszego serwera, logów ani analityki — panel nie ma analityki). Historia i audyt dostaw nie zawierają
  adresów ani telefonów.

**Decyzje do podjęcia (nie wymyślono reguł):**
1. **Zamienniki** — czy wymagają zgody klienta przed zakupem (i jak ją zbierać), jaka cena obowiązuje, kto
   pokrywa różnicę. Dziś system zapisuje zamianę + notatkę dla operatora i **nie zmienia kwot**.
2. **Częściowe ilości / braki** w trybie płatnym — korekta kwoty lub zwrot (W2/W3). W W1 bez znaczenia (bez opłat).

Docelowo produkt to **marketplace lokalnych sklepów** — każdy sklep prowadzi własny katalog i
ofertę. Pilotaż nie może przekształcić platformy w jeden centralny sklep Dowózka.pl ani przenieść
własności asortymentu na platformę.

**Decyzja właściciela (2026-09-29, §0):** sprzedawcą towaru jest **sklep**, a Dowózka.pl obsługuje
**platformę + zakupy/kompletację + dostawę**. To decyzja biznesowa — sposób jej udokumentowania (paragon
za dostawę, zakup zbiorczy w rundzie) wymaga jeszcze potwierdzenia księgowego/prawnika.

## 2. Wariant A — Marketplace / pośrednictwo

- **Sprzedawca towaru:** sklep. Sklep zarządza cenami i dostępnością.
- **Rola Dowózka.pl:** pośredniczy w zamówieniu i organizuje **zakupy/kompletację oraz dostawę**.
- **Przepływ pieniędzy (do potwierdzenia z księgowym):**
  - kwota **za towary** należna sklepowi (płatność klienta trafia docelowo do sklepu — np. przez
    jego własną bramkę; dziś w projekcie płatność jest routowana per‑sklep, ZipZap nie jest płatnikiem);
  - **prowizja** Dowózka.pl od wartości koszyka (Plan B dostawy) — przychód operatora;
  - **opłata za dostawę** (Plan A dostawy: stałe 25 zł dla ZipZap + ewentualna nadwyżka dla sklepu);
  - ewentualne **opłaty operatora** (serwisowe/subskrypcyjne).
- **Dokument sprzedaży za towary:** wystawia **sklep** — i to jest kluczowe pytanie do księgowego:
  **jak sklep dokumentuje sprzedaż osobno dla każdego klienta**, skoro operator kupuje pozycje
  wielu klientów **łącznie w jednej rundzie**. Fizyczne grupowanie zakupów **nie może zacierać**
  przypisania produktów i płatności do konkretnych zamówień.
- **Reklamacje / braki / zwroty:** wymaga jednoznacznego podziału odpowiedzialności sklep ↔ operator
  (kto odpowiada za jakość towaru, kto za błąd kompletacji/dostawy). Do ustalenia w umowie i regulaminie.
- **Przychód operatora:** **prowizja + opłaty za dostawę/serwis** — **nie** cała wartość koszyka.
- **Wymagania operacyjne:** rozliczenie sklep↔operator (raport prowizji/dostaw), regulamin i podział
  odpowiedzialności, obsługa zamienników/braków zgodnie z preferencją klienta.

## 3. Wariant B — Odsprzedaż przez operatora

- **Sprzedawca towaru:** **Dowózka.pl** kupuje towary na własny rachunek i **odsprzedaje** klientowi.
- **Konsekwencje:** operator staje się **sprzedawcą wobec klienta** i przejmuje **pełną obsługę sprzedaży**:
  dokumenty fiskalne/faktury za towary, rozliczenia podatkowe (w tym VAT/marża), zwroty, braki,
  zamienniki, **ryzyko dostępności** i **kapitał obrotowy** (operator finansuje zakup przed odsprzedażą).
- **Dokument sprzedaży za towary:** wystawia **operator** (paragon/faktura na klienta).
- **Reklamacje / zwroty / rękojmia:** po stronie **operatora** jako sprzedawcy.
- **Przychód operatora:** marża (cena dla klienta − koszt zakupu) + ew. opłata za dostawę.
- **Ryzyko:** ten wariant **może nie odpowiadać docelowemu modelowi marketplace** (operator przestaje
  być pośrednikiem, staje się sprzedawcą detalicznym z pełnymi obowiązkami handlu towarem).

## 4. Tabela porównawcza

| Kryterium | A — Marketplace / pośrednictwo | B — Odsprzedaż przez operatora |
|---|---|---|
| Sprzedawca towaru | Sklep | Dowózka.pl (operator) |
| Kto przyjmuje płatność za towar | Sklep (bramka sklepu; ZipZap nie jest płatnikiem) | Operator |
| Kto wystawia dokument za towar | Sklep (osobno per klient — pytanie do księgowego) | Operator |
| Reklamacje / rękojmia / zwroty towaru | Sklep (kompletacja/dostawa: operator) | Operator |
| Braki / zamienniki | Zgodnie z preferencją klienta; koszt/ryzyko wg umowy | Operator (ryzyko dostępności) |
| Przychód operatora | Prowizja + opłata za dostawę/serwis | Marża + opłata za dostawę |
| Kapitał obrotowy | Nie (sklep sprzedaje własny towar) | Tak (operator finansuje zakup) |
| Obowiązki fiskalne za towar | Sklep | Operator |
| Zgodność z docelowym marketplace | Wysoka | Niska/średnia (zmiana charakteru działalności) |
| GMV = przychód operatora? | **Nie** (przychód ≠ wartość koszyka) | Częściowo (przychód = marża, nie GMV) |

## 5. Pytania do księgowego / prawnika (do rozstrzygnięcia PRZED płatnościami)

1. Czy **sklep pozostaje sprzedawcą**, gdy operator **fizycznie kupuje/odbiera** produkty podczas
   wspólnej rundy dla wielu klientów?
2. Czy i **jak sklep ma wystawiać dokument sprzedaży osobno dla każdego klienta** (paragon/faktura),
   skoro zakup rundy jest jedną transakcją w sklepie?
3. **Kto i na jakiej podstawie pobiera oraz rozlicza** środki za towary i za dostawę (przepływ pieniędzy,
   ewentualne pośrednictwo płatnicze, rozliczenie z regulacjami dot. usług płatniczych)?
4. Jak obsłużyć **fakturę na żądanie**, **zakup na firmę/NIP** oraz obowiązki **KSeF w 2026 r.**
   (harmonogram wdrożenia faktury ustrukturyzowanej)?
5. Jakie **obowiązki fiskalne** (kasa fiskalna/paragon, VAT) dotyczą **sklepu** i **operatora**
   w każdym z wariantów?
6. Kto ponosi **odpowiedzialność za zamienniki, niedostępność, produkty świeże i zwroty** wobec klienta
   (prawa konsumenta, rękojmia, odstąpienie od umowy na odległość)?
7. Jak model wpływa na **rejestr działalności/PKD**, umowy ze sklepami i regulamin świadczenia usług?
8. **Paragon / dokument za usługę dostawy:** kto go wystawia (Dowózka.pl czy sklep), na kogo i kiedy — jeśli
   klient płaci całość (towar + dostawa) przez **P24 sklepu**, a Dowózka.pl rozlicza dostawę ze sklepem osobno?
9. Czy kierunek „**kwota końcowa po kompletacji i zgodzie na zamienniki → płatność P24 do sklepu przed
   dostawą**" (§0) jest dopuszczalny i jak udokumentować różnicę względem kwoty szacunkowej?

### Źródła urzędowe (do weryfikacji z doradcą — bez cytowania długich fragmentów)

- Krajowy System e‑Faktur (KSeF): <https://www.podatki.gov.pl/ksef/>
- Podatki dla firm (VAT, kasy rejestrujące): <https://www.podatki.gov.pl/>
- Portal informacyjny dla przedsiębiorców (Biznes.gov.pl): <https://www.biznes.gov.pl/>
- Prawa konsumenta / sprzedaż na odległość (UOKiK): <https://prawakonsumenta.uokik.gov.pl/>
- Rzecznik/informacje konsumenckie: <https://www.uokik.gov.pl/>

> Linki mają pomóc **zidentyfikować kwestie do konsultacji**, nie zastępują indywidualnej interpretacji
> podatkowej ani porady prawnej.

## 6. Prosty model ekonomiczny (runda i zamówienie)

**Przychód operatora (na zamówienie/rundę)** liczony jako:

```
przychód operatora
  = prowizja
  + opłaty za dostawę
  − czas zakupów
  − czas dostaw
  − paliwo/transport
  − opłaty płatnicze
  − opakowania
  − zwroty/braki
  − pozyskanie klienta
  − podatki
```

- **W wariancie A (marketplace)** wartość towarów (GMV) **nie jest przychodem operatora** — przychodem
  są prowizja i opłaty za dostawę/serwis. Raporty muszą to rozróżniać (uogólnienie ledgera przychodu:
  `prowizja | opłata za dostawę` — patrz [ANALYSIS_TWO_PLANS.md](ANALYSIS_TWO_PLANS.md) §10).
- **W wariancie B (odsprzedaż)** przychód = **marża**, też **nie** równa GMV.
- **Koszt rundy** (do policzenia w pilotażu): czas zakupów + czas dostaw + paliwo + opakowania,
  rozłożony na liczbę zamówień w rundzie → **koszt jednostkowy realizacji zamówienia**.

## 7. Metryki pilotażu (do zbierania — bez danych osobowych)

Zbieramy **agregaty i identyfikatory techniczne**, **nie** dane osobowe ani pełne adresy:

- skany QR (per sklep/lokalizacja + kod źródła kampanii) — od S2 zbierane jako dzienny licznik wejść na kartę sklepu
  wg źródła (`qr`, `qr-kasa`…), bez IP i bez danych klienta; raport zbiorczy w S5,
- wejścia na kartę sklepu,
- dodania do ulubionych,
- **wyszukania bez wyników** (czego klienci szukają, a nie ma),
- utworzone koszyki,
- zamówienia, realizacje, anulowania,
- braki / zamienniki,
- średnia wartość koszyka,
- czas zakupów, czas dostaw, **koszt jednej rundy**.

> **Prywatność:** do analityki **nie** trafiają dane osobowe, pełne adresy, telefony, e‑maile ani dane
> płatnicze. Metryki opierają się na licznikach zdarzeń i identyfikatorach sklepu/rundy.

## 8. Rekomendacja i punkt decyzyjny (dla właściciela)

- **Wariant A (marketplace/pośrednictwo) — wybrany przez właściciela (§0)**: spójny z docelowym
  produktem; operator zarabia na prowizji + dostawie, nie na odsprzedaży. Wariant B nie jest rozwijany.
- **Nie włączamy** realnych płatności za towary, fakturowania, paragonów, KSeF **przed** potwierdzeniem
  wariantu przez **właściciela oraz księgowego/prawnika** (patrz §5).
- **Pilotaż** przygotowujemy **bez płatności online** (w trybie publicznym mock płatności jest wyłączony),
  żeby zebrać metryki (§7) i zwalidować ścieżkę, **bez** realnego obciążania klienta. Jak w takim trybie
  płynie pieniądz za towar — patrz **§9 (decyzja wymagana przed testem z prawdziwymi klientami)**.

## 9. Operacyjny przepływ pieniędzy w pilotażu — stan i decyzja

> **Decyzja właściciela (2026-09-24): W1 wyłącznie dla zamkniętej grupy testerów, bez pobierania pieniędzy.**
> W2 i W3 **nie są wybrane** — wracamy do nich po konsultacji z księgowym/prawnikiem. **Publiczny test z
> prawdziwymi klientami jest niedopuszczalny** do czasu tej decyzji. Wdrożenie techniczne W1 i ograniczenie
> do zamkniętej grupy — §9.6.

### 9.1 Stan faktyczny w systemie (zweryfikowany w kodzie, gałąź `pilot-s0-rounds`)
- Złożenie zamówienia tworzy wpis płatności **`Pending` bez sesji i bez autoryzacji** (tryb `Pilot:Public`).
- Płatność **nie blokuje realizacji**: zamówienie może zostać potwierdzone, skompletowane, wydane i oznaczone
  jako dostarczone.
- Prowizja **nie jest księgowana** (tylko dla płatności potwierdzonej webhookiem) — zamówienia pilotażu nie
  trafiają do Faktur ani Rozliczeń.
- **W systemie nie ma akcji „klient zapłacił”** (gotówka, karta, BLIK, przelew) — płatność zostaje `Pending`.
- **Co widzi dziś klient** po złożeniu zamówienia: ekran „Płatność” z tekstem „Do zapłaty X zł”, **nieaktywnym**
  przyciskiem „Zapłać teraz” i komunikatem „Czekam na potwierdzenie płatności od dostawcy…”, które **nigdy nie
  nadejdzie**, oraz link „Zapłacę później — śledź zamówienie”. **To wprowadza w błąd** — do poprawy przed
  jakimkolwiek testem z ludźmi.
- **Co widzi sklep/operator**: w „Zamówieniach” płatność „Oczekuje”, dostawca „—”.

### 9.2 Trzy pytania, na które przepływ musi odpowiedzieć
1. **Kto i kiedy płaci sklepowi** za towar kupiony podczas rundy?
2. **Jak klient płaci** i **jak system to oznacza** (kto, kiedy, ile, jaką metodą)?
3. **Co widzi klient** przed zamówieniem, po zamówieniu i po dostawie (w tym przy brakach/zamiennikach)?

### 9.3 Warianty

**W1 — Pilotaż zamknięty bez pobierania płatności (testerzy)**
- *Sklep:* operator kupuje towar **na własny rachunek** (jeden zakup w rundzie, dokument sprzedaży na operatora);
  koszt towaru = koszt testu (budżet pilotażu).
- *Klient:* nic nie płaci. System oznacza zamówienie jako **„testowe — bez opłaty”** (nie `Pending`).
- *Klient widzi:* „Zamówienie testowe — bez opłaty” zamiast ekranu płatności + rundę zakupową i okno dostawy.
- *Za:* najprostsze — brak odsprzedaży i brak dotykania pieniędzy klienta. *Przeciw:* nie sprawdza gotowości
  do płacenia; wymaga zgody i regulaminu testu; konwersja zawyżona.
- *Do księgowego:* rozliczenie kosztu towaru przekazanego testerom nieodpłatnie (cel testowy/marketingowy),
  ewentualne skutki podatkowe po stronie operatora i odbiorców.

**W2 — Płatność przy odbiorze na rzecz sklepu, pobierana przez operatora**
- *Sklep:* wydaje towar **per zamówienie** z dokumentem sprzedaży **na klienta** (nie jeden paragon na rundę);
  kto i kiedy płaci sklepowi — do ustalenia w umowie (np. rozliczenie po rundzie z pobranych kwot).
- *Klient:* płaci przy dostawie (gotówka / terminal sklepu / BLIK na rachunek sklepu); operator przekazuje środki
  sklepowi. System: akcja **„Pobrano płatność”** (metoda, kwota końcowa, kto, kiedy) — **status operacyjny, nie
  rozliczenie ani dokument fiskalny**.
- *Klient widzi:* przed zamówieniem „Płatność przy odbiorze” + **kwota szacunkowa** (może się zmienić przez
  braki/zamienniki); po kompletacji — kwota końcowa; po dostawie — dokument sprzedaży od sklepu.
- *Za:* najbliżej docelowego marketplace (sklep sprzedawcą), dobre UX. *Przeciw:* operator dotyka pieniędzy
  klienta, gotówka, fiskalizacja per klient po stronie sklepu.
- *Do prawnika/księgowego:* czy pobieranie przez operatora na rzecz sklepu wymaga szczególnej formy
  (pełnomocnictwo, umowa agencyjna, wyjątki w ustawie o usługach płatniczych); kiedy sklep wystawia dokument
  (przed wydaniem czy przy dostawie).

**W3 — Klient płaci bezpośrednio sklepowi (poza platformą)**
- *Sklep:* wydaje towar po potwierdzeniu wpłaty (lub zna klienta i dopuszcza płatność po dostawie).
- *Klient:* płaci sklepowi (przelew/BLIK na rachunek sklepu, link płatniczy sklepu). System: pracownik sklepu
  oznacza **„Opłacone u sklepu”** (kto, kiedy, kwota).
- *Klient widzi:* instrukcję płatności sklepu i status „Czeka na potwierdzenie sklepu” → „Opłacone”.
- *Za:* pieniądze nie przechodzą przez operatora. *Przeciw:* tarcie — płatność przed zakupem w rundzie, a braki
  i zamienniki zmieniają kwotę (zwrot różnicy); ręczne potwierdzenia.

### 9.4 Rekomendacja robocza (do decyzji właściciela)
- **Etap 1:** **W1** — zamknięta grupa testerów, bez płatności. Weryfikuje QR→PWA, rundy, kompletację, dostawy
  i koszt rundy bez ryzyka finansowo-podatkowego.
- **Etap 2 (prawdziwi klienci):** dopiero po odpowiedziach księgowego/prawnika. Właściciel wskazał kierunek
  do zaprojektowania (§0): **płatność online przez P24 sklepu przed dostawą, po kwocie końcowej** (pieniądze
  nie przechodzą przez operatora — bliżej W3, ale w aplikacji zamiast poza platformą). **Nie jest to
  zatwierdzony przepływ produkcyjny.** W2 (pobranie przy odbiorze przez operatora) nie pasuje do decyzji, że
  płatność klienta trafia bezpośrednio do sklepu (§0 pkt 2) — zostaje tylko jako tło porównawcze.

### 9.5 Co trzeba zbudować po wyborze wariantu (bez tego — brak testu z ludźmi)
1. **Tryb płatności pilotażu** w konfiguracji (np. `test` / `przy odbiorze` / `u sklepu`), udostępniony aplikacji
   w `/config/public` — aplikacja **nie pokazuje** wtedy ekranu „Zapłać teraz” ani oczekiwania na bramkę.
2. **Status operacyjny pobrania** osobny od płatności online (np. brak / test / pobrano / opłacone u sklepu)
   z metodą, kwotą, kto i kiedy — **nie** jest dokumentem fiskalnym ani rozliczeniem.
3. **Kwota szacunkowa vs końcowa** po brakach/zamiennikach (łączy się z S1b), widoczna dla klienta i operatora.
4. **Panel**: akcja „Pobrano płatność” / „Opłacone u sklepu” oraz kolumna statusu pobrania w zamówieniach
   i w rundzie.
5. **Raport** (S5): wartość towarów sklepów (GMV) oddzielnie od przychodu operatora.

### 9.6 W1 w systemie i ograniczenie do zamkniętej grupy (gałąź `pilot-s0-rounds`, niewdrożone)

**Co widzi tester:** przed złożeniem zamówienia baner „Zamówienie testowe — bez opłaty", podsumowanie
z wartością zamówienia i wierszem „Opłata: bez opłaty — zamówienie testowe", przycisk „Złóż zamówienie
testowe". Po złożeniu trafia **od razu na śledzenie zamówienia** (z tym samym banerem) — **nie widzi ekranu
„Do zapłaty" ani komunikatu o oczekiwaniu na potwierdzenie płatności**. Nawet wejście na stary adres
ekranu płatności przekierowuje do śledzenia.

**Co widzi operator/sklep:** w „Zamówieniach" kolumna płatności „Testowe — bez opłaty" (zamiast
„Oczekuje"), w szczegółach: „klient nie płaci; towar kupuje operator; brak płatności i księgowania".

**Jak system to oznacza:** zamówienie zapisuje tryb `test` w chwili złożenia (zmiana konfiguracji później
nie zmienia znaczenia starych zamówień). Dla zamówienia testowego **nie powstaje żaden wpis płatności**
ani prowizja; Faktury/Rozliczenia go nie obejmują.

**Ograniczenie do zamkniętej grupy — warstwy:**

| Warstwa | Jak działa | Stan |
|---|---|---|
| Rola `Tester` | W trybie `Pilot:PaymentMode=test` zamówienie może złożyć **tylko** konto z rolą `Tester` (lub Admin do kontroli). Pozostali dostają odmowę **zanim** zostanie zajęte miejsce w terminie dostawy. Przeglądanie oferty (także bez logowania) zostaje otwarte. | wdrożone w kodzie |
| Limit kosztu | Maks. `Pilot:TestOrdersPerTesterPerDay` zamówień testowych na testera w 24 h (domyślnie 3; anulowane się nie liczą) — każde zamówienie to towar kupiony przez operatora. | wdrożone w kodzie |
| Konfiguracja „fail-closed" | W publicznym pilotażu serwis **nie wystartuje** bez `Pilot:PaymentMode=test` — nie da się przypadkiem włączyć zamówień bez płatności dla wszystkich ani trybu „Oczekuje" bez końca. | wdrożone w kodzie |
| Zaproszenie (procedura) | Operator prowadzi listę zaproszonych (imię, e-mail). Tester zakłada konto w PWA → admin nadaje rolę „Tester pilotażu" (Panel → Użytkownicy) → tester wylogowuje się i loguje ponownie (rola trafia do tokenu). | procedura |
| Odebranie dostępu | Panel → Użytkownicy → **Dezaktywuj** (unieważnia sesje; token dostępu wygasa w ≤ 15 min). Brak jeszcze osobnej akcji „odbierz tylko rolę Tester" — do dodania, jeśli potrzebne. | częściowo |
| Kontrola przed rundą | Operator przegląda zamówienia testowe przed rundą i może **anulować** podejrzane. | procedura |
| Materiały QR | Na czas W1 QR/linki rozdajemy **tylko testerom**; nie drukujemy publicznych materiałów zachęcających do zamówień. | procedura |

**Czego to nie blokuje (świadome ryzyka):** tester może udostępnić swoje konto innej osobie — ogranicza to
limit dzienny i przegląd przed rundą. Mocniejsze opcje (kody zaproszeń przy rejestracji, weryfikacja
telefonu, lista dozwolonych adresów dostawy) — nie wdrożone, do decyzji.

---
*Dokument roboczy pilotażu. Uzupełniać po konsultacji z doradcą. Ostateczne decyzje podatkowe/prawne —
poza zakresem tego dokumentu.*
