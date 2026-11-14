# Design System Dowózka.pl (E1-2, E1-5)

Zaakceptowany kierunek: „Świeżo i po sąsiedzku" (właściciel, 2026-11-14). Makiety:
[płótno projektowe](https://claude.ai/artifact/U4EWEdpwyYuvHgDuvi8ymj), kopie w [`makiety/`](makiety/).
Nazwy w kolumnie „Flutter" to docelowe tokeny z zadania E2-1 (`lib/core/theme/`).

## 1. Kolory

### Role (tryb jasny / ciemny)

| Rola | Flutter | Jasny | Ciemny | Do czego |
|---|---|---|---|---|
| Marka | `ZzColors.brand` | `#14B9BA` | `#14B9BA` | ilustracje, tła dekoracyjne, aktywne kropki — **nigdy pod biały tekst** |
| Marka mocna | `palette.brandStrong` | `#097778` | `#3FD3D4` | wypełnienie przycisków, linki, aktywna zakładka, cena-akcent |
| Tekst na marce mocnej | `palette.onBrand` | `#FFFFFF` | `#062A2A` | treść przycisku głównego |
| Marka jasna | `palette.brandTint` | `#E6F7F7` | `#0F2B2C` | chipy, licznik ilości, podświetlenie wyboru |
| Akcent | `ZzColors.accent` | `#FFB703` | `#FFB703` | promocje, baner rundy, plakietka koszyka; zawsze z tekstem `#1F2A37` |
| Tekst | `palette.text` | `#1F2A37` | `#E8F0F0` | treść i nagłówki |
| Tekst pomocniczy | `palette.textMuted` | `#5B6675` | `#9FB0B0` | podpisy, jednostki |
| Tło ekranu | `palette.surface` | `#F3F8F8` | `#0F1717` | scaffold, pasek stanu w trybie ciemnym |
| Karta | `palette.card` | `#FFFFFF` | `#182323` | karty, arkusze, nawigacja |
| Obrys | `palette.border` | `#E3EBEB` | `#263636` | pola, separatory |
| Sukces | `palette.success` / tło | `#15803D` / `#E9F7EE` | `#4ADE80` / `#12301E` | „Otwarte", „Dowozi tu" |
| Błąd | `palette.danger` / tło | `#B91C1C` / `#FDECEC` | `#F87171` / `#3A1515` | walidacja, „Zamknięte" |
| Uwaga | `palette.warning` / tło | `#B45309` / `#FEF3E2` | `#FBBF24` / `#3A2A0A` | „Poza strefą", tryb testowy |

### Kontrast (WCAG 2.1 AA, wyliczony)

| Para | Kontrast |
|---|---|
| tekst na tle ekranu / karcie | 13,56 / 14,54 |
| pomocniczy na karcie / tle | 5,83 / 5,44 |
| biały na marce mocnej (przycisk) | **5,36** (dziś biały na `#14B9BA`: 2,42 — nie spełnia) |
| marka mocna na marce jasnej (chip) | 4,85 |
| tekst na akcencie | 8,33 |
| sukces / błąd / uwaga na karcie | 5,02 / 6,47 / 5,02 |
| ciemny: tekst na tle / karcie | 15,71 / 13,93 |
| ciemny: pomocniczy na karcie | 7,14 |
| ciemny: `#3FD3D4` na karcie | 8,81 |
| ciemny: `#062A2A` na `#3FD3D4` (przycisk) | 8,36 |

Zasada: kolor nigdy nie jest jedynym nośnikiem stanu — status ma też tekst („Otwarte"), błąd ma komunikat.

## 2. Typografia

Poppins (nagłówki, 600–700) + Inter (treść, 400–700), pliki czcionek w paczce aplikacji (E2-2).

| Styl | Flutter | Krój | Rozmiar / interlinia | Użycie |
|---|---|---|---|---|
| H1 | `headlineMedium` | Poppins 700 | 26–28 / 1,2 | tytuł ekranu, powitanie |
| H2 | `titleLarge` | Poppins 600 | 18–22 / 1,25 | sekcje |
| H3 | `titleMedium` | Poppins 600 | 16–18 / 1,25 | nazwa sklepu |
| Treść | `bodyLarge` | Inter 400 | 16 / 1,5 | tekst, pola |
| Treść mała | `bodyMedium` | Inter 400–500 | 14 / 1,45 | opisy |
| Pomocniczy | `bodySmall` | Inter 500 | 13 / 1,4 | jednostki, meta |
| Etykieta | `labelSmall` | Inter 600–700 | 11–12, odstęp liter 0,04–0,06 em | zakładki, plakietki |
| Cena | `titleSmall` | Inter 700, cyfry tabelaryczne | 15–18 | ceny i sumy |

Tekst skaluje się z ustawieniem systemu do 130 % bez ucinania (E2-5).

## 3. Odstępy, promienie, cienie

- Odstępy `ZzSpace`: 4 · 8 · 12 · 16 · 20 · 24 · 32. Margines ekranu 16–20, odstęp między sekcjami 20–24.
- Promienie `ZzRadius`: `field` 12 · `card` 20 · `panel` 28 (nagłówek, arkusz dolny) · `pill` 999.
- Cienie `ZzShadow` (tylko tryb jasny; w ciemnym karty odróżnia jaśniejsze tło):
  - `sm`: `0 1 2 rgba(15,40,40,.06)` + `0 6 18 rgba(15,40,40,.05)` — karty produktów;
  - `md`: `0 1 2 rgba(15,40,40,.06)` + `0 8 24 rgba(15,40,40,.08)` — karty sklepów, panele;
  - `float`: `0 10 28 rgba(15,40,40,.22)` — pasek koszyka, pływająca nawigacja.
- Cel dotykowy ≥ 48 × 48 (ikony 20–24 w polu 44–48).

## 4. Komponenty

| Komponent | Warianty | Stany | Wymiary |
|---|---|---|---|
| Przycisk | główny (marka mocna), pomocniczy (obrys), tekstowy, ikona okrągła, niebezpieczny | zwykły, wciśnięty (skala 0,98), fokus (obwódka 3 px `brand`), ładowanie (wskaźnik zamiast tekstu, szerokość bez zmian), nieaktywny (`#E3EBEB` + `#5B6675`, kontrast 4,82) | wys. 52–56, pigułka; ikona 40–44 |
| Pole tekstowe | zwykłe, z ikoną, hasło z „pokaż" | zwykłe (obrys `#D5E2E2`), fokus (obrys marka mocna 1,5), błąd (obrys i komunikat pod polem), nieaktywne | wys. 52, promień 12, etykieta nad polem (nie tylko placeholder) |
| Pasek wyszukiwania | w nagłówku (białe tło), na stronie (tło ekranu) | pusty, wpisywanie, wyniki, brak wyników | wys. 50–52, pigułka |
| Chip kategorii | kółko z ikoną + podpis | zwykły (marka jasna), wybrany (marka mocna, ikona biała) | kółko 58–60, ikona 26–28 |
| Zakładki kategorii sklepu | pigułki w poziomym przewijaniu | wybrana (grafit, biały tekst), zwykła (karta) | wys. 40 |
| Karta sklepu | z okładką (F-5), kompaktowa (logo) | otwarte / zamknięte / wyszarzona „nie dowozi pod Twój kod" | okładka 16:9 lub 86–96 px, logo 52, status-pigułka |
| Karta produktu | siatka (zdjęcie, nazwa, cena/jednostka, „+") i wiersz (lista, koszyk) | dostępny, w koszyku (licznik zamiast „+"), niedostępny (wyszarzony, „Chwilowo brak") | zdjęcie 104–110 px, płytka zastępcza z ikoną kategorii |
| Licznik ilości | pigułka `− n +` | min. 1 (przy 1 „−" zmienia się w „usuń"), max wg stanu | wys. 34–44 |
| Pasek koszyka | pływający, ciemny | brak do minimum („Brakuje 5,02 zł"), gotowy | wys. 56–60, przycisk akcentowy |
| Pasek postępu minimum | — | poniżej / osiągnięte (tekst sukcesu) | wys. 8 |
| Kroki zamówienia | 3 segmenty + „Krok n z 3 · nazwa" | ukończony, bieżący, przyszły | wys. 6 |
| Wybór terminu | kafel 2 kolumny | wybrany (obrys marka mocna), wolny, pełny (nieaktywny + „brak miejsc") | wys. 64 |
| Arkusz dolny / okno | arkusz (telefon), okno (≥ 900 px) | — | promień 28 |
| Komunikat | sukces, błąd, informacja | pojawia się na 4 s, z akcją „Cofnij" gdy dotyczy | pływający, promień 16 |
| Stan pełnoekranowy | pusty, błąd, offline, sukces | ilustracja 160–200 px + 1 zdanie + 1 przycisk | — |
| Szkielet ładowania | karta sklepu, karta produktu, wiersz | połysk 1,2 s (bez połysku przy „ogranicz ruch") | jak docelowy element |
| Nawigacja | dolna pływająca (< 900 px), boczna (≥ 900 px) | aktywna (tło marki jasnej + tekst marki mocnej), plakietka koszyka | dolna wys. 68, 5 pozycji |

## 5. Ikony i ilustracje

- Ikony interfejsu: istniejący zestaw `assets/svg/icons/` (kreska ~2, `currentColor`).
- Ikony kategorii: `assets/svg/categories/` — 12 plików, kreska 1,8, `currentColor`, 24 × 24.
- Ilustracje: `assets/svg/illustrations/` — maskotka, 3 plansze onboardingu, 6 stanów. Płaskie, paleta marki,
  tło-plama z półprzezroczystego turkusu (czytelna w obu trybach), koła z turkusową obwódką (widoczne na ciemnym tle).
- Zasady: bez filtrów, stylów CSS i bitmap (ograniczenia flutter_svg); każdy plik ma `<title>`; ilustracja ≤ 12 KB,
  ikona ≤ 2 KB. Sprawdza to test `mobile/test/redesign_illustrations_test.dart`. Generator: [`gen-svg.mjs`](gen-svg.mjs).
- Zdjęcia produktów: z oferty sklepu; brak zdjęcia → płytka w pastelowym kolorze z ikoną kategorii.

## 6. Ruch (E1-5)

`ZzMotion`: `fast` 120 ms, `base` 200 ms, `slow` 250 ms; krzywa `easeOutCubic` (wejście), `easeInCubic` (wyjście).
Przy systemowym „ogranicz ruch" (`MediaQuery.disableAnimations`) wszystkie czasy = 0, połysk szkieletów wyłączony,
zmiany stanu natychmiastowe. Animacja nigdy nie blokuje dotyku ani nie opóźnia przejścia dalej.

| Gdzie | Co | Czas | Uwagi |
|---|---|---|---|
| Ekran startowy | maskotka wjeżdża z lewej, logo się pojawia | ≤ 1 200 ms łącznie | pomijany przy wejściu z linku/QR, gdy dane są gotowe |
| „+" na produkcie | przycisk zmienia się w licznik (rozszerzenie), plakietka koszyka podskakuje (skala 1 → 1,2 → 1) | 200 + 250 ms | wibracja lekka (Android) |
| Licznik | cyfra przesuwa się w górę/dół | 120 ms | — |
| Pasek koszyka | wjazd od dołu przy pierwszym produkcie | 200 ms | — |
| Zmiana zakładki | przenikanie | 120 ms | bez przesuwania całych ekranów |
| Kroki zamówienia | przesunięcie w bok + segment paska wypełnia się | 250 ms | wstecz w drugą stronę |
| Zamówienie przyjęte | ilustracja sukcesu: koło skaluje się, znak „✓" rysuje się kreską | 400 ms | jedyna dłuższa animacja, nie blokuje przycisku |
| Status zamówienia | nowy etap osi podświetla się | 200 ms | — |
| Szkielety | połysk przesuwa się | 1 200 ms pętla | wyłączony przy „ogranicz ruch" |

Bez nowych bibliotek (Lottie/Rive — decyzja F-4: nie teraz); wszystko w `AnimatedContainer`/`AnimatedSwitcher`/
`TweenAnimationBuilder`.

## 7. Responsywność

| Szerokość | Układ |
|---|---|
| < 600 | jedna kolumna, nawigacja dolna, produkty w 2 kolumnach |
| 600–899 | treść max 640 wyśrodkowana, produkty w 3 kolumnach, nawigacja dolna |
| 900–1199 | nawigacja boczna, produkty `auto-fill` min. 170 px, koszyk jako strona |
| ≥ 1200 | nawigacja boczna + treść max 1100 + koszyk jako panel boczny |

Bezpieczne obszary (notch, pasek gestów) respektowane; w trybie ciemnym `theme-color` = kolor tła, nie turkus.

## 8. Teksty

Po ludzku i krótko: „Gdzie dowieźć?", „Kiedy dowieźć?", „Sprawdź i zamów". Przycisk mówi, co się stanie
(„Złóż zamówienie", nie „OK"). Błąd mówi, co zrobić („Kod ma 5 cyfr, np. 31-042."). Termin dostawy zawsze
konkretnie („dziś od 17:00", „Dziś, 17:00–19:00"), nigdy „szybko".
