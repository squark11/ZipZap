# Raport Etapu 2 — Fundamenty frontendowe (Claude Code → ChatGPT Codex)

Data: 2026-11-16. Commity wypychane na `main`. Status wg Claude Code: **gotowe do weryfikacji**. Etapu nie uznaję
za zaliczony; o zaliczeniu decyduje weryfikacja Codexa wg [`03-weryfikacja-codex.md`](../03-weryfikacja-codex.md)
i akceptacja właściciela. Nowej wersji aplikacji **nie wdrożono** na produkcję (app.dowózka.pl to nadal stara wersja).

## Zrealizowane zadania

| ID | Zadanie | Wynik |
|---|---|---|
| E2-1 | Tokeny v2 | `zz_theme.dart`: `ZzColors` (brand, accent, grafit), `ZzPalette` (ThemeExtension, `context.zz`) z rolami jasny/ciemny wg `design-system.md` §1 (surface, card, border, text, textMuted, heading, brandStrong, onBrand, brandTint, success/danger/warning/info + tła, skeleton, cienie sm/md/float), `ZzSpace`, `ZzRadius` (12/20/28/pill), `ZzBreakpoints`, `ZzMotion`, `zzMinTouch = 48`. Wszystkie użycia w 22 plikach przeniesione na nowe nazwy |
| E2-2 | Czcionki w paczce | Inter i Poppins (400/500/600/700), podzbiór łaciński + polskie znaki, 8 plików, razem ok. 320 KB; licencje OFL obok; generator `docs/redesign/build_fonts.py`. Pakiet `google_fonts` usunięty |
| E2-3 | Biblioteka komponentów | `lib/core/ui/`: `ZzQtyStepper`, `ZzCartBar`, `ZzStoreCard` + `ZzStatusChip`, `ZzNotice` (info/sukces/ostrzeżenie/błąd), `ZzSectionHeader`, `ZzSearchField`, `ZzCategoryChip`; układ `ZzPage`, `context.zzWidth`, `context.zzSideNav`. Ekrany Start, Sklep, Koszyk, Śledzenie, Reset hasła korzystają z nich zamiast własnych kart/liczników/ramek komunikatów. Galeria `/dev/ui` tylko w trybie debug (`kDebugMode`) |
| E2-4 | Układ responsywny | Nawigacja dolna < 900 px, boczna (`NavigationRail`) od 900 px, rozwinięta z logo od 1200 px. Treść w `ZzPage`: Start 1100 px, Sklep 840 px, pozostałe 640 px (reguła z `design-system.md` §7). Lista sklepów od 700 px w 2–3 kolumnach o naturalnej wysokości kart |
| E2-5 | Ruch i dostępność | `ZzMotion.of()` zwraca 0 przy `MediaQuery.disableAnimations`; połysk szkieletu 250 ms co 1 s, statyczny przy „ogranicz ruch"; drzewo semantyki włączone na web (`SemanticsBinding.ensureSemantics`); etykiety „Mniej:/Więcej: {produkt}", „Ilość: n"; pola dotyku 48 × 48 |
| E2-6 | Nawigacja 5 zakładek | Start (`/stores`) · Szukaj (`/search`) · Koszyk · Zamówienia · Konto. Ścieżki `/stores`, `/stores/:id`, `/s/{slug}?src=qr` bez zmian — linki i kody QR działają (test `qr_entry_flow_test`) |

## Odstępstwa od planu (do oceny Codexa)

1. **Brak aliasów `@Deprecated`** dla starych nazw (`orange*`, `sm/md/lg`). Plan przewidywał je do końca Etapu 3;
   zamiast tego zmieniłem wszystkie użycia od razu (analyzer pilnuje, że nic nie zostało). Efekt ten sam, mniej długu.
2. **Cienie w `ZzPalette`, nie w osobnym `ZzShadow`** — różnią się w trybie ciemnym, więc należą do motywu.
3. **Szukaj** szuka na razie tylko sklepów (nazwa, miejscowość, adres; bez wielkości liter i polskich znaków).
   Wyszukiwanie produktów we wszystkich sklepach wymaga nowego endpointu — decyzja F-7 / zadanie E3-10.
4. **Wygląd ekranu Start** jest jeszcze stary (baner kodu pocztowego, lokalizacji) — przebudowa to Etap 3 (E3-1).
   Zmieniły się tylko karty sklepów, nawigacja i szerokości.

## Nowe pliki

- `mobile/lib/core/ui/{ui,zz_layout,zz_qty_stepper,zz_cart_bar,zz_store_card,zz_basics}.dart`
- `mobile/lib/features/search/search_screen.dart`, `mobile/lib/features/dev/ui_gallery_screen.dart`
- `mobile/assets/fonts/*.ttf` (8), `OFL-Inter.txt`, `OFL-Poppins.txt`; `docs/redesign/build_fonts.py`
- `mobile/test/redesign_foundation_test.dart`; ten raport

## Zmienione pliki

`zz_theme.dart`, `pubspec.yaml`, `main.dart`, `app_router.dart`, `main_scaffold.dart`, `zz_logo.dart` (FittedBox —
przelewał się o 27 px przy 360 px), `skeleton.dart`, `states.dart`, ekrany: stores, store_detail, cart,
order_track, forgot_password oraz pozostałe ekrany z migracji nazw kolorów (commit `8fce729`);
`test/qr_entry_flow_test.dart` — patrz niżej.

## Zmiana w istniejącym teście (jawnie)

`qr_entry_flow_test.dart`: oczekiwany tekst paska koszyka `'Koszyk (1)'` → `textContaining('1 produkt · ')`.
Powód: nowy `ZzCartBar` pokazuje „1 produkt · 9,98 zł  Koszyk →" (makieta Sklepu). Test nadal sprawdza to samo —
że po dodaniu produktu z wejścia przez QR pasek koszyka pokazuje 1 pozycję. Nic nie osłabiono w innych testach.

## Wyniki testów

| Polecenie | Wynik |
|---|---|
| `cd mobile && flutter analyze` | No issues found |
| `cd mobile && flutter test` | 95/95 (79 dotychczasowych + 16 nowych w `redesign_foundation_test.dart`) |
| Kontrast tokenów (test) | każda para tekst/tło w obu motywach ≥ 4,5:1, w tym `onBrand` na `brandStrong` |
| `Color(0x` poza `core/theme` | 0 (test + `grep`) |
| `google_fonts` w kodzie/pubspec | 0 |
| Układ całej aplikacji (test) | 360, 768, 1280, 1920 px — brak błędów przepełnienia; nawigacja dolna/boczna wg szerokości |
| Licznik przy tekście 130 % | pola ≥ 48 px, etykiety dostępności obecne; komponenty przy 320 px bez przepełnień; tryb ciemny |
| Sieć (build web lokalnie, panel przeglądarki) | 8 czcionek z `/assets/assets/fonts/`, **0 żądań** do `fonts.gstatic.com` / `googleapis.com` |
| Przegląd wizualny | 375 px: 5 zakładek z ikonami, karty sklepów; 1280 px: rozwinięta nawigacja boczna z logo, karty w 2 kolumnach |

Backendu i panelu nie zmieniano — `dotnet test` / `ng test` niepotrzebne.

## Znane problemy

1. Paczka web +320 KB (czcionki); w zamian 0 zapytań do Google przy starcie i działanie offline po instalacji.
2. Semantyka włączona na web — plan wymagał pomiaru wydajności przed/po; **nie wykonałem pomiaru**. Do zrobienia
   na telefonie właściciela przy Etapie 3 albo do oceny Codexa, czy to blokuje.
3. Zrzuty 768 i 1920 px tylko w testach widgetów (panel przeglądarki nie wyrenderował zrzutu przy ukrytym oknie).
4. W panelu przeglądarki błąd konsoli „An unknown error occurred when fetching the script." — rejestracja service
   workera w podglądzie lokalnym; na produkcji (lh.pl, HTTPS) instalacja PWA działała (Etap 1).
5. Lokalnie są dwa commity etapu: `1d99044` (WIP, nie kompiluje się — zapis stanu w toku) i `8fce729` (E2-1/E2-2);
   pełny stan etapu = HEAD.

## Jak zweryfikować

1. `cd mobile && flutter analyze && flutter test`.
2. `grep -rn "Color(0x" mobile/lib | grep -v core/theme` → pusto; `grep -rn google_fonts mobile` → pusto.
3. `flutter run -d chrome` → `/dev/ui` (galeria), zmiana szerokości okna 360 / 768 / 1280 / 1920 px.
4. Linki: `/stores`, `/s/{slug}?src=qr`, `/search`.
5. Kryteria etapu: `03-weryfikacja-codex.md` → „Etap 2".

---

## Runda 2 (2026-11-16) — po weryfikacji Codexa (FAIL)

Pełna odpowiedź, poprawki, zrzuty, pomiar semantyki i kompletny inwentarz plików: [`etap-1-2-zbiorczy.md`](etap-1-2-zbiorczy.md).
Korekta tego raportu: wynik „0 żądań do Google" był błędny (pamięć podręczna podglądu) — naprawione w rundzie 2; fonty to 310 200 B; testy 99/99.
