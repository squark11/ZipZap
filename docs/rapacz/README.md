# Rapacz Market — uruchomienie w Dowózka.pl

Pierwszy partner (decyzja właściciela 2026-11-14). Kolejne sklepy zakładają konto i dodają asortyment same.

## Lokalizacje

| Lokalizacja | Adres | Współrzędne (OpenStreetMap) |
|---|---|---|
| Raba Wyżna | Raba Wyżna 557A, 34-721 Raba Wyżna | brak w OSM — ustawić pinezką w panelu |
| Spytkowice | Spytkowice 14B (lub 14a — do potwierdzenia), 34-745 Spytkowice | 49.5774210, 19.8254052 (OSM: „Rapacz Market", 14B) |
| Rabka-Zdrój | ul. Zakopiańska 6A, 34-700 Rabka-Zdrój | brak w OSM — ustawić pinezką w panelu |
| Klikuszowa | Klikuszowa 45B, 34-404 Klikuszowa | 49.5199754, 19.9861665 (OSM: 45b; obok punkt „Rapacz Market" pod nr 47) |

OSM zna też „Rapacz Market" w Makowie Podhalańskim (ul. Bartosza Głowackiego 3) — nie ma go na liście właściciela; do wyjaśnienia,
czy to ta sama sieć.

## Asortyment

W internecie nie ma katalogu ani gazetki Rapacz Market — asortyment musi przyjść od sklepu. Najprostsza droga:
**eksport z programu kasowo-magazynowego** (np. PC-Market, Subiekt GT, Comarch) do pliku CSV/Excel i wgranie w panelu:
Asortyment → „Import asortymentu z pliku" (najpierw podgląd, potem zapis).

Format (plik [`szablon-asortymentu.csv`](szablon-asortymentu.csv)), separator średnik, pierwszy wiersz to nagłówek:

| nazwa | kategoria | cena | jednostka | dostepny |
|---|---|---|---|---|
| nazwa jak na półce/paragonie | np. Nabiał | 4,99 (przecinek lub kropka) | szt / kg / 100g / l | tak / nie |

- Kodowanie: UTF-8 albo Windows-1250 (polski Excel, „CSV rozdzielany średnikami") — panel rozpoznaje oba (od 2026-11-14).
- Ponowne wgranie aktualizuje produkty po nazwie (ceny, dostępność); nowe nazwy dodaje. Eksport z panelu daje ten sam format.
- Każda lokalizacja to osobny sklep z własnym asortymentem — ten sam plik można wgrać do wszystkich czterech, a potem
  zmieniać ceny/dostępność osobno.
- Import nie przenosi zdjęć ani kodów EAN; zdjęcia dodaje się w edycji produktu.

## Do ustalenia z Rapaczem (blokuje start)

1. Osoba i e-mail do konta właściciela (jedno konto zarządza czterema lokalizacjami).
2. Plik z asortymentem i cenami (eksport z programu kasowego) — dla każdej lokalizacji albo jeden wspólny.
3. Obszar dostaw każdej lokalizacji (kody pocztowe / miejscowości) i opłata za dostawę.
4. Godziny dostaw (domyślnie po rundach zakupowych 12:00 i 16:00, pon–sob) i limit zamówień na termin.
5. Spytkowice: 14B czy 14a.
6. Dokumenty: regulamin i polityka prywatności sklepu (wymagane do akceptacji przy zamówieniu, jeśli sklep je włączy).

## Dane demonstracyjne na produkcji

„Rapacz — Rynek" (Kraków, Rynek Główny 5) i „Lewiatan — Podwawelskie" to sklepy demo z fikcyjnymi adresami, a
„Zasieg Test e6bf" to pozostałość testów. Przed pokazaniem aplikacji Rapaczowi trzeba je ukryć lub usunąć (decyzja właściciela),
żeby klienci nie widzieli fałszywego „Rapacza" w Krakowie.
