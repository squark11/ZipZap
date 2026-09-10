# Import asortymentu — szablon

Plik **`asortyment-import-szablon.csv`** to wzór do masowego wgrania oferty sklepu.
Wypełnij wierszami (jeden produkt = jeden wiersz) i zaimportuj w panelu.

## Kolumny (separator `;`)
| Kolumna | Wymagane | Opis |
|---|---|---|
| `nazwa` | ✅ | Nazwa produktu (np. `Chleb żytni`). |
| `kategoria` | — | Nazwa kategorii; dopasowywana **po nazwie**, tworzona gdy nie istnieje. Puste = bez kategorii. |
| `cena` | ✅ | Cena za jednostkę. Przecinek lub kropka (`6,00` lub `6.00`). |
| `jednostka` | ✅ | Jednostka miary: `szt`, `kg`, `l`, `opak`… |
| `dostepny` | — | `tak` / `nie` (domyślnie `tak`). |

Waluta: **PLN** (domyślnie).

## Format pliku
- **Separator: średnik `;`** (domyślny dla Excela PL).
- **Kodowanie: UTF‑8 (z BOM)** — plik otwiera się poprawnie w Excelu z polskimi znakami.
  Zapisując z Excela wybierz typ **„CSV UTF‑8 (rozdzielany przecinkami)"**.
- Pierwszy wiersz to **nagłówki** — nie usuwaj go.

## Jak zaimportować (gdy funkcja będzie w panelu)
1. Panel → **Oferta** → **Importuj z pliku** → wybierz `.csv`.
2. **Podgląd** pokaże rozpoznane wiersze i ewentualne błędy (do poprawy przed zatwierdzeniem).
3. **Zatwierdź** → produkty zostają dodane/zaktualizowane (**upsert po nazwie** w obrębie sklepu).

> Uwaga: import **aktualizuje** istniejący produkt o tej samej nazwie (nadpisuje cenę/jednostkę/dostępność).
> Sprawdź plik przed importem. Kopię obecnej oferty pobierzesz przez **Eksport asortymentu**.
