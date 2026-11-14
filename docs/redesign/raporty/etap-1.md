# Raport Etapu 1 — Koncepcja wizualna (Claude Code → ChatGPT Codex)

Data: 2026-11-14. Gałąź: `main`. Status wg Claude Code: **gotowe do weryfikacji** — etapu nie uznaję za zaliczony;
decyduje weryfikacja Codexa wg [`03-weryfikacja-codex.md`](../03-weryfikacja-codex.md) i akceptacja właściciela.

Właściciel zaakceptował 2026-11-14: kierunek wizualny (F-2), okładki sklepów (F-5), kategorie platformy (F-6),
zakres „tylko aplikacja webowa / PWA, bez sklepów z aplikacjami" (F-1).

## Zrealizowane zadania

| ID | Zadanie | Wynik |
|---|---|---|
| E1-1 | Tablica kierunku | artboard „Kierunek wizualny" (paleta z kontrastem, typografia, kształty, komponenty, co bierzemy z inspiracji) |
| E1-2 | Specyfikacja Design System | [`design-system.md`](../design-system.md): role kolorów jasny/ciemny + tabela kontrastów, typografia, odstępy, promienie, cienie, 17 komponentów z wariantami/stanami/wymiarami, responsywność, teksty; skill `.claude/skills/zipzap-app-design` przestawiony z pomarańczu na zaakceptowaną paletę |
| E1-3 | Makiety | 9 artboardów: Start, Start (ciemny), Sklep, Koszyk, Zamówienie w 3 krokach (adres, termin, podsumowanie), Logowanie, widok na komputerze (≥ 900 px), Grafiki, Kierunek |
| E1-4 | Ilustracje i ikony | 10 ilustracji (maskotka, 3 onboardingu, 6 stanów) + 12 ikon kategorii, płaskie SVG 0,3–1,8 KB; test parsowania w silniku aplikacji |
| E1-5 | Specyfikacja ruchu | `design-system.md` §6: czasy, krzywe, 9 animacji, zachowanie przy „ogranicz ruch" |

Dodatkowo (poza Etapem 1, wykonane wcześniej tego dnia, commit `4d8ef05`): naprawa instalacji PWA — ikony w
`/app-icons/` (hosting lh.pl przesłaniał `/icons/` → 404), ikona 180 px dla iOS, `id`/`scope`/`lang` w manifeście.
Potwierdzone na telefonie właściciela (Android 13, Chrome 157): aplikacja zainstalowana jako WebAPK, start
pełnoekranowy, lista sklepów po ok. 2 s.

## Nowe pliki

- `docs/redesign/design-system.md`
- `docs/redesign/gen-svg.mjs` — generator ilustracji i ikon (źródło prawdy dla SVG)
- `docs/redesign/makiety/*.dc.html`, `canvas.json` — makiety (kopia płótna https://claude.ai/artifact/U4EWEdpwyYuvHgDuvi8ymj)
- `docs/redesign/makiety/grafiki-podglad.png` — podgląd ilustracji na jasnym i ciemnym tle
- `docs/redesign/raporty/etap-1.md` — ten raport
- `mobile/assets/svg/illustrations/*.svg` (10), `mobile/assets/svg/categories/*.svg` (12)
- `mobile/test/redesign_illustrations_test.dart`

## Zmienione pliki

- `.claude/skills/zipzap-app-design/SKILL.md` — paleta, zakres PWA, wskazanie na `design-system.md`
- `docs/redesign/02-plan-zadan-claude.md` — usunięta budowa APK (decyzja F-1), ścieżka ikon `web/app-icons`
- `docs/redesign/05-decyzje.md` — statusy decyzji

## Zmiany w architekturze

Brak. Kod aplikacji (`mobile/lib`), backend i panel nietknięte. Nowe pliki SVG **nie są jeszcze w `pubspec.yaml`**
(podkatalogi `illustrations/` i `categories/` nie są objęte wpisami `assets/svg/` i `assets/svg/icons/`), więc nie
zwiększają paczki — dołączy je Etap 2.

## Wyniki testów

| Polecenie | Wynik |
|---|---|
| `cd mobile && flutter test test/redesign_illustrations_test.dart` | 24/24 — każdy plik: parsuje się przez `vg.loadPicture`, ma `<title>`, bez `<style>`/`filter`/`<image>`, ilustracja ≤ 12 KB, ikona ≤ 2 KB |
| `cd mobile && flutter test` | 79/79 (55 dotychczasowych + 24 nowe) |
| `cd mobile && flutter analyze` | No issues found |
| Kontrast palety | wyliczony skryptem (wzór WCAG 2.1) — tabela w `design-system.md` §1; najniższa para tekstowa 4,54 (sukces na swoim tle) |
| Przegląd wizualny ilustracji | jeden render na jasnym i ciemnym tle (headless Edge) → poprawka: koła z turkusową obwódką, bo na ciemnym tle znikały |

Backendu nie zmieniano — `dotnet test` nie był potrzebny.

## Znane problemy

1. Makiety to HTML, nie Flutter — różnice renderu czcionek i cieni możliwe; ostateczny wygląd sprawdzi Etap 2/3.
2. Zdjęcia produktów i okładki na makietach to rysowane płytki (płótno nie ładuje zdjęć z sieci).
3. Opłata za dostawę na kroku 3 checkoutu to symbol `[opłata strefy]` — kwota zależy od strefy sklepu.
4. Tryb ciemny ilustracji: elementy grafitowe (`#1F2A37`) mają niski kontrast z tłem `#0F1717`; zostawione, bo leżą
   na półprzezroczystej plamie i mają obrys; do oceny Codexa.
5. Słodycze i dziecko — ikony kategorii najmniej czytelne w 24 px; do oceny przy wdrożeniu.

## Niewykonane w tym etapie

- Pliki w Figmie (decyzja F-3 otwarta; makiety są na płótnie Claude).
- Makiety ekranów: konto, adresy, zamówienia/śledzenie, onboarding, wyszukiwarka — w specyfikacji komponentów, bez
  osobnych artboardów; zrobię przed odpowiednim zadaniem Etapu 3, jeśli Codex lub właściciel uzna to za blokujące.

## Jak zweryfikować

1. `git log --oneline -3` — commit Etapu 1 zawiera tylko pliki z list powyżej (`git show --stat HEAD`).
2. `cd mobile && flutter analyze && flutter test`.
3. `node docs/redesign/gen-svg.mjs <katalog>` — generuje te same pliki co w `mobile/assets/svg/{illustrations,categories}`
   (porównać `diff -r`).
4. Kontrast: dowolne narzędzie WCAG dla par z `design-system.md` §1.
5. Makiety: otworzyć płótno https://claude.ai/artifact/U4EWEdpwyYuvHgDuvi8ymj (dostęp nadaje właściciel) albo pliki
   `docs/redesign/makiety/*.dc.html` jako tekst; sprawdzić, że każda kwota/produkt pochodzi z danych demo
   (`backend/src/ZipZap.Api/Program.cs`, `/api/admin/seed/pilot`) lub jest oznaczona etykietą „wymaga backendu".
6. Kryteria etapu: `03-weryfikacja-codex.md` → „Etap 1".

---

## Poprawki po weryfikacji Codexa (FAIL z 2026-11-14) — runda 2

Ocena Claude Code każdego zarzutu i co zrobiono:

| Zarzut Codexa | Ocena Claude Code | Działanie |
|---|---|---|
| E1-3: brak makiet Produktu, Zamówienia/śledzenia, Konta | **słuszny** — kryterium z mojego planu | dodane: `Produkt.dc.html` (arkusz z wyborem jednostki kg/szt., licznik, „Dodaj · 9,98 zł"), `Zamowienie.dc.html` (oś statusów, termin, pozycje, telefon do sklepu), `Konto.dc.html` (potwierdzenie e-mail, „Dodaj do ekranu głównego", ustawienia, motyw) |
| E1-3: ekrany w 390 zamiast 375 px | **słuszny** (390 to popularna szerokość, ale kryterium mówi 375) | wszystkie makiety telefonu przestawione na 375 × 844; checkout 3 × 375 |
| E1-3: brak wariantów 1280 dla każdego ekranu | **częściowo słuszny** | dodane 1280: `KomputerSklep` (sklep + koszyk w panelu z paskiem minimum), `KomputerZamowienie` (krok terminu + podsumowanie); Start 1280 był. Koszyk na 1280 = panel boczny na tych artboardach. **Propozycja zmiany kryterium (do akceptacji właściciela):** Produkt, Logowanie, Konto i Śledzenie mają na komputerze ten sam układ jednokolumnowy (maks. 640 px, Produkt jako okno dialogowe) — opisany regułą w `design-system.md` §7 zamiast osobnych artboardów |
| E1-3: brak 4 stanów ekranów | **słuszny** | dodany `Stany.dc.html`: ładowanie (szkielety Startu + komunikat „Budzimy serwer…" po 4 s), pusty koszyk (ilustracja + „Przeglądaj sklepy"), błąd oferty sklepu („Spróbuj ponownie"), brak internetu (pasek + ostatnie dane, koszyk zachowany) — te same wzorce dotyczą pozostałych ekranów |
| Daty dostaw niezgodne z seedem (sobota/poniedziałek vs `tomorrow`) | **częściowo słuszny**. Seed tworzy terminy „na jutro" względem chwili wywołania, nie na stałą datę — nie wynika z niego „niedziela 15.11". Sprawdziłem produkcję: **oba sklepy mają dziś 0 terminów dostawy**, a API rund podaje najbliższą rundę na poniedziałek 16.11, 12:00 (sobota 15:30 minęła, niedziela poza harmonogramem). Słuszne natomiast: zdanie „W niedziele nie dowozimy" nie wynikało z API (harmonogram dotyczy rund zakupowych, nie terminów dostawy) | zdanie usunięte; terminy oznaczone jako przykładowe (stopka checkoutu, etykieta na 1280, notatka na płótnie) |
| Suma „3 produkty · 21,77 zł" bez pozycji | **słuszny — błąd** (żadna kombinacja produktów demo nie daje 21,77) | poprawione na 22,46 zł = Jabłka 2 kg 9,98 + Mleko 3,49 + Szynka 8,99 |
| E1-5: sukces 400 ms i pętla połysku 1 200 ms przekraczają 250 ms | **słuszny** | sukces: 250 ms (120 + 130); połysk szkieletu: jeden przebieg 250 ms, powtarzany co 1 s — każdy ruch ≤ 250 ms. Zaznaczyłem w specyfikacji, że to wskaźnik czekania (do potwierdzenia przez właściciela, czy tak liczymy) |
| Grafit ilustracji 1,25:1 na ciemnym tle | uwaga zasadna, element dekoracyjny | bez zmian w tej rundzie — koła mają turkusową obwódkę; ocena przy wdrożeniu (Etap 3) |
| Gałąź: raport mówi `main`, worktree to `pilot-outbox-deadletter` | słuszna precyzja | `HEAD` = `origin/main`; commity są wypychane na `main`. Lokalna gałąź `main` w głównym katalogu jest nieaktualna i nieużywana |

Dodatkowa uwaga z karty Produktu: nie obiecujemy „kwoty po skompletowaniu" — zasada rozliczania po kompletacji jest
tylko rekomendacją, niezatwierdzoną przez właściciela (PILOT_BUSINESS_MODEL.md).

**Ustalenie operacyjne poza Etapem 1 (do właściciela):** na produkcji Lewiatan i Rapacz nie mają żadnych przyszłych
terminów dostawy → klient nie może dziś złożyć zamówienia. Terminy dodaje sklep lub admin w panelu.

Pliki rundy 2: `docs/redesign/makiety/{Produkt,Zamowienie,Konto,Stany,KomputerSklep,KomputerZamowienie}.dc.html` (nowe),
`Start, Sklep, Koszyk, Logowanie, StartCiemny, Checkout, Main` + `canvas.json` (zmienione), `design-system.md` §4 i §6,
ten raport. Kod aplikacji bez zmian — `flutter analyze` / `flutter test` jak w rundzie 1.

---

## Runda 3 — odpowiedź na weryfikację Codexa z rundy 2 (FAIL)

| Kryterium | Status rundy 2 | Ocena Claude Code | Działanie |
|---|---|---|---|
| Stany każdego ekranu | FAIL | **słuszny** — kryterium z mojego planu; teksty stanów każdego ekranu są potrzebne do Etapu 3 | dodany `StanyEkranow.dc.html`: macierz **8 ekranów × 4 stany** (Start, Sklep, Produkt, Koszyk, Zamówienie-checkout, Zamówienia i śledzenie, Konto, Logowanie × ładowanie / pusty / błąd / brak internetu). Każda komórka ma tekst właściwy dla ekranu. Komunikaty błędów oparte na API: pełny termin („Wybrany slot dostawy jest już pełny.", „…przy dużym ruchu"), logowanie 401 „Nieprawidłowy e-mail lub hasło.", limit prób 429 (API bez tekstu — komunikat oznaczony jako propozycja). Logowanie / pusty: oznaczone „nie dotyczy" z uzasadnieniem (formularz zawsze widoczny). Brak internetu: koszyk jest na serwerze, więc zmiany ilości i „+" są wyłączone, nic nie trafia do kolejki. Generator: `docs/redesign/gen-states.mjs`, podgląd: `makiety/stany-podglad.png` |
| Warianty 1280 px | BLOCKED | słuszny — wymaga decyzji właściciela | bez zmian; czeka na odpowiedź właściciela (reguła jednokolumnowa dla Produktu, Logowania, Konta, Śledzenia) |

Pliki rundy 3: `docs/redesign/makiety/StanyEkranow.dc.html`, `canvas.json`, `stany-podglad.png`,
`docs/redesign/gen-states.mjs`, ten raport, raport Codexa `etap-1-codex.md` (zapisany przez Codexa). Kod aplikacji bez zmian.

---

## Runda 4 — decyzja właściciela (2026-11-14)

Właściciel zaakceptował regułę zamiast osobnych makiet 1280 px dla Produktu, Logowania, Konta i Śledzenia
(„Tak, opisz regułą"). Zmieniono kryterium E1-3 w `02-plan-zadan-claude.md`, listę kontrolną Etapu 1 w
`03-weryfikacja-codex.md` oraz dopisano regułę w `design-system.md` §7 (kolumna maks. 640 px, Logowanie jako karta 440 px,
Produkt jako okno dialogowe 560 px od 900 px). Status „BLOCKED" z rundy 2 ma tym samym rozstrzygnięcie.

**Poza Etapem 1 — dane produkcyjne (na polecenie właściciela „dodaj terminy dostaw"):** po kopii bazy
(`neondb-przed-terminami-20261114-155554.dump`) dodano 48 terminów: Lewiatan — Podwawelskie i Rapacz — Rynek, strefa
„Centrum", 16–28.11.2026, pon–sob, 13:00–15:00 i 17:00–19:00 (godzinę po rundach 12:00 i 16:00), limit 20 na termin.
Wstawienie w jednej transakcji, idempotentne. Sprawdzone przez publiczne API: po 24 terminy na sklep, pierwszy
16.11 13:00 = najwcześniejsza dostawa po rundzie 12:00. Na produkcji jest też sklep testowy „Zasieg Test e6bf"
(pozostałość testów) — nie ruszany, do decyzji właściciela.
