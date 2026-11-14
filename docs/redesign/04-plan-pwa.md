# E. Plan PWA

## Stan obecny (sprawdzony)
Jest: `manifest.json` (standalone, kolory, ikony 192/512 + maskable), `sw.js` (network-first, tylko własny origin,
API pomijane, nawigacje → `index.html`), ekran ładowania, podpowiedź instalacji, HTTPS z przekierowaniem,
`Cache-Control: no-cache`. Aplikację da się już zainstalować w Chrome/Edge.

## Luki
| # | Luka | Działanie |
|---|---|---|
| 1 | Ikony „maskable" to te same pliki co zwykłe (brak marginesu bezpieczeństwa), brak ikony 180 px dla iOS | nowe ikony z marginesem 20 %, `apple-touch-icon` 180 |
| 2 | Manifest bez `id`, `scope`, `lang`, `categories`, `shortcuts`, `screenshots` | uzupełnić; zrzuty dają bogate okno instalacji na Androidzie |
| 3 | Brak ekranów startowych iOS i `theme-color` dla trybu ciemnego | `apple-touch-startup-image` dla głównych rozmiarów, dwa `theme-color` z `media` |
| 4 | Instalacja: brak własnego przycisku „Zainstaluj" (Android/desktop) i instrukcji dla Safari | obsługa `beforeinstallprompt`; na iOS plansza „Udostępnij → Do ekranu początkowego" |
| 5 | Aktualizacja: `skipWaiting` bez informacji dla użytkownika | komunikat „Dostępna nowa wersja — odśwież" przy `controllerchange` |
| 6 | Offline: jest pasek, brak ekranu „Brak połączenia" dla tras wymagających danych | ekran z ilustracją i „Spróbuj ponownie"; **bez** obietnicy pracy offline |
| 7 | Start 3–4 MB, czcionki z Google | czcionki w paczce, próba budowy WebAssembly (`--wasm`), przegląd zależności, pomiar przed/po |
| 8 | Usypianie API (Render free) | decyzja F-9: płatny plan albo podtrzymywanie; aplikacja pokazuje „Budzimy serwer…" po 4 s |

## Strategia pamięci podręcznej
- Powłoka (HTML/JS/WASM/czcionki/ikony): network-first z zapasem z pamięci — bez zmian.
- Zdjęcia produktów i logo (publiczne, z API): stale-while-revalidate, limit 200 pozycji / 7 dni.
- **Nigdy**: odpowiedzi API (oferta, ceny, koszyk, zamówienia, konto), żądania z nagłówkiem `Authorization`.

## Testy (kryterium zaliczenia Etapu 4)
| Platforma | Co sprawdzamy |
|---|---|
| Android Chrome | okno instalacji, ikona maskable, tryb standalone, powrót z płatności, aktualizacja, tryb samolotowy |
| iOS Safari 16.4+ | „Do ekranu początkowego", ekran startowy, bezpieczne obszary (notch), klawiatura w formularzach, wylogowanie po 7 dniach bezczynności (ograniczenie iOS) |
| Windows/macOS Chrome, Edge | instalacja z paska adresu, okno ≥ 900 px z nawigacją boczną, skróty |
| Safari macOS, Firefox | działanie w karcie (bez instalacji) |
| Lighthouse (mobile) | PWA: instalowalna; cel: LCP ≤ 4 s na „Fast 4G", brak błędów w konsoli |
Testy na prawdziwych telefonach wykonuje właściciel według listy kontrolnej; ja dostarczam wyniki z emulacji i Lighthouse.
