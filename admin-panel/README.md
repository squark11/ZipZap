# ZipZap — Admin Panel (Angular)

Minimalny panel webowy (Angular 20, standalone) dla ról **ADMIN / STORE_EMPLOYEE / DRIVER**.
Na start: **logowanie (JWT)** + **lista zamówień sklepu** (wybór sklepu, status, suma, prowizja, opłata za dostawę).

## Uruchomienie
Wymaga działającego backendu (`docker compose up` w katalogu głównym — API na `http://localhost:5080`).

```bash
cd admin-panel
npm install        # tylko za pierwszym razem
npm start          # ng serve -> http://localhost:4200
```

Zaloguj się domyślnym adminem (dev): `admin@zipzap.local` / `Admin123!`.

## Uwagi
- Adres API: `http://localhost:5080/api` (stała `apiBase` w `src/app/app.ts`).
- Backend ma włączony CORS w środowisku Development.
- Kolory/typografia wg `/branding`.
- To celowo minimalny szkielet UI — kolejne ekrany (kompletacja zamówień, oferta, dostawy) w następnych iteracjach.
