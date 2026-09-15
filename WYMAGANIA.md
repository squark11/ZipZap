# Wymagane jeszcze rzeczy (przegląd luk) — 2026-09-15

Lista braków po przeglądzie kodu (właściciel, 2026-09-15). Legenda: 🔴 P0 (przed pilotażem/ważne UX), 🟠 P1, 🟡 P2.
Skrót stanu: **BE** = backend, **FE** = aplikacja/panel. „BE gotowe" = endpoint istnieje, brakuje tylko UI/dopięcia.

## 1. Rejestracja i konto (auth) — 🔴 P0
- ✅ **Hasło 2×** (powtórz hasło) przy rejestracji (2026-09-15) — aplikacja mobilna (`login_screen.dart`) **oraz** web „Załóż sklep"/„Zostań dostawcą" (`app.ts`): pole „Powtórz hasło" + walidacja zgodności i min. 6 znaków.
- ✅ **Zmiana hasła (klient)** (2026-09-15) — ekran `change_password_screen.dart` (obecne+nowe+powtórz), wejście w „Konto", trasa `/change-password`, `AuthRepository.changePassword` → `POST /api/identity/password/change`.
- 🔴 **Potwierdzenie e-mail (realne)**. BE ma endpointy `/api/identity/email/verify` i `/email/resend-verification` oraz wysyłkę linku — ALE sender to **`LoggingEmailSender` (mock, nic nie wysyła)**; login **NIE** wymusza `IsEmailVerified`. Trzeba: (a) realny sender SMTP (dane w `.env`, sekcja Email — dziś zakomentowane), (b) ekran „potwierdź e-mail" + „wyślij ponownie" w aplikacji, (c) decyzja: miękkie (baner) czy twarde (blokada zakupu do potwierdzenia).
- 🟠 **Edycja profilu** (imię, telefon) klienta — brak (dziś tylko podgląd).

## 2. Adresy dostaw klienta — 🔴/🟠
- ✅ **Zapisane adresy klienta (CRUD + domyślny)** (2026-09-15) — encja `CustomerAddress` (Ordering) + migracja, `OrderingService` CRUD per `ICurrentUser`, endpointy `/api/ordering/addresses` (+`/{id}/default`). Aplikacja: **Konto → „Moje adresy"** (lista/dodaj/edytuj/usuń/domyślny). ✅ **Ustrukturyzowany adres** (ulica, nr domu/lokalu, **kod pocztowy** z walidacją PL, miasto, uwagi). Zweryfikowane na żywo (API).
- 🔴 **Wpiąć adres w checkout** — dziś checkout ma **wolne pole tekstowe** (`checkout_screen.dart`); trzeba: wybór zapisanego adresu / dodanie nowego + przekazanie ustrukturyzowanego adresu do zamówienia (dziś `deliveryAddress` = string).
- 🟠 **Autouzupełnianie adresu (Google Places API)** — podpowiedzi + geokodowanie (adres → współrzędne, do zasięgu). Klucz **konfigurowalny w panelu** (jak Google Client ID / captcha). Alternatywa: Nominatim/OSM (P4b).

## 3. Dobór sklepów wg miejsca zamieszkania — 🔴 P0
- ✅ **Backend (2026-09-15):** naprawiono zapis **kodów pocztowych stref** (były `Ignore` w EF → teraz kolumna `text[]`, migracja `Ordering_ZonePostalCodes`); endpoint **`GET /api/stores/serving?postalCode=&lat=&lng=`** zwraca tylko sklepy z aktywną strefą obejmującą kod (pusta lista kodów strefy = obsługuje wszędzie). Zweryfikowane na żywo: sklep ze strefą „31-042" widoczny dla 31-042, niewidoczny dla 99-999; bez kodu → wszystkie.
- ✅ **Frontend (aplikacja) (2026-09-15):** baner **kodu pocztowego** na ekranie sklepów (`stores_screen.dart` → `_PostalBanner`) z dialogiem wpisania/zmiany/wyczyszczenia kodu, **auto-uzupełnianie z domyślnego adresu klienta** (jednorazowo po zalogowaniu), `catalog_repository.listStores(postalCode:)` → woła `/stores/serving` gdy 5 cyfr, komunikat „Brak sklepów dowożących" gdy pusto. Analiza czysta; PWA zbudowana. ⏳ **Deploy Netlify oczekuje** (brak `NETLIFY_AUTH_TOKEN` na maszynie — patrz niżej).
- 🟠 **Auto-dobór strefy + opłaty w checkoucie** na podstawie kodu adresu (zamiast ręcznego dropdownu). Panel sklepu: pokazać zapisane kody strefy (DTO `DeliveryZoneDto` nie eksponuje jeszcze `postalCodes`).

## 4. Panel sklepu (administrator sklepu) — 🟠 (audyt 2026-09-15)
Sekcje: Start, Pulpit, Zamówienia, Dostawy, Oferta, Integracje, Rozliczenia. Wyniki przeglądu kodu:
- **Start (onboarding.ts):** ✅ checklista gotowości, ✅ strefy (z **kodami pocztowymi** — pole jest!), ✅ terminy/sloty, ✅ publikacja. 🔴 **Profil sklepu edytuje TYLKO logo-URL / GPS / status** — brak edycji **nazwy, opisu, adresu, telefonu, miasta, NIP** oraz **godzin otwarcia**. 🟠 Logo tylko jako URL (**brak uploadu pliku**). 🟠 GPS ręczne (**brak geokodera** adres→współrzędne, P4b).
- **Oferta (catalog.ts):** ✅ lista, dodawanie (nazwa/cena/jednostka/kategoria), ukryj/pokaż, zmiana ceny (prompt), ✅ import/eksport CSV. 🔴 **Brak pola zdjęcia produktu i uploadu** (dziś tylko przez API/seed; CSV też bez zdjęcia). 🟠 Edycja produktu ograniczona (tylko cena+dostępność) — brak edycji nazwy/jednostki/kategorii/opisu, **brak usuwania**, **brak zarządzania stanem magazynowym** (`stockQty` w modelu, brak UI).
- **Integracje (integrations.ts):** ✅✅ **podpięcie bramki płatniczej** (Przelewy24/Stripe, Merchant/POS ID, sandbox, klucz API+CRC — **szyfrowane, write-only, maskowane**) — spełnia wymóg „Rapacz łatwo podpina bramkę". ✅ dokumenty prawne (regulamin/polityka/RODO + wymóg akceptacji). 🔴 **Realny adapter bramki NIE podpięty** — płatność wciąż mock do R5 (odszyfrowanie i użycie tokenów). 🟡 Brak „testuj połączenie".
- **Zamówienia / Dostawy / Rozliczenia / Pulpit:** obecne z MVP (zweryfikowany live: Pulpit pokazuje metryki + listę dostaw). 🟠 **Do funkcjonalnego przetestowania** na koncie sklepu (nie audytowane liniowo).

## 5. Panel dostawcy (kierowca) — 🔴 (audyt 2026-09-15)
- ✅ **REGRESJA naprawiona (2026-09-15):** dostawca ma teraz własną trasę **„Moje dostawy"** (`driver-home.ts`, placeholder), a role `Driver` usunięto z Pulpitu/Dostaw (komponenty sklepowe). Guard przekierowuje dostawcę na jego stronę. Docelowo → pełne komponenty (niżej).
- 🟠 **Brak jakiegokolwiek widoku dostawcy** (R8): dostępne/moje dostawy, akceptacja, **online/offline**, **mapa/nawigacja/ETA**, **zarobki**, „dostarczono". Backend (moduł Delivery, workflow kierowcy) istnieje — brakuje UI + tras/komponentów dla roli Driver.
- 🟠 **Przetestować** ścieżkę: rejestracja dostawcy → zatwierdzenie przez admina → logowanie → dostawy.

## 6. Przekrojowe / infrastruktura — 🟠
- 🔴 **Realny e-mail (SMTP)** — wspólne dla: weryfikacji e-mail, resetu hasła, powiadomień. Dane w `.env` (host lh.pl) — trzeba dopiąć sender (np. MailKit) za `IEmailSender` + przenieść config do panelu.
- 🟠 **Realne płatności Przelewy24 (sandbox)** przed wizytą (mock teraz — R5).
- 🟡 Render free tier usypia (cold start) — rozgrzać przed pokazem lub upgrade.

## 7. Wiele jednostek produktu — ✅ ZROBIONE (2026-09-16)
- ✅ Produkt może mieć **kilka jednostek sprzedaży** (np. jabłka: kg 4,99 / szt 1,20); **klient wybiera** przy dodawaniu (bottom sheet). Backend (Catalog `UnitOptionsJson` + event/read-model, Ordering `AddItemAsync(unit)` autorytatywnie rozwiązuje cenę, `CartItem.Unit`/`OrderItem.Unit`), panel (dodatkowe jednostki w formularzu oferty), aplikacja (wybór + spójna jednostka w koszyku). Naprawia niespójność kg/szt. Wdrożone: backend Render, APK, panel; seed jabłek zaktualizowany.
- ✅ **Ilość w koszyku z klawiatury** (dotknięcie liczby → pole numeryczne) obok +/−.
- ✅ **Skeletony** (placeholdery ładowania) — wyraźny odcień + „pigułki", nie mylą się z tekstem.

## 8. Panel administratora serwisu (właściciel) — 🔴 NOWY EPIK (2026-09-16)
Wymagania właściciela (admin serwisu = Kacper, kacpermackowiak256@gmail.com):
- 🔴 **E-mail z danymi logowania do panelu admina** na jego adres. Wymaga realnego SMTP (niżej).
- 🔴 **Zmiana hasła admina w panelu** (dziś zmiana hasła jest tylko w app kliencie — `POST /identity/password/change`; dodać ekran w panelu).
- 🔴 **2FA / uwierzytelnianie przez authenticator (TOTP)** dla konta admina — nowość (generowanie sekretu + QR + weryfikacja kodu przy logowaniu).
- 🔴 **Zapisane dane logowania testowych kont** (klient / dostawca / sklep) widoczne w koncie admina — podgląd danych demo do testów.
- 🔴 **Aktywacja/dezaktywacja kont** z panelu admina (jest już `Deactivate` dla dostawcy — rozszerzyć na wszystkie role + UI listy kont).
- 🔴 **Brak wglądu admina w dane sklepu** (statystyki/produkty) — CHYBA że sklep udostępni **„kod klienta" (kod wsparcia)**. Każdy sklep ma unikalny kod, który podaje przy zgłoszeniu pomocy; admin wpisuje kod → dostaje czasowy wgląd. Model: `Store.SupportCode` + endpoint „wejdź z kodem".
- **SMTP (lh.pl):** host `mail-serwer325339.lh.pl`, port **465 (SSL)**; **login/hasło wpisywane w panelu admina** (config w panelu, nie env — [[config-in-panel-not-env]]). Realny sender (MailKit) za `IEmailSender` — wspólny dla: e-mail z danymi logowania, weryfikacji e-mail (sekcja 1), resetu hasła, powiadomień.

---
Kolejność sugerowana: **1 (hasło 2× + zmiana hasła — szybkie)** → **2+3 (adresy + zasięg — rdzeń UX zamawiania)** → **7 ✅** → **8 (epik admina: SMTP → e-mail danych → zmiana hasła w panelu → 2FA → kody wsparcia → aktywacja kont)** → **4 (audyt panelu sklepu)** → **5 (panel dostawcy)**. Punkty „BE gotowe" (zmiana hasła, weryfikacja e-mail) to najszybsze wygrane.
