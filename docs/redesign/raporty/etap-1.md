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
