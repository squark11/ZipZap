# Przegląd planu E3-4 przez Codexa (codex-review:plan)

Data: 2026-11-27. Plan: [`plany/e3-4-checkout.md`](../plany/e3-4-checkout.md). Codex CLI 0.172, tryb tylko do odczytu, jedna sesja (`01a21eac-…`), 10 rund.

| Runda | Werdykt | Uwagi Codexa → zmiana w planie |
|---|---|---|
| 1 | REVISE | moment odczytów providerów (autoDispose) → obserwacja na poziomie ekranu; nieaktualne id slotu → bramka aktualności 2.10; 409 bez rozróżnienia przyczyn → bez zgadywania, zostaje krok 3; niepewna wysyłka i podwójne dotknięcie → 2.11; stany pusty/błąd/offline i przejście E2E; redakcja danych w śladach CDP |
| 2 | REVISE | 5xx po zatwierdzonej transakcji też niepewne → blokada edycji poza 4xx; slot pełny i stary wynik w trakcie odświeżania → pojemność + brak odświeżania w bramce; spójność celu z nowymi bramkami; dosłowne porównanie `expectedRoundStartsAtUtc` |
| 3 | REVISE | bramka dla odświeżanej rundy po 409; reset bazy przed każdym przebiegiem porównania |
| 4 | REVISE | `!hasError` w bramkach (Riverpod 2.6 zachowuje poprzednią wartość przy błędzie) |
| 5 | **APPROVED** | „Nie widzę już blokujących braków; plan jest gotowy do wdrożenia." |
| 6 | REVISE | (po wymaganiu właściciela „bez przewijania") wstępny adres = nowy odczyt API; dopasowanie kodu do strefy niemożliwe (DTO bez kodów, serwer nie sprawdza); wyjątek klawiatura/130 % wymaga akceptacji; brak najgorszych przypadków → usunięte wypełnianie adresu, limity opcji, testy |
| 7 | REVISE | auto-wybór jedynej strefy może wskazać strefę nieobsługującą adresu; arkusze i przewijanie dni w bok to też przewijanie → wybór strefy ręczny, wszystkie opcje w siatkach bez gestów |
| 8 | REVISE | wyjątek 130 % wymaga akceptacji właściciela; brak pełnego rachunku wysokości kroku 1 → właściciel zaakceptował wyjątek (2026-11-27), rachunek 388/428 px, limit 4 strefy, test „Dalej" przez pola przy 130 % |
| 9 | REVISE | `TestOrderBanner` nie mieści się w budżecie kroku 1 → etykieta w pasku postępu, pełny baner tylko dla zablokowanego |
| 10 | **APPROVED** | „Nie widzę już blokujących luk względem nowych wymagań ani niezmienników żądania, walidacji i bramek 2.10/2.11." |

Pełne odpowiedzi Codexa z rund 1–10 zostały przeczytane w sesji Claude Code; ten plik streszcza je bez zmiany sensu.
