# F. Decyzje wymagające akceptacji właściciela

| # | Decyzja | Rekomendacja | Skutek odmowy |
|---|---|---|---|
| F-1 | Zostajemy przy Flutterze jako PWA (zamiast pisać osobny front w HTML) | **Tak** — jeden kod na web, Androida i iOS; start odchudzamy | osobny front to 2–3 miesiące i dwa kody do utrzymania |
| F-2 | Paleta: turkus marki + ciemniejszy turkus na przyciski + ciepły żółty akcent | **Tak**; alternatywa: zieleń jak w inspiracji, czyli zmiana marki, logo i strony głównej | bez ciemniejszego turkusu przyciski nie spełnią wymogu kontrastu |
| F-3 | Figma: ponowna autoryzacja integracji i ewentualnie płatny plan (Starter kończy limit po kilku wywołaniach) | makiety jako HTML/SVG w repo; Figma opcjonalnie | brak pliku w Figmie, reszta bez zmian |
| F-4 | Dodatkowa biblioteka animacji (Lottie/Rive) | **Nie teraz** — wystarczą animacje Fluttera | — |
| F-5 | Okładka sklepu oraz „sposób i termin dostawy" na karcie (nowe pola w API i panelu) | **Tak** — bez tego karty sklepów zostaną bez zdjęć | wariant z samym logo |
| F-6 | Wspólna lista kategorii platformy (Pieczywo, Nabiał, Owoce…) obok kategorii sklepu | **Tak**, 10–12 pozycji zarządzanych w panelu | kategorie tylko wewnątrz sklepu |
| F-7 | Wyszukiwarka produktów we wszystkich sklepach (nowy endpoint) oraz ulubione | wyszukiwarka **tak** (Etap 3), ulubione później | szukanie tylko sklepów po nazwie |
| F-8 | Baner promocyjny i „Zamów ponownie" — skąd treść | „Zamów ponownie" z historii zamówień; baner edytowany w panelu | sekcje pominięte |
| F-9 | Hosting API: płatny Render (ok. 7 USD miesięcznie), koniec usypiania | **Tak** przed publicznym startem — największy pojedynczy zysk dla pierwszego wrażenia | komunikat „Budzimy serwer…" i 30–60 s czekania |
| F-10 | Redesign panelu Angular osobno, po aplikacji klienta | **Tak** | — |
| F-11 | Obieg z Codexem: raporty w `docs/redesign/raporty/`, praca na gałęzi `redesign/*`, scalenie do `main` po PASS | **Tak**; potrzebne potwierdzenie, czy Codex czyta lokalne repo, czy GitHub | — |
| F-12 | Ilustracje (maskotka, stany, kategorie) rysowane w SVG przez Claude Code, do akceptacji przed użyciem | **Tak**; alternatywa: zlecenie grafikowi | — |
