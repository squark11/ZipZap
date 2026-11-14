# B. Koncepcja nowego UI/UX

## Co bierzemy z inspiracji („Foodie"), a czego nie
Bierzemy **strukturę i nastrój**, nie wygląd 1:1:
- kolorowy, zaokrąglony **nagłówek z adresem dostawy** („Dostawa do: Dom, ul. …") zamiast dwóch banerów,
- **baner promocyjny** z ilustracją, pod nim **kategorie w kółkach**, „Zamów ponownie", oferty, sklepy w pobliżu,
- **karty produktów w siatce 2 kolumn**: duże zdjęcie, nazwa, cena, okrągły przycisk „+",
- ekran logowania z ilustracją w górnej połowie i formularzem na białej, zaokrąglonej karcie,
- animowany ekran startowy z maskotką-dostawcą, dolna nawigacja z 5 pozycjami, przyciski-pigułki.

Nie bierzemy: zielonej palety (mamy własną markę), kalorii i czasu przygotowania (to sklepy spożywcze, nie
restauracje), obietnicy „szybko i za darmo" (dowozimy w rundach o stałych godzinach — klient musi widzieć *kiedy*),
trójwymiarowych renderów jedzenia (nie wytworzymy ich spójnie; zastąpią je zdjęcia sklepów i płaskie ilustracje SVG).

## Kierunek: „Świeżo i po sąsiedzku"
Ciepła, lekka aplikacja osiedlowa: dużo bieli, turkus jako kolor marki, jeden ciepły akcent, miękkie kształty,
krótkie zdania pisane po ludzku („Co dziś kupujemy?", „Koszyk czeka").

### Kolory (propozycja — decyzja F-2)
| Rola | Kolor | Uwaga |
|---|---|---|
| Marka | `#14B9BA` turkus | tła, ilustracje, aktywne stany — **nie** pod biały tekst |
| Marka mocna | `#0A7F80` | wypełnienie przycisków i tekst-akcent (kontrast z bielą ok. 4,8:1) |
| Marka jasna | `#E6F7F7` | tła chipów, podświetlenia |
| Akcent ciepły | `#FFB703` „słoneczny" | promocje, wyróżniki, drobne ilustracje (z ciemnym tekstem) |
| Sukces / błąd / uwaga | `#16A34A` / `#DC2626` / `#D97706` | tylko komunikaty i statusy |
| Tekst / tekst pomocniczy | `#1F2A37` / `#5B6675` | |
| Tło ekranu / karta / obrys | `#F3F8F8` / `#FFFFFF` / `#E3EBEB` | neutralne z lekkim odcieniem turkusu |
Tryb ciemny zostaje, z tymi samymi rolami.

### Typografia
**Poppins** (nagłówki 600–700) + **Inter** (treść) — bez zmian, bo pasują do inspiracji i marki. Zmiana techniczna:
pliki czcionek w paczce aplikacji zamiast pobierania z Google. Skala: 28 / 22 / 18 / 16 / 14 / 12.

### Kształt i głębia
Promienie 12 (pola) · 20 (karty) · 28 (panele, nagłówek) · pigułka (przyciski, chipy). Cienie miękkie, dwa
poziomy; obrys tylko tam, gdzie nie ma cienia. Odstępy 4/8/12/16/24/32. Cele dotykowe ≥ 48 px.

### Komponenty (wspólne, na tokenach)
Przycisk (główny/pomocniczy/tekstowy/ikona, stany: wciśnięty, ładowanie, nieaktywny) · pole formularza z ikoną
i komunikatem błędu · pasek wyszukiwania · chip kategorii (kółko z ikoną + podpis) · karta sklepu (okładka 16:9,
logo, nazwa, „Dostawa dziś 16:00", min. zamówienie, status) · karta produktu (siatka) i wiersz produktu (lista) ·
licznik ilości · pasek koszyka · arkusz dolny i okno dialogowe · komunikat (sukces/błąd/informacja) · oś statusu
zamówienia · stany pusty/błąd/offline z ilustracją · szkielety · dolna nawigacja (telefon) i boczna (≥ 900 px).

### Nawigacja i ekrany
Dół: **Start · Szukaj · Koszyk · Zamówienia · Konto**. Start: nagłówek z adresem → szukajka → baner → kategorie →
zamów ponownie → sklepy. Sklep: okładka, chipy informacji, przyklejone zakładki kategorii, siatka produktów,
pasek koszyka. Checkout: 3 kroki (Adres → Termin → Podsumowanie) z paskiem postępu. Na komputerze: treść do
1100 px, 3–4 kolumny kart, koszyk jako panel boczny.

### Grafiki i animacje
- **Ilustracje SVG własne** (płaskie, turkus + akcent): maskotka-dostawca na rowerze z wózkiem (start,
  logowanie, sukces), 3 plansze onboardingu, 6 stanów (pusty koszyk, brak sklepów, brak zamówień, błąd, offline,
  sukces), 10–12 ikon kategorii. Bez stocków.
- **Animacje w samym Flutterze** (bez nowych bibliotek): start (logo + maskotka, ≤ 1,2 s), „+" → licznik,
  podskok plakietki koszyka, płynne przejścia zakładek, połysk szkieletów, „odhaczenie" po złożeniu zamówienia.
  Wszystko ≤ 250 ms, wyłączane przy „ogranicz ruch" w systemie.
- Lottie/Rive tylko jeśli zaakceptujesz dodatkową bibliotekę (decyzja F-4); nie jest konieczna.
- **Figma**: integracja jest teraz niezalogowana (wymaga ponownej autoryzacji przez `/mcp`), a konto Starter ma
  limit kilku wywołań. Makiety Etapu 1 powstaną więc jako pliki SVG/HTML w repo, a do Figmy trafią po
  odblokowaniu (decyzja F-3).
