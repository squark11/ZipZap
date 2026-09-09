# ZipZap — Scenariusze testowe (prawdziwy użytkownik) + rejestr defektów

Scenariusze E2E z perspektywy realnych użytkowników + przypadki brzegowe i błędy.
Cel: wychwycić bugi funkcjonalne **oraz UI/UX**, zanim trafią do użytkownika końcowego.
Sekcja „Defekty" na końcu — uzupełniana z przeglądu na żywo.

Legenda wyniku: ✅ ok · 🐞 bug · ⚠️ UX/uwaga · ⬜ nietestowane.

## A. Klient (aplikacja Flutter)
### A1. Onboarding sesji / bootstrap
1. Pierwsze wejście → splash → lista sklepów (bez logowania). ✅
2. Reload po zalogowaniu → sesja przywrócona z secure storage. ✅
### A2. Przeglądanie i koszyk
1. Lista sklepów: status „Otwarte/Zamknięte", min. zamówienia. ✅
2. Wejście w sklep → oferta; „Dodaj" → stepper; licznik koszyka. ✅
3. Szybkie dodanie 2 różnych produktów pod rząd (wyścig) → koszyk spójny. ✅ (naprawione: serializacja)
4. Koszyk: pozycje, „Wartość produktów", „Opłata za dostawę" OSOBNO, min‑order. ✅
5. **Empty:** pusty koszyk → ilustracja + CTA. ⬜ (sprawdzić treść/ikonę)
6. Sklep bez produktów → stan pusty „Brak produktów". ⬜
### A3. Checkout
1. Poniżej min‑order → CTA zablokowane + komunikat. ✅
2. Puste adres/telefon → walidacja (snackbar). ✅
3. Wybór strefy → opłata dolicza się OSOBNO; wybór terminu. ✅
4. Sklep „nie przyjmuje" → czytelny błąd (nie generyczny 500). ✅
5. **Idempotencja:** podwójny submit → jedno zamówienie. ⬜ (klucz jest; potwierdzić UX podwójnego kliknięcia)
### A4. Płatność (webhook‑autorytatywna)
1. Ekran „Do zapłaty" + „Zapłać teraz" (redirect) + polling. ✅
2. Webhook authorized → ekran „Płatność potwierdzona". ✅
3. **Webhook failed → ekran „Płatność nieudana" + retry.** ⬜ (przetestować ścieżkę porażki)
4. Zamknięcie/utrata sieci w trakcie pollingu → wznowienie/stan. ⬜
### A5. Śledzenie
1. Oś statusów, produkty, dostawa OSOBNO, adres. ✅
2. Auto‑odświeżanie do „Dostarczone". ✅
### A6. Konto
1. Rejestracja→sesja→powrót do celu (redirect). ✅
2. Logowanie: złe hasło → 401/komunikat. ✅
3. **Odzyskiwanie hasła** → „Sprawdź skrzynkę". ✅
4. Dzwonek powiadomień: licznik + skrzynka + oznacz przeczytane. ✅
5. Wylogowanie → stan anonimowy. ⬜

## B. Merchant/Admin (panel Angular)
### B1. Zamówienia
1. Kolumna „Płatność" (Oczekuje→Opłacone) + status. ✅
2. Szczegóły: płatność + dostawa (kierowca, znaczniki). ✅
3. Przejścia: Potwierdź→Kompletuj→Gotowe (dostawa powstaje). ✅
4. **Anulowanie** zamówienia → status + (zwrot?). ⬜
### B2. Sklepy / Zespół / Rozliczenia / Dostawy
1. Utworzenie sklepu → w selektorze; edycja statusu→„Przyjmuje NIE". ✅
2. Dodanie pracownika/kierowcy; blokada konta. ✅
3. Rozliczenia: prowizja + księga. ✅
4. Dostawy: filtr statusów + kierowca + czasy. ✅
### B3. Zakres roli
1. Pracownik: tylko swój sklep; brak zakładek admina. ✅

## C. Kierowca (dziś w panelu / API)
1. Pula dostępnych → przyjmij → odbierz → dostarcz. ✅
2. **Izolacja:** kierowca B nie ruszy dostawy A (403). ✅
3. ⬜ Dedykowany interfejs kierowcy (brak — backlog).

## D. Przekrojowe / niefunkcjonalne
1. **Dostępność:** kontrast, rozmiary dotyku, etykiety, focus. ⬜
2. **Dark mode:** aplikacja renderuje poprawnie w trybie ciemnym urządzenia. ⬜
3. **Responsywność:** web desktop vs mobile; brak poziomego scrolla. ⚠️ (obserwacja skalowania w podglądzie)
4. **Formatowanie:** kwoty `pl_PL`, daty, znaczniki czasu strefy. ⚠️ (sprawdzić dd.MM vs strefy)
5. **Sieć:** wolne API / offline → skeletony/bannery, brak zawieszeń. ⬜
6. **Bezpieczeństwo:** brak tokenów w logach; refresh w secure storage. ✅

## Rejestr defektów (bug‑hunt: przegląd kodu + testy na żywo)

1. **[P1 · NAPRAWIONE] Płatność — pułapka bez wyjścia.** Przy statusie „Oczekuje"
   ekran płatności miał `automaticallyImplyLeading:false` i brak jakiejkolwiek
   nawigacji → przy mocku/porzuceniu płatności użytkownik **utykał**. *Repro:* złóż
   zamówienie → na ekranie płatności nie płać. *Naprawa:* dodano akcję
   „Zapłacę później — śledź zamówienie" (→ śledzenie zamówienia).
2. **[P2] UX — brak globalnej dolnej nawigacji.** Zamówienia/Konto dostępne tylko
   przez ikony w app‑barze; konkurencja ma bottom‑nav (Sklepy/Szukaj/Koszyk/
   Zamówienia/Konto). *Sugestia:* dodać `BottomNavigationBar` (skill §3).
3. **[P2] UI — panel formatuje po en‑US.** Kwoty z kropką („3.60 zł") zamiast
   polskiego przecinka; daty `MM‑dd`. *Sugestia:* `LOCALE_ID='pl-PL'` +
   `registerLocaleData(localePl)` w panelu Angular. (Aplikacja Flutter jest OK — `pl_PL`.)
4. **[P3] UX — checkout: CTA aktywne bez wyboru strefy/terminu.** Walidacja dopiero
   po kliknięciu (snackbar). *Sugestia:* wyłączyć przycisk, dopóki strefa+termin
   niewybrane (jak przy min‑order).
5. **[P3] UI — brak trybu ciemnego.** Aplikacja ma tylko `ZzTheme.light()`; na
   urządzeniu w dark mode zostaje jasna (bez crasha). *Backlog:* dark theme (R1).
6. **[info] Dev — mock płatności to ślepa uliczka.** Redirect na
   `localhost:4200/pay/mock` (origin panelu) nie ma strony płatności. **Oczekiwane**
   (realny dostawca ją ma); po naprawie #1 użytkownik może wyjść.
7. **[do weryfikacji] Skalowanie w podglądzie web.** Podgląd czasem renderował w
   powiększeniu — prawdopodobnie artefakt panelu podglądu; **zweryfikować na realnym
   Chrome/Androidzie** (device matrix, R10).

**Potwierdzone OK w tej sesji:** pełny lejek klienta (przeglądanie→koszyk→checkout→
płatność authorized→śledzenie), wyścig dodawania do koszyka (naprawiony), izolacja
najemcy/sklepu i kierowcy (403), webhook (podpis: zły→400, dobry→200/authorized),
zakres roli sklepowej, odzyskiwanie hasła, skrzynka powiadomień, panel (płatność+
dostawa, sklepy, zespół, rozliczenia, dostawy).

### Do dotestowania na żywo (następna sesja bug‑huntu)
Ścieżka płatności **failed** (webhook outcome=failed → „Płatność nieudana"), utrata
sieci w trakcie pollingu, stany empty (pusty koszyk/brak produktów) wizualnie,
wylogowanie, dostępność (kontrast/etykiety), dark mode na urządzeniu.
