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

## 1. Założenie operacyjne pilotażu (kontekst)

Operator Dowózka.pl **nie jedzie do sklepu po każdym zamówieniu**. Na start robi zakupy i
kompletuje zamówienia w **dwóch zaplanowanych rundach zakupowych dziennie: 12:00 i 16:00**
(strefa `Europe/Warsaw`, godziny konfigurowalne). Zamówienia zebrane do rundy są kupowane
**łącznie podczas jednej wizyty w sklepie**, ale **każda pozycja nadal należy do konkretnego
zamówienia i klienta**.

**Runda zakupowa ≠ okno dostawy.** Runda = kiedy operator *kupuje/kompletuje*. Okno dostawy =
kiedy klient *dostaje*. Klient musi widzieć oba oraz **nie wolno obiecywać dostawy natychmiastowej**.

Docelowo produkt to **marketplace lokalnych sklepów** — każdy sklep prowadzi własny katalog i
ofertę. Pilotaż nie może przekształcić platformy w jeden centralny sklep Dowózka.pl ani przenieść
własności asortymentu na platformę.

**Robocze założenie do UI i modelu danych (wymaga potwierdzenia):** sprzedawcą towaru jest
**sklep**, a Dowózka.pl obsługuje **platformę + zakupy/kompletację + dostawę**. To założenie
biznesowe, nie rozstrzygnięcie prawne/podatkowe.

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

- skany QR (per sklep/lokalizacja + kod źródła kampanii),
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

- **Robocza rekomendacja projektowa:** wariant **A (marketplace/pośrednictwo)** — spójny z docelowym
  produktem; operator zarabia na prowizji + dostawie, nie na odsprzedaży.
- **Nie włączamy** realnych płatności za towary, fakturowania, paragonów, KSeF **przed** potwierdzeniem
  wariantu przez **właściciela oraz księgowego/prawnika** (patrz §5).
- **Pilotaż** przygotowujemy **bez płatności online** (w trybie publicznym mock płatności jest wyłączony),
  żeby zebrać metryki (§7) i zwalidować ścieżkę, **bez** realnego obciążania klienta. Jak w takim trybie
  płynie pieniądz za towar — patrz **§9 (decyzja wymagana przed testem z prawdziwymi klientami)**.

## 9. Operacyjny przepływ pieniędzy w pilotażu — stan i decyzja

> **Status: BLOKER.** Dopóki właściciel nie wybierze wariantu z §9.3, pilotaż **nie jest gotowy do testu z
> prawdziwymi klientami** (może ruszyć najwyżej zamknięta grupa testerów w wariancie W1).

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
- **Etap 2 (prawdziwi klienci):** **W2 lub W3** dopiero po odpowiedziach księgowego/prawnika. W3 najmniej
  angażuje operatora w pieniądze klienta; W2 daje najlepsze UX.

### 9.5 Co trzeba zbudować po wyborze wariantu (bez tego — brak testu z ludźmi)
1. **Tryb płatności pilotażu** w konfiguracji (np. `test` / `przy odbiorze` / `u sklepu`), udostępniony aplikacji
   w `/config/public` — aplikacja **nie pokazuje** wtedy ekranu „Zapłać teraz” ani oczekiwania na bramkę.
2. **Status operacyjny pobrania** osobny od płatności online (np. brak / test / pobrano / opłacone u sklepu)
   z metodą, kwotą, kto i kiedy — **nie** jest dokumentem fiskalnym ani rozliczeniem.
3. **Kwota szacunkowa vs końcowa** po brakach/zamiennikach (łączy się z S1b), widoczna dla klienta i operatora.
4. **Panel**: akcja „Pobrano płatność” / „Opłacone u sklepu” oraz kolumna statusu pobrania w zamówieniach
   i w rundzie.
5. **Raport** (S5): wartość towarów sklepów (GMV) oddzielnie od przychodu operatora.

---
*Dokument roboczy pilotażu. Uzupełniać po konsultacji z doradcą. Ostateczne decyzje podatkowe/prawne —
poza zakresem tego dokumentu.*
