# Wymagane jeszcze rzeczy (przegląd luk) — 2026-09-15

Lista braków po przeglądzie kodu (właściciel, 2026-09-15). Legenda: 🔴 P0 (przed pilotażem/ważne UX), 🟠 P1, 🟡 P2.
Skrót stanu: **BE** = backend, **FE** = aplikacja/panel. „BE gotowe" = endpoint istnieje, brakuje tylko UI/dopięcia.

## 1. Rejestracja i konto (auth) — 🔴 P0
- ✅ **Hasło 2×** (powtórz hasło) przy rejestracji (2026-09-15) — aplikacja mobilna (`login_screen.dart`) **oraz** web „Załóż sklep"/„Zostań dostawcą" (`app.ts`): pole „Powtórz hasło" + walidacja zgodności i min. 6 znaków.
- ✅ **Zmiana hasła (klient)** (2026-09-15) — ekran `change_password_screen.dart` (obecne+nowe+powtórz), wejście w „Konto", trasa `/change-password`, `AuthRepository.changePassword` → `POST /api/identity/password/change`.
- 🔴 **Potwierdzenie e-mail (realne)**. BE ma endpointy `/api/identity/email/verify` i `/email/resend-verification` oraz wysyłkę linku — ALE sender to **`LoggingEmailSender` (mock, nic nie wysyła)**; login **NIE** wymusza `IsEmailVerified`. Trzeba: (a) realny sender SMTP (dane w `.env`, sekcja Email — dziś zakomentowane), (b) ekran „potwierdź e-mail" + „wyślij ponownie" w aplikacji, (c) decyzja: miękkie (baner) czy twarde (blokada zakupu do potwierdzenia).
- 🟠 **Edycja profilu** (imię, telefon) klienta — brak (dziś tylko podgląd).

## 2. Adresy dostaw klienta — 🔴/🟠
- 🔴 **Zapisane adresy klienta (CRUD + domyślny)**. BE: **BRAK** encji adresu klienta — checkout przyjmuje adres jako **wolny tekst** (`checkout_screen.dart`, jedno pole „ul. Przykładowa 12/3"). Trzeba: model adresu + API + wybór/dodawanie adresu w Koncie i przy checkoucie.
- 🔴 **Ustrukturyzowany adres**: osobne pola **ulica, nr domu/lokalu, kod pocztowy, miasto** (+ uwagi dla kuriera). Dziś jedno pole → brak kodu pocztowego, brak walidacji.
- 🟠 **Autouzupełnianie adresu (Google Places API)** — podpowiedzi przy wpisywaniu + geokodowanie (adres → współrzędne, do doboru sklepów wg zasięgu). Klucz **konfigurowalny w panelu admina** (zgodnie z zasadą „config w panelu, nie w env" — jak Google Client ID / captcha). Alternatywa open-source: Nominatim/OSM (P4b w roadmapie).

## 3. Dobór sklepów wg miejsca zamieszkania — 🔴 P0
- 🔴 Dziś: sklepy **sortowane wg odległości** (Haversine, `distanceKm`), ale **NIE filtrowane** po tym, czy dowożą pod adres klienta. Przy checkoucie klient **ręcznie wybiera „strefę dostawy"** — zła UX (klient nie powinien zgadywać strefy).
- 🔴 Trzeba: **model zasięgu sklepu** (obsługiwane **kody pocztowe** lub **promień + współrzędne**), **filtr „tylko sklepy dowożące pod mój adres"**, oraz **automatyczny dobór strefy + opłaty** na podstawie adresu (zamiast ręcznego dropdownu w checkoucie).

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

---
Kolejność sugerowana: **1 (hasło 2× + zmiana hasła — szybkie)** → **2+3 (adresy + zasięg — rdzeń UX zamawiania)** → **4 (audyt panelu sklepu)** → **5 (panel dostawcy)**. Punkty „BE gotowe" (zmiana hasła, weryfikacja e-mail) to najszybsze wygrane.
