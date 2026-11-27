# Przegląd planu „sieć sklepów" przez Codexa

Data: 2026-11-27. Plan: [`plany/siec-sklepow.md`](../plany/siec-sklepow.md). Codex CLI 0.172, tylko do odczytu,
sesja `01a21ef1-…`, 4 rundy. Decyzje właściciela (wspólna oferta sieci; strefa → najbliższa → kolejność z panelu;
wspólny harmonogram rund; stare APK → komunikat o aktualizacji; opłata 9,90 zł) wpisane w nagłówek planu.

| Runda | Werdykt | Uwagi Codexa → zmiana w planie |
|---|---|---|
| 1 | REVISE | opcja A (StoreId = lokalizacja) psuje koszyk, zamienniki, płatności i księgę → **opcja B** (StoreId = witryna, FulfillmentStoreId = lokalizacja); ukrycie lokalizacji tylko z listy nie wystarcza (szczegóły, katalog, strefy, sloty, QR); brak zakresów uprawnień sieć/lokalizacja; projekcja bez rodzica i współrzędnych; strefa bez kodów łapie wszystko; 3 decyzje do właściciela |
| 2 | REVISE | prowizja po dostarczeniu bez drogi do witryny → `Payment.StoreId`; sprzeczne uprawnienia → właściciel sieci obsługuje swoje lokalizacje (zamierzone); `OrderDto` klienta ujawniałby lokalizację → osobny `CustomerOrderDto`; publiczne dokumenty prawne lokalizacji → dokumenty witryny; adres jako struktura, warunek slot∈strefa, NOT NULL po backfillu, seed na model sieci |
| 3 | REVISE | panel czyta strefy/sloty publicznymi trasami → autoryzowany odczyt dla zakresu; `GET /catalog/products/{id}` lokalizacji → 404; seed: wyszukiwanie przez store-tree, przepięcie tylko sklepów bez historii; `ParentStoreId` niezmienne |
| 4 | **APPROVED** | „Runda 4 domyka wcześniejsze blokery"; korekta redakcyjna w pkt 7 naniesiona |
