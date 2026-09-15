# Wymagane jeszcze rzeczy (przegląd luk) — 2026-09-15

Lista braków po przeglądzie kodu (właściciel, 2026-09-15). Legenda: 🔴 P0 (przed pilotażem/ważne UX), 🟠 P1, 🟡 P2.
Skrót stanu: **BE** = backend, **FE** = aplikacja/panel. „BE gotowe" = endpoint istnieje, brakuje tylko UI/dopięcia.

## 1. Rejestracja i konto (auth) — 🔴 P0
- 🔴 **Hasło 2×** (powtórz hasło) przy rejestracji — aplikacja mobilna (`login_screen.dart`, tryb rejestracji) **oraz** web „Załóż sklep"/„Zostań dostawcą" (`admin-panel` `app.ts`). Dziś jedno pole.
- 🔴 **Potwierdzenie e-mail (realne)**. BE ma endpointy `/api/identity/email/verify` i `/email/resend-verification` oraz wysyłkę linku — ALE sender to **`LoggingEmailSender` (mock, nic nie wysyła)**; login **NIE** wymusza `IsEmailVerified`. Trzeba: (a) realny sender SMTP (dane w `.env`, sekcja Email — dziś zakomentowane), (b) ekran „potwierdź e-mail" + „wyślij ponownie" w aplikacji, (c) decyzja: miękkie (baner) czy twarde (blokada zakupu do potwierdzenia).
- 🔴 **Zmiana hasła (klient)** — BE gotowe (`POST /api/identity/password/change`), brak UI w aplikacji (ekran „Konto" nie ma tej opcji).
- 🟠 **Edycja profilu** (imię, telefon) klienta — brak (dziś tylko podgląd).

## 2. Adresy dostaw klienta — 🔴/🟠
- 🔴 **Zapisane adresy klienta (CRUD + domyślny)**. BE: **BRAK** encji adresu klienta — checkout przyjmuje adres jako **wolny tekst** (`checkout_screen.dart`, jedno pole „ul. Przykładowa 12/3"). Trzeba: model adresu + API + wybór/dodawanie adresu w Koncie i przy checkoucie.
- 🔴 **Ustrukturyzowany adres**: osobne pola **ulica, nr domu/lokalu, kod pocztowy, miasto** (+ uwagi dla kuriera). Dziś jedno pole → brak kodu pocztowego, brak walidacji.
- 🟠 **Autouzupełnianie adresu (Google Places API)** — podpowiedzi przy wpisywaniu + geokodowanie (adres → współrzędne, do doboru sklepów wg zasięgu). Klucz **konfigurowalny w panelu admina** (zgodnie z zasadą „config w panelu, nie w env" — jak Google Client ID / captcha). Alternatywa open-source: Nominatim/OSM (P4b w roadmapie).

## 3. Dobór sklepów wg miejsca zamieszkania — 🔴 P0
- 🔴 Dziś: sklepy **sortowane wg odległości** (Haversine, `distanceKm`), ale **NIE filtrowane** po tym, czy dowożą pod adres klienta. Przy checkoucie klient **ręcznie wybiera „strefę dostawy"** — zła UX (klient nie powinien zgadywać strefy).
- 🔴 Trzeba: **model zasięgu sklepu** (obsługiwane **kody pocztowe** lub **promień + współrzędne**), **filtr „tylko sklepy dowożące pod mój adres"**, oraz **automatyczny dobór strefy + opłaty** na podstawie adresu (zamiast ręcznego dropdownu w checkoucie).

## 4. Panel sklepu (administrator sklepu) — 🟠 do weryfikacji
- 🟠 **Przejść i przetestować cały panel sklepu** (właściciel nie weryfikował). Sekcje są: Start/onboarding, Pulpit, Zamówienia, Dostawy, Oferta, Integracje, Rozliczenia.
- 🟠 Znane luki: **pole zdjęcia produktu + upload** w formularzu Oferty (dziś tylko przez API/seed); **godziny otwarcia** sklepu; **konfiguracja zasięgu/kodów pocztowych** (pkt 3); **geokoder adresu sklepu** (auto-współrzędne, P4b); NIP/edycja danych firmy w profilu sklepu (NIP zbierany przy rejestracji — brak edycji w panelu).

## 5. Panel dostawcy (kierowca) — 🟠 do zbudowania (R8)
- 🟠 Dziś: **placeholder** (nawigacja rolowa jest, treści brak). Trzeba pełny interfejs: **dostępne/moje dostawy**, akceptacja, **dostępność online/offline**, **nawigacja + mapa/ETA**, **zarobki**, oznaczanie „dostarczono". Backend workflow kierowcy istnieje (moduł Delivery) — brak UI.
- 🟠 **Przetestować** ścieżkę dostawcy end-to-end (rejestracja → akceptacja przez admina → logowanie → dostawy).

## 6. Przekrojowe / infrastruktura — 🟠
- 🔴 **Realny e-mail (SMTP)** — wspólne dla: weryfikacji e-mail, resetu hasła, powiadomień. Dane w `.env` (host lh.pl) — trzeba dopiąć sender (np. MailKit) za `IEmailSender` + przenieść config do panelu.
- 🟠 **Realne płatności Przelewy24 (sandbox)** przed wizytą (mock teraz — R5).
- 🟡 Render free tier usypia (cold start) — rozgrzać przed pokazem lub upgrade.

---
Kolejność sugerowana: **1 (hasło 2× + zmiana hasła — szybkie)** → **2+3 (adresy + zasięg — rdzeń UX zamawiania)** → **4 (audyt panelu sklepu)** → **5 (panel dostawcy)**. Punkty „BE gotowe" (zmiana hasła, weryfikacja e-mail) to najszybsze wygrane.
