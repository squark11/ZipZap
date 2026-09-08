# ZipZap — Advanced MVP Audit

**Date:** 2026-09-08 · **Against:** ZipZap Advanced Production MVP specification
**Method:** direct inspection of the repository at commit `3d6396f` (+ uncommitted broker-resilience WIP).

Legend: ✅ done / production-shaped · 🟡 partial · 🔴 missing · ⚠️ architectural deviation or security gap · 🚫 not started

---

## 0. Executive summary

The **backend architecture is sound and matches the spec's intent**: .NET 8 modular monolith, schema-per-module PostgreSQL, integration events over a **replaceable** bus (in-process **and** RabbitMQ already implemented), **outbox** pattern, multi-tenancy via `StoreId`, JWT+refresh RBAC, clean `ZipZap.Contracts` published language. The **order domain is the strongest part** (price snapshots, explicit commission vs delivery fee, server-side state machine, slot capacity with `xmin` concurrency, unit tests).

The gap to a **real pilot** is large and concentrated in a few areas:

1. **Payments** are a mock inside an event handler — there is **no `IPaymentProvider` abstraction, no payment session, no webhook, no refunds, no real ledger**. This is the single biggest P0 blocker.
2. **Mobile customer app does not exist** — `mobile/` is a placeholder README. (The spec's context assumed an "initial Flutter application"; in reality it was never built.)
3. **Driver workflow is a passive projection** — no driver-owned assign/accept/pickup/deliver actions, no "my deliveries", no driver-scoped isolation.
4. **Tenant isolation has holes** — several store-scoped read endpoints do **not** verify the caller belongs to that store (IDOR): store A employee can read store B orders/commission.
5. **Identity is basic** — no logout/revoke, password reset, email verification, Google OAuth, account block/delete, profile.
6. **Cross-cutting production concerns** are thin — no unified error envelope (`code/message/traceId`), no correlation IDs/structured logging config, no rate limiting, secrets live in `appsettings` for dev.
7. **Notifications** are a no-op log channel — no push/email, recipient resolution incomplete.
8. **Tests** cover only Ordering domain — no integration/auth/payment/tenant-isolation tests.

**Do NOT rebuild.** Everything above is additive/hardening on top of a correct spine. No fundamental architectural blocker was found.

---

## 1. Module-by-module

### Identity — 🟡 basic, needs production completion
Present: users, PBKDF2 password hashing, roles (`CUSTOMER/STORE_EMPLOYEE/DRIVER/ADMIN`) with optional `StoreId`, JWT access + **rotating hashed refresh tokens**, endpoints `register/login/refresh/me/admin/users`, dev admin seeder, policies (`Admin/StoreEmployee/Driver`), `MapInboundClaims=false`.
Missing (P0/P1): 🔴 logout + refresh **revocation**, 🔴 password change, 🔴 password reset, 🔴 email verification, 🔴 **Google OAuth/OIDC**, 🔴 account block/delete, 🔴 profile update, 🔴 rate limiting/brute-force, 🔴 roles `STORE_OWNER/STORE_MANAGER`.
Note: refresh tokens are stored **hashed** and rotated ✅; sensitive values are not logged ✅.

### Catalog — ✅ solid, needs store richness
Present: Store/Category/Product, decimal money `numeric(12,2)` ✅, public browse endpoints, management endpoints with **store-ownership guard** (`EnsureCanManageStore`) ✅, EF **global tenant query filter**, publishes `StoreRegistered/Updated`, `ProductPublished/Updated` via outbox.
Missing: 🟡 Store lacks `status` enum (`OPEN/CLOSED/TEMPORARILY_UNAVAILABLE` — only `isActive` bool), opening hours, phone, logo, **minimum order value**, delivery config; 🟡 Product lacks EAN/SKU, availability is a bool (acceptable); 🟡 Category lacks image/active flag. Product `imageUrl` is a string; no upload pipeline.

### Ordering — ✅ strongest module
Present: guest+customer cart with **price/name snapshots**, checkout that **revalidates availability against the read-model** and computes totals server-side ✅, **explicit `subtotal / commissionAmount / deliveryFee / total`** (never merged) ✅, order **state machine** with validated transitions + history ✅, delivery zones + **time slots with capacity and `xmin` optimistic concurrency** ✅, local read-model of stores/products from Catalog events ✅, `OrderPlaced` etc. via outbox ✅, **10 unit tests** (totals, commission, transitions, slot capacity) ✅. `PaymentAuthorized` auto-confirms order ✅.
Missing/weak: 🟡 delivery address is a **flat string** (no saved/structured addresses), 🟡 no **minimum-order** or **store-open-hours** validation at checkout, 🟡 no **discount** slot in the model, 🟡 delivery-zone postal matching not persisted (EF-ignored), 🔴 **no checkout idempotency key**, 🟡 cancellation reason not captured.

### Payments — ⚠️🔴 mock only (biggest P0)
Present: `Payment`, `CommissionLedgerEntry`; handler mock-authorizes on `OrderPlaced` and writes commission ledger on `OrderDelivered`; read endpoints for payment-by-order and store commission.
Missing: 🔴 **`IPaymentProvider` abstraction**, 🔴 payment **session creation**, 🔴 **webhook** (verification, idempotency, duplicate handling), 🔴 payment **state machine**, 🔴 **refunds** (full/partial), 🔴 proper **ledger** (customer payment, delivery fee, commission, provider fee, refund), 🔴 real provider (Przelewy24/Fiserv) behind mock. ⚠️ Payment authorization currently depends on nothing external (auto-success) — must become webhook-authoritative.

### Delivery — 🟡 passive projection, no driver workflow
Present: `Delivery` rows projected from order events; `GET /delivery/available` (Driver), `GET /delivery/orders/{id}`.
Missing: 🔴 **driver assignment** (assign/accept), 🔴 **driver-owned "my deliveries"**, 🔴 driver **accept/pickup/deliver** actions owned by Delivery (today the *Ordering* transition endpoints drive pickup/deliver, not the driver via Delivery), ⚠️ **driver isolation not enforced** (any Driver sees all available), 🟡 status enum lacks `Accepted/Cancelled` semantics per spec.

### Notifications — 🟡 no-op skeleton
Present: `Notification` rows + `LoggingNotificationChannel` (no-op); consumes `CustomerRegistered/OrderPlaced/PaymentAuthorized/OrderReadyForPickup/OrderDelivered`.
Missing: 🔴 push (FCM) / email channels, 🟡 recipient resolution (store/driver recipients are null), 🟡 dedup on redelivery (the in-flight **inbox** will cover this), 🔴 per-user in-app fetch endpoint.

### Integrations — ✅ correct as ports
`IFiscalPrinterDriver`, `IPosConnector` + no-op + registry + admin endpoint. **Keep as-is** (spec §55). Do not fake real integrations.

---

## 2. Cross-cutting

- **Event bus / replaceability** ✅ — in-process **and** RabbitMQ (topic exchange + queue) both implemented; toggle by config; modules unchanged. Matches §6.
- **Outbox** ✅ — transactional outbox + dispatcher per module. Matches §7.
- **Broker resilience** 🟡 **in flight (uncommitted)** — `Inbox/` (dedup), retry/backoff + DLQ options added; subscriber rewrite + migration + DI **not finished**. → complete first.
- **Multi-tenancy** 🟡⚠️ — `StoreId` everywhere; Catalog global filter; **but** several store-scoped **reads lack ownership checks** → IDOR (see §3).
- **Error handling** 🟡 — endpoints return `Results.Problem(code,title,detail)`; **no unified `{code,message,traceId}` envelope** (§41), no global exception middleware.
- **Observability** 🔴 — default logging only; no correlation/trace IDs, no readiness probe, no business-event logging policy. Basic `/health` exists.
- **Config/secrets** 🟡 — dev JWT signing key + DB creds in `appsettings`/compose; needs strict env-only for staging/prod + documented variables (§43).
- **Concurrency/idempotency** 🟡 — slot reservation uses `xmin` ✅; **no checkout/payment/webhook idempotency** yet (§46).
- **Tests** 🔴 — only Ordering domain unit tests; no integration/auth/payment/tenant tests (§50).
- **Mobile** 🔴🚫 — **not started** (placeholder README only).
- **Admin panel** 🟡 — login + dashboard + orders(+filters) + catalog; missing users/stores/employees/payments/refunds/drivers/deliveries/audit/settings and store-scoped role.

---

## 3. Security findings (must fix — §8/§47)

| # | Finding | Location | Severity |
|---|---|---|---|
| S1 | Store employee of store A can read **store B orders** (no store-match check) | `OrderingEndpoints` GET `/stores/{storeId}/orders` (`ListStoreOrdersAsync`) | High (IDOR/tenant) |
| S2 | Store employee can read **store B commission** | `PaymentsEndpoints` GET `/stores/{storeId}/commission` | High |
| S3 | Any Driver sees **all available deliveries**, no per-driver ownership | `DeliveryEndpoints` `/available` | Medium |
| S4 | Order status transitions authorized by **role only**, store-staff actions don't all re-check store ownership for the *order's* store on every action | `OrderingService.Authorize` | Medium (mostly covered, verify) |
| S5 | No **rate limiting** on auth endpoints | `Identity` | Medium |
| S6 | Dev **secrets** (JWT key) present in `appsettings.json` | `ZipZap.Api` | Low (dev), High if shipped |
| S7 | No **email verification / account state** gating | `Identity` | Medium |

(S4 is partially mitigated: `EnsureCanManageStore`/`Authorize` do compare `user.StoreId == order.StoreId` for store actions — verify all paths.)

---

## 4. Architectural deviations vs original assumptions

1. **Mobile app absent** while the spec assumed an "initial Flutter application" — reconcile: build it fresh (P0), no legacy to preserve.
2. **Payments faked** — auto-success in a handler contradicts §27/§28 (webhook-authoritative). Must refactor to provider abstraction + webhook. **REPLACE the fake, KEEP the module + events.**
3. **Delivery is passive** — driver actions live in Ordering, not Delivery. Move driver-facing commands into Delivery with proper ownership. **REFACTOR, do not rebuild.**
4. Everything else conforms; **no fundamental blocker**.

---

## 5. What NOT to touch (KEEP)

- Modular-monolith layout, schema-per-module, `ZipZap.Contracts`, outbox, event bus (in-process + RabbitMQ), migrator, tenant middleware — **stable, spec-conformant**.
- Ordering domain (snapshots, state machine, slot concurrency) and its tests — **only extend**.
- Catalog module shape and tenant query filter — **extend with store richness**.
- Integrations ports — **leave as no-op**.

---

## 6. KEEP / REFACTOR / REPLACE decisions

| Area | Decision | Rationale |
|---|---|---|
| BuildingBlocks (bus/outbox/tenant) | **KEEP** | Conformant, working |
| Ordering domain | **KEEP + extend** | Strongest, tested |
| Catalog | **KEEP + extend** (store status/hours/min-order/logo) | Good base |
| Identity | **KEEP + extend** (OAuth, reset, verify, revoke, block) | Correct primitives |
| Payments mock | **REPLACE internals, KEEP module+events** | Fake success unacceptable for pilot |
| Delivery | **REFACTOR** (own driver commands + isolation) | Passive today |
| Notifications | **EXTEND** (channels, recipients, dedup) | Skeleton |
| Mobile | **BUILD** (new Flutter app) | Absent |
| Admin panel | **EXTEND** (ops sections, store scope) | Partial |
| Tenant reads | **FIX** ownership checks | Security |

See `ADVANCED_MVP_ROADMAP.md` for the sequenced plan, dependencies, and risks.
