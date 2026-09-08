# ZipZap — Advanced MVP Roadmap

Derived from `ADVANCED_MVP_AUDIT.md`. Priorities per spec §56. Order per §61.
Rule: **extend/refactor the existing modular monolith; never rebuild.** External integrations (Google, Przelewy24, FCM) are implemented as **abstraction + mock + documented credential/config step** (spec §62/§63) — no faked success paths.

---

## Priority table

| # | Feature | Current | Required | Prio | Files / Modules | Depends on |
|---|---|---|---|---|---|---|
| 1 | **Broker resilience** (inbox dedup, retry+backoff, DLQ) | in-flight | complete | P0 | BuildingBlocks (Inbox, RabbitMq subscriber) | — |
| 2 | **Tenant isolation fixes** (S1–S4) | holes | ownership enforced server-side | P0 | Ordering/Payments/Delivery endpoints, `ICurrentUser` | — |
| 3 | **Unified error envelope + traceId + global handler** | partial | `{code,message,traceId}` | P0 | Api, BuildingBlocks | — |
| 4 | **Correlation/structured logging + readiness** | none | trace ids, `/health/ready` | P0 | Api | 3 |
| 5 | **Identity hardening** (logout/revoke, password change/reset, email verify, block, profile, rate limit) | basic | production | P0/P1 | Identity | — |
| 6 | **Google OAuth/OIDC login** | none | standard code flow | P0 | Identity, Api | 5 |
| 7 | **Store richness** (status OPEN/CLOSED/UNAVAILABLE, hours, phone, logo, min-order, delivery cfg) | isActive only | full | P0/P1 | Catalog | — |
| 8 | **Checkout hardening** (min-order, open-hours, zone check, **idempotency key**, structured address) | partial | complete | P0 | Ordering | 7 |
| 9 | **Saved delivery addresses** | flat string | CRUD + default | P0 | Ordering (or Identity/Profile) | — |
| 10 | **Payment provider abstraction** `IPaymentProvider` + **MockProvider** | inline mock | ports + session model | P0 | Payments, Contracts | — |
| 11 | **Payment session + webhook** (verify, idempotency, dup handling, state machine) | none | webhook-authoritative | P0 | Payments, Api | 10 |
| 12 | **Real provider (Przelewy24) behind abstraction** + BLIK/card via PSP | none | integrated (sandbox) | P0 | Payments | 11 (+creds) |
| 13 | **Payment ledger** (customer pay, delivery fee, commission, provider fee, refund) | commission only | auditable | P1 | Payments | 10 |
| 14 | **Refunds** (full/partial, provider call) | none | architecture + provider | P1 | Payments | 12,13 |
| 15 | **Driver workflow** (my/assigned deliveries, assign, accept, pickup, deliver, isolation) | passive | driver-owned | P0 | Delivery, Contracts, Ordering | 2 |
| 16 | **Notifications** (recipient resolution, dedup via inbox, in-app fetch, **FCM abstraction+mock**, email abstraction) | no-op | usable | P0/P1 | Notifications | 1 |
| 17 | **Flutter customer app** (auth incl. Google, stores→product→cart→checkout→pay→track, profile, error/empty/offline states) | absent | production-quality | P0 | mobile/ | 5,6,8,10,11 |
| 18 | **Admin panel ops** (users, stores, employees, orders detail, payments, refunds, drivers, deliveries, audit, settings) | partial | operational | P0/P1 | admin-panel | 2,5,7,13 |
| 19 | **Store panel scope** (store-role users see only their store) | admin-only | scoped | P1 | admin-panel, Identity | 2,5 |
| 20 | **Employee management** (invite/assign/remove/disable/role) | admin/users only | full | P1 | Identity, Catalog | 5 |
| 21 | **Audit log** (admin actions) | none | actor/action/entity/ts | P1 | new `Audit` module or BuildingBlocks | 3 |
| 22 | **DB hardening** (indexes for lookups, unique constraints, review) | partial | production | P0/P1 | all modules migrations | — |
| 23 | **Tests** (unit: pricing/commission/fee/state/authz/payment-state; integration: checkout/webhook/tenant/authz) | Ordering only | broad | P0/P1 | tests | features |
| 24 | **Ratings** (order/store/delivery) | none | basic | P1 | Ordering or new module | 8 |
| 25 | **Config/secrets split** (dev/staging/prod, env-only secrets, docs) | dev-inline | production | P0 | Api, compose, docs | 3 |
| 26 | **Docs** (PRODUCTION_SETUP, PAYMENTS, DEPLOYMENT + README sync) | README only | complete | P1 | root docs | 6,12,17,25 |

**P2 (do not build now, keep architecture open):** auto-dispatch, live GPS, loyalty, analytics, dynamic pricing, Kafka cluster, multi-region, warehouse mgmt, AI recommendations.

---

## Sequenced implementation plan (with build/test gates)

**Phase A — Hardening foundation (P0, no external creds)**
A1. Complete **broker resilience** (#1) → build+test.
A2. **Tenant isolation** fixes S1–S4 (#2) + add authz tests → build+test.
A3. **Error envelope + traceId + global exception handler + correlation logging + readiness** (#3,#4) → build.
Gate: build green, tests green, Docker smoke.

**Phase B — Identity for pilot (P0)**
B1. Logout/revoke, password change/reset (token), email verification (token), account block/delete, profile, rate limiting (#5).
B2. **Google OIDC** login behind config (client id/secret via env; mock/test path documented) (#6).
Gate: auth integration tests.

**Phase C — Catalog & checkout correctness (P0)**
C1. Store status/hours/phone/logo/min-order/delivery cfg (#7) + events + migration.
C2. Checkout: min-order, open-hours, zone validation, **idempotency key**, structured address + saved addresses (#8,#9).
Gate: checkout unit+integration tests (price-changed, unavailable, bad zone, closed store, dup checkout).

**Phase D — Payments for real (P0, needs sandbox creds)**
D1. `IPaymentProvider` + `MockPaymentProvider` + payment **session** model + payment **state machine** (#10).
D2. **Webhook** endpoint (signature verify, idempotency, dup handling) — provider-agnostic (#11).
D3. **Przelewy24** provider (sandbox) behind abstraction; BLIK/card via PSP; document credentials (#12).
D4. **Ledger** (#13) + **refunds** architecture (#14).
Gate: payment-state tests, webhook dup test, mock end-to-end; document remaining sandbox credentials.

**Phase E — Delivery & notifications (P0)**
E1. Driver-owned deliveries: my/assigned, assign (admin/store), accept, pickup, deliver, **isolation** (#15) + events; reconcile with Ordering state machine.
E2. Notifications: recipient resolution, dedup, in-app fetch, **FCM abstraction+mock**, email abstraction (#16).
Gate: driver isolation tests; notification dedup test.

**Phase F — Clients (P0)**
F1. **Flutter customer app** full flow (#17) — needs B,C,D APIs.
F2. **Admin panel** ops sections + store scope (#18,#19,#20).
Gate: manual end-to-end pilot walkthrough.

**Phase G — Production polish (P0/P1)**
G1. DB indexes/constraints review (#22), config/secrets split (#25), audit log (#21), ratings (#24), docs (#26), broad tests (#23).
Gate: final quality gate (audit §69).

---

## Dependencies (critical path)
`#1 → #2/#3/#4` → `#5 → #6` · `#7 → #8/#9` · `#10 → #11 → #12 → #13/#14` · `#2 → #15` · `#1 → #16` · `(#5,#6,#8,#10,#11) → #17` · `(#2,#5,#7,#13) → #18`.

## Risks
- **R1 External credentials** (Przelewy24 sandbox, Google client, FCM) block full end-to-end; mitigate with mock provider + documented config; do not fake success.
- **R2 Flutter scope** is large; mitigate by shipping the vertical customer happy-path + core error states first, then breadth.
- **R3 Payment↔Ordering coupling** — keep Ordering depending only on Payments **events/contracts**, never a provider SDK (§54).
- **R4 Migration drift** across 7 schemas — generate migrations per module, apply on startup, verify in Docker after each phase.
- **R5 Tenant-isolation regressions** — add authorization integration tests as a guardrail (#23) alongside #2.
- **R6 Time/budget** — this is multi-session; each phase ends build+test green and is committed independently.

## First execution step
Complete **Phase A1 (broker resilience)** now — the code is already in flight — then commit A1 with the audit + roadmap, and proceed to A2 (tenant isolation).
