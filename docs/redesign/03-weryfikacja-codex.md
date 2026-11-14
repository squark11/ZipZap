# D. Lista zadań dla ChatGPT Codex (niezależny audytor)

Codex pracuje na tym samym repozytorium i **nie ufa raportom Claude Code** — każdą deklarację sprawdza w kodzie
albo uruchomieniem. Statusy: PASS · FAIL · BLOCKED · NOT TESTED. Wynik zapisuje w
`docs/redesign/raporty/etap-N-codex.md`.

## Zawsze (każdy etap)

| Co | Jak | Zaliczenie | Blokuje etap |
|---|---|---|---|
| Kompilacja i testy | `cd mobile && flutter analyze && flutter test`; `dotnet test backend/ZipZap.sln`, jeśli zmieniono backend | 0 błędów, 0 ostrzeżeń analizy, wszystkie testy zielone | jakikolwiek błąd |
| Testy nie zostały osłabione | `git diff <baza>..HEAD -- mobile/test backend/tests`: usunięte lub wyłączone testy, `skip:`, `// ignore` | brak usunięć bez uzasadnienia w raporcie | usunięty test ścieżki zamówienia |
| Logika biznesowa nietknięta | diff `cart_controller.dart`, `*_repository.dart`, `models/*`, backend | zmiany tylko zadeklarowane w raporcie | niezadeklarowana zmiana wywołań API |
| Tokeny | `grep -rn "Color(0x" mobile/lib` poza `core/theme` | 0 wyników (od Etapu 2) | więcej niż 0 |
| Sekrety | diff pod kątem kluczy, haseł, plików `.env` | brak | jakikolwiek sekret |
| Zgodność raportu | lista plików z raportu porównana z `git diff --stat` | identyczne | plik zmieniony, a niewymieniony |

## Etap 0 — sprawdzenie audytu

Zweryfikować samodzielnie: liczbę plików, linii i testów; brak wyszukiwarki i kategorii w UI
(`grep -rn "categor" mobile/lib/features`); jedno użycie `maxWidth`; rozmiary `main.dart.js` i `canvaskit.wasm`
(`curl -H "Accept-Encoding: gzip"`); pobieranie czcionek z Google; kontrast bieli na `#14B9BA`.
FAIL, gdy fakt z audytu jest nieprawdziwy. Dopisać własne znaleziska.

## Etap 1

Kontrast każdej pary z palety (własne wyliczenie); każda makieta ma 4 stany; dane na makietach istnieją w API
albo są oznaczone „wymaga backendu"; pliki SVG się otwierają, mają ≤ 12 KB, bez osadzonych bitmap i obcych licencji.
Blokuje: obiecane dane bez pokrycia, kontrast tekstu poniżej 4,5:1.

## Etap 2

Brak `google_fonts` w `pubspec.yaml` i brak żądań do Google przy starcie; komponenty z `core/ui` użyte zamiast
lokalnych kopii; układ w 360/768/1280/1920 px (własne zrzuty); animacje wyłączają się przy `disableAnimations`;
stare linki `/stores` i `/s/{slug}?src=qr` działają. Blokuje: regresja linków QR, poziome przewijanie.

## Etap 3 (dla każdego ekranu)

Przejść ścieżkę ręcznie na lokalnym API: wejście z kodu QR → sklep → koszyk → logowanie → checkout → zamówienie
testowe → śledzenie. Sprawdzić stany pusty/błąd/offline (wyłączyć API i sieć) oraz cele dotykowe ≥ 48 px.
Porównać żądania sieciowe checkoutu przed i po (te same endpointy i ładunki).
Blokuje: inny ładunek zamówienia, brak osobnej linii opłaty za dostawę, brak informacji o terminie dostawy,
zamówienie możliwe bez wymaganej zgody.

## Etap 4

Manifest (Lighthouse „installable"); service worker nie zapisuje odpowiedzi API ani żądań z `Authorization`
(przegląd `sw.js` i Cache Storage po sesji z logowaniem); aktualizacja po podmianie plików; zachowanie bez sieci.
Blokuje: dane konta lub koszyka w pamięci podręcznej, biała strona po aktualizacji.

## Etapy 5–6

Powtórzyć pomiar wagi startu i porównać z audytem; uruchomić scenariusze z `TEST_SCENARIOS.md`; sprawdzić, że
dokumentacja opisuje stan faktyczny; po wdrożeniu sprawdzić nagłówki, HTTPS i wersję plików na `app.dowózka.pl`.
