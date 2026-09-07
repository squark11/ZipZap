<!-- markdownlint-disable MD033 -->
<p align="center">
  <img src="branding/zipzap-logo-carrot.svg" alt="ZipZap" width="220" />
</p>

# ZipZap

Marketplace do zamawiania zakupów z **lokalnych sklepów** z dostawą.
Model: sklep płaci **prowizję** od wartości koszyka, klient płaci **osobną opłatę za dostawę**.

> **Start celowo mały:** 1 miejscowość, 2–3 sklepy, 1–2 kierowców.
> Architektura zaprojektowana pod skalę (tysiące sklepów, miliony użytkowników),
> ale **bez przedwczesnej infrastruktury** — skalujemy bez przepisywania od zera.

---

## Architektura (skrót)

Backend to **modularny monolit** na .NET 8 — jeden deployment, twarde granice modułów.

| Moduł | Odpowiedzialność | Status MVP |
|---|---|---|
| **Identity** | Użytkownicy, JWT + refresh, role (RBAC) | 🟡 Etap 2 |
| **Catalog** | Sklepy, kategorie, produkty | 🟡 Etap 3 |
| **Ordering** | Koszyk, zamówienie, statusy, strefy + sloty | 🟡 Etap 4 |
| **Delivery** | Kierowca, odbiór/dostawa | ⚪ Szkielet |
| **Payments** | Płatności, prowizja | ⚪ Szkielet |
| **Notifications** | Powiadomienia (reakcja na zdarzenia) | ⚪ Szkielet |
| **Integrations** | Porty: kasy fiskalne / POS (Novitus, Elzab, Posnet, Comarch) | ⚪ Tylko porty |

**Zasady granic:**
- Każdy moduł ma **własny schemat PostgreSQL** + własny `DbContext` + własne migracje.
- **Brak** cross-schema FK i wspólnych tabel; odwołania do innych modułów **tylko po ID**.
- Komunikacja **wyłącznie** przez zdarzenia integracyjne na in-process **event busie**
  (do wymiany na broker bez zmian w modułach).
- **Outbox pattern** — zdarzenia zapisywane w tej samej transakcji co zmiana domenowa
  (niezawodna zmiana statusu zamówienia).
- **Multi-tenancy od 1. dnia** — każda encja niesie `StoreId` (row-level).

```
ZipZap.Api (host: JWT, RBAC, Swagger, DI)
   ├── Modules: Identity · Catalog · Ordering · Delivery · Payments · Notifications · Integrations
   └── BuildingBlocks: IEventBus (in-process) · Outbox dispatcher · Tenant/User context · Result/Error
                       PostgreSQL (schemat-per-moduł)
```

Szczegółowy diagram modułów i przepływ zdarzeń zamówienia — dodawany w etapie 5.

## Struktura repo (monorepo)

```
/backend        .NET 8 — modularny monolit (API + moduły + testy)
/admin-panel    Angular — panel sklepu/kierowcy/administratora (etap 5)
/mobile         Flutter — aplikacja klienta (osobny etap)
/branding       logo, paleta kolorów, typografia
docker-compose.yml
```

## Uruchomienie lokalne

### Wymagania
- **Docker** (wystarczy do zbudowania i uruchomienia backendu + PostgreSQL).
- Opcjonalnie: **.NET 8 SDK** (do developmentu/testów bez Dockera).

### Szybki start (Docker)
```bash
cp .env.example .env        # opcjonalnie — domyślne wartości działają out-of-the-box
docker compose up --build
```
- API: http://localhost:5080 — health: http://localhost:5080/health, Swagger: http://localhost:5080/swagger
- PostgreSQL: `localhost:5432` (db/user/pass: `zipzap` / `zipzap` / `zipzap`)

### Development bez Dockera (wymaga .NET 8 SDK)
```bash
docker compose up -d postgres          # sama baza
cd backend
dotnet run --project src/ZipZap.Api
```

## Marka
Kolory: pomarańcz `#F97316`, zieleń `#22C55E`, grafit `#3A3F4B`.
Zasoby: [`/branding`](branding/README.md).

## Roadmapa MVP
1. ✅ Fundament: struktura, BuildingBlocks (event bus + outbox), docker-compose, branding
2. ⏳ Identity — rejestracja/logowanie, JWT + refresh, role
3. ⏳ Catalog — sklepy, kategorie, produkty
4. ⏳ Ordering — koszyk, zamówienie, statusy, strefy + sloty, prowizja + testy
5. ⏳ Szkielety (Delivery/Payments/Notifications/Integrations) + panel Angular + diagram w README
