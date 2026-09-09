# ZipZap — Model biznesowy i analiza funkcjonalności (vs konkurencja)

Analiza modelu (Glovo, Wolt, Bolt Food, Pyszne.pl/Uber Eats, + ekosystem PL: Restimo,
UpMenu, DeliGoo, Stava, POSbistro/GOPOS) i mapa funkcji: **co mamy / częściowo / brak**.

## 1. Model biznesowy (jak zarabiają platformy)
Trzy strony rynku: **klient · kurier · merchant (sklep/restauracja)**. Strumienie przychodu:
1. **Prowizja od merchanta** (% wartości koszyka) — podstawowy przychód (Pyszne/Uber Eats/Glovo).
2. **Opłata za dostawę** od klienta (stała lub wg dystansu) — pokrywa kuriera / marża.
3. **Opłata serwisowa** (service fee) od klienta.
4. **Subskrypcja** (Glovo Prime / Wolt+ / Uber One) — darmowa/tańsza dostawa za abonament.
5. **Reklama/promowanie** merchantów (listing, bannery, „sponsorowane").
6. **Quick‑commerce / dark stores** (Glovo) — własne mini‑magazyny (późny etap).

**ZipZap — dwa plany** (Faza H): dostawa ZipZap (stałe 25 zł, bez prowizji) vs dostawa
merchanta (prowizja). To hybryda modeli 1–2. Docelowo warto dołożyć: service fee (opcjonalnie),
promowanie, subskrypcję „ZipZap+".

**Ważne — „nie startujemy jak Glovo" (nie on‑demand).** Na starcie dostawa ZipZap
(Plan A) jeździ do lokalnych sklepów **falami o ustalonych godzinach** (np. 10:00 i 16:00,
ustawiane w panelu admina) — zamówienia grupowane, wiezione w najbliższym oknie (tańsze,
planowane trasy). **Plan B** (kurier merchanta) — to sklep decyduje: o konkretnej godzinie
lub na bieżąco. **Klient musi jasno widzieć sposób i harmonogram dostawy sklepu.**
Szczegóły: [ANALYSIS_TWO_PLANS.md](ANALYSIS_TWO_PLANS.md) §10.

## 2. Typy użytkowników i ich potrzeby
- **Klient** — odkrywanie (kategorie, wyszukiwarka, „w pobliżu", promocje), szybkie
  zamówienie, płatność (BLIK/karta), śledzenie na mapie, konto (adresy, historia), oceny,
  wsparcie/chat, powiadomienia push.
- **Kurier** — pula/przydział dostaw, nawigacja, statusy, zarobki/rozliczenia, dostępność
  (online/offline), (docelowo) optymalizacja tras (jak DeliGoo/Stava).
- **Merchant** — menu/oferta (warianty, dodatki, godziny, dostępność), zamówienia w czasie
  rzeczywistym (dźwięk/akceptacja), integracja z **POS/fiskalizacją** (GOPOS/POSbistro),
  strefa/zasięg + opłata, rozliczenia/wypłaty, statystyki.
- **Admin/operator** — sklepy, zespół, płatności, audyt, spory/zwroty, konfiguracja, moderacja.

## 3. Ekosystem integracji (z Twoich notatek)
- **Platformy zamówień** (Pyszne/Uber Eats/Glovo/Wolt/Bolt Food) ↔ **POS** (POSbistro/GOPOS)
  przez łączniki (Restimo, UpMenu). Wniosek: **merchant już ma POS** — ZipZap musi się
  z nim integrować (import menu, push zamówień, fiskalizacja), a nie zmuszać do podwójnej pracy.
- **Outsourcing dostaw** (DeliGoo, Stava) — model „dostawa jako usługa" = nasz **Plan A**
  (dostawa ZipZap). Warto rozważyć integrację/agregację flot na późnym etapie.

## 4. Mapa funkcji: konkurencja → status ZipZap
Legenda: ✅ mamy · 🟡 częściowo · ⬜ brak.

### Klient
| Funkcja | Wolt/Glovo/Pyszne | ZipZap |
|---|---|---|
| Przeglądanie sklepów bez logowania | ✅ | ✅ |
| **Kategorie + ikony** (top scroller) | ✅ | ⬜ (backend ma kategorie; brak UI) |
| **Wyszukiwarka + filtry** (cena, ocena, czas) | ✅ | ⬜ |
| **„W pobliżu" / geolokalizacja** | ✅ | ⬜ (brak lokalizacji/dystansu) |
| Karta sklepu + oferta | ✅ | ✅ |
| **Menu restauracji (warianty/dodatki)** | ✅ | ⬜ (tylko „produkty") |
| Koszyk + checkout (dostawa osobno) | ✅ | ✅ |
| Płatność **BLIK/karta** (realna) | ✅ | 🟡 (webhook‑autorytatywny + mock) |
| Śledzenie: oś statusów | ✅ | ✅ |
| **Śledzenie na mapie + ETA** | ✅ | ⬜ |
| **Oceny/recenzje** | ✅ | ⬜ |
| Konto: historia | ✅ | ✅ |
| **Zapisane adresy** | ✅ | ⬜ (płaski string) |
| Powiadomienia in‑app | ✅ | ✅ |
| **Push (FCM) realny** | ✅ | 🟡 (abstrakcja+mock) |
| **Promocje/kupony/subskrypcja** | ✅ | ⬜ |
| **Wsparcie/chat, zgłoszenie problemu** | ✅ | ⬜ |
| Logowanie Google/Apple | ✅ | 🟡 (Google za konfiguracją) |
| **Onboarding** | ✅ | ⬜ |

### Kurier
| Funkcja | Konkurencja | ZipZap |
|---|---|---|
| Pula/przyjęcie/odbiór/dostarczenie | ✅ | ✅ (w panelu) |
| **Aplikacja/PWA kierowcy** | ✅ | ⬜ (dziś tylko panel) |
| **Nawigacja/mapa** | ✅ | ⬜ |
| **Zarobki/rozliczenia kuriera** | ✅ | ⬜ |
| Dostępność online/offline | ✅ | ⬜ |

### Merchant
| Funkcja | Konkurencja | ZipZap |
|---|---|---|
| Zamówienia w czasie rzeczywistym | ✅ | ✅ (panel; brak dźwięku/akceptacji push) |
| Oferta/menu | ✅ | 🟡 (produkty; brak wariantów/menu) |
| **Godziny otwarcia / harmonogram** | ✅ | ⬜ (tylko status Open/Closed) |
| Strefa/zasięg + opłata | ✅ | 🟡 (strefy+kody; brak promienia/dystansu) |
| **Integracja POS/fiskalizacja (GOPOS)** | ✅ | ⬜ |
| Rozliczenia/prowizja | ✅ | ✅ |
| **Wypłaty (payout)** | ✅ | ⬜ |
| Statystyki/raporty | ✅ | 🟡 (pulpit podstawowy) |

## 5. Priorytetowy backlog funkcji (P0→P2)
- **P0 (domknięcie klienta):** kategorie+ikony, wyszukiwarka+filtry, onboarding, zapisane
  adresy, oceny, realna płatność (Przelewy24), realny push (FCM), obsługa błędów/zwrotów,
  **jawny sposób + harmonogram dostawy sklepu** (fale ZipZap „dziś 16:00" vs „na bieżąco").
- **P0 (merchant):** menu z wariantami/dodatkami, godziny otwarcia, akceptacja zamówienia
  (dźwięk/push), integracja POS (GOPOS) + fiskalizacja.
- **P1:** geolokalizacja + „w pobliżu" + dystans, mapa śledzenia + ETA, aplikacja/PWA kuriera,
  wypłaty, promocje/kupony.
- **P2:** subskrypcja „ZipZap+", reklama/promowanie, quick‑commerce/dark stores, agregacja flot.

## 6. Wnioski
1. **Silnik jest gotowy** (zamówienia, płatność webhook‑autorytatywna, dostawa, prowizja,
   panel, audyt). Brakuje **warstwy „konsumenckiej"** (odkrywanie, oceny, mapa, onboarding)
   i **głębi merchanta** (menu/warianty, godziny, POS, wypłaty).
2. **POS/fiskalizacja (GOPOS)** to must‑have dla restauracji w PL — bez tego merchant nie wejdzie.
3. **Realne płatności (Przelewy24/BLIK)** to bramka do prawdziwego pilota.
4. Model **dwóch planów** dobrze odpowiada rynkowi (dostawa własna vs outsourcing) — Faza H.

Powiązane: [ADVANCED_APP_ROADMAP.md](ADVANCED_APP_ROADMAP.md) · [ANALYSIS_TWO_PLANS.md](ANALYSIS_TWO_PLANS.md).
