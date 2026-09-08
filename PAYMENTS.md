# ZipZap — Płatności

Płatności są **webhook-authoritative**: status płatności zmienia wyłącznie **zweryfikowany webhook dostawcy**,
nigdy przekierowanie klienta. Warstwa biznesowa zależy tylko od abstrakcji `IPaymentProvider` — nigdy od SDK
konkretnego dostawcy (spec §54). Moduł Ordering jest sprzężony z płatnościami **tylko przez zdarzenia**.

## Przepływ

```
Zamówienie złożone (OrderPlaced)
        ↓  (Payments handler)
Płatność: Pending  +  sesja u dostawcy (redirect URL)
        ↓  klient płaci na stronie dostawcy
Webhook dostawcy  →  POST /api/payments/webhook/{provider}
        ↓  weryfikacja podpisu + idempotencja
Płatność: Authorized  →  zdarzenie PaymentAuthorized
        ↓  (Ordering handler)
Zamówienie: Confirmed
        ↓  … dostawa … OrderDelivered
Prowizja zaksięgowana + płatność Settled
```

- **Order NIE jest potwierdzane** dopóki nie przyjdzie autoryzujący webhook.
- Webhook jest **idempotentny**: powtórka nie zmienia stanu i nie dubluje zdarzeń.
- Zły/brak podpisu → `400 invalid_signature`; nieznana płatność → `404`.

## Endpointy

| Metoda | Ścieżka | Auth | Opis |
|---|---|---|---|
| POST | `/api/payments/webhook/{provider}` | brak (podpis) | Autorytatywny callback dostawcy |
| GET | `/api/payments/orders/{orderId}` | zalogowany + sklep | Status płatności + `redirectUrl` |
| GET | `/api/payments/stores/{storeId}/commission` | StoreEmployee/Admin | Suma prowizji |

## Konfiguracja

```jsonc
"Payments": {
  "Provider": "mock",                    // domyślny dostawca do tworzenia sesji
  "PublicUrl": "http://localhost:4200",  // baza dla stron płatności/powrotu
  "Mock": { "Secret": "mock-dev-secret" } // sekret HMAC webhooków mocka (dev)
}
```
Env (docker/hosting): `Payments__Provider`, `Payments__PublicUrl`, `Payments__Mock__Secret`.

## Dostawca testowy (mock)

`MockPaymentProvider` tworzy lokalną sesję i **weryfikuje webhook podpisem HMAC-SHA256** z sekretu — to nie
fałszywy sukces: autoryzacja wymaga poprawnie podpisanego callbacku (jak u realnego dostawcy).

Body webhooka (mock):
```json
{ "paymentId": "<guid>", "outcome": "authorized|failed", "providerRef": "..." }
```
Nagłówek: `X-Signature: <HEX(HMACSHA256(body, secret))>` (`MockPaymentProvider.Sign` liczy tę wartość).

## Podłączenie realnego dostawcy (np. Przelewy24) — kroki

> Nie implementujemy realnego dostawcy bez oficjalnej dokumentacji i poświadczeń (spec §62/§63).
> Adapter dodajesz jako kolejną implementację `IPaymentProvider` — **bez zmian w Ordering ani w domenie płatności**.

1. Utwórz konto sprzedawcy i **sandbox** u dostawcy; pobierz `MerchantId/PosId`, `CRC`/klucz API, sekret webhooka.
2. Dodaj `Przelewy24PaymentProvider : IPaymentProvider` (`Key => "przelewy24"`):
   - `CreateSessionAsync` → wywołanie API rejestracji transakcji wg **aktualnej dokumentacji**, zwróć `RedirectUrl`.
   - `VerifyWebhook` → weryfikacja podpisu/sumy kontrolnej wg dokumentacji, parsowanie statusu.
3. Zarejestruj adapter w DI i ustaw `Payments:Provider=przelewy24`.
4. Skonfiguruj **URL webhooka** (`/api/payments/webhook/przelewy24`) i URL powrotu w panelu dostawcy.
5. Poświadczenia trzymaj w zmiennych środowiskowych / secret store — **nigdy w repo**.

## Do zrobienia w kolejnych iteracjach
- Realny adapter Przelewy24 (sandbox) · BLIK/karta przez PSP.
- **Refundy** (pełne/częściowe) — `IPaymentProvider.RefundAsync` + stan `Refunded` (już w enumie).
- Rozszerzony **ledger** (płatność klienta / opłata za dostawę / prowizja / opłata dostawcy) — audytowalny.
