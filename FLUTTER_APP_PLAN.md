# ZipZap — Plan aplikacji klienta (Flutter) — Faza F1

Cel: produkcyjnej jakości aplikacja **CUSTOMER** konsumująca `ZipZap.Api`.
Lejek 6 kroków: **wybór sklepu → koszyk → adres/termin → płatność → potwierdzenie → status live**.
Przeglądanie oferty bez logowania; rejestracja/logowanie dopiero przy płatności.

## 1. Stack i biblioteki
- **Flutter (stable)**, Dart. Target weryfikacji: **web (Chrome)** + `flutter analyze` + `flutter test`. Android/iOS build opcjonalnie (nie wymaga pełnego toolchainu na tym etapie).
- **State management: Riverpod** (`flutter_riverpod`) — testowalny, bez BuildContext w logice.
- **Routing: go_router** — deklaratywne trasy, deep-linki (powrót z płatności).
- **HTTP: dio** + interceptor (auth bearer, auto-refresh na 401, correlation id).
- **Sesja: flutter_secure_storage** — refresh token trzymany bezpiecznie (nie w SharedPreferences). Access token w pamięci.
- **Modele: ręczne `fromJson`** (bez codegenu freezed na start — mniej tarcia; można dołożyć później).
- **Bez Firebase na start**: rejestracja tokenu urządzenia (push) za flagą; realny FCM = udokumentowany krok z kredencjałami (nie zgadujemy API, nie commitujemy sekretów).
- **Google Sign-In**: przycisk wołający `POST /identity/google` z id-tokenem; plugin `google_sign_in` + client-id = udokumentowany krok konfiguracyjny. Do czasu konfiguracji przycisk wyłączony z jasnym komunikatem (bez fałszywego sukcesu).

## 2. Struktura projektu (`mobile/`)
```
lib/
  main.dart
  core/
    config/        # AppConfig: baseUrl (web=localhost:5080, android-emu=10.0.2.2), flags
    api/           # ApiClient (dio), interceptors, ApiException, envelope {code,message,traceId}
    auth/          # AuthController (Riverpod), TokenStore (secure), session state
    theme/         # ZzTheme z tokenów /branding (orange/green/graphite, Poppins/Inter)
    router/        # go_router + guardy (checkout wymaga zalogowania)
    widgets/       # wspólne: ZzButton, ZzField, EmptyState, ErrorState, LoadingState, OfflineBanner
  features/
    stores/        # lista + szczegóły sklepu (status OPEN/CLOSED, min-order)
    catalog/       # kategorie + produkty + szczegóły produktu
    cart/          # koszyk (token koszyka), pozycje, podsumowanie (subtotal + delivery fee OSOBNO)
    checkout/      # adres, strefa+slot, klucz idempotencji, walidacja min-order
    payment/       # otwarcie redirectUrl (webview/zewn. przeglądarka) + polling statusu
    orders/        # moje zamówienia + śledzenie (timeline statusów + dostawa)
    notifications/ # skrzynka in-app (/notifications/mine, /{id}/read) + rejestracja urządzenia
    account/       # profil (/me), logowanie/rejestracja, weryfikacja e-mail, reset hasła
  models/          # Store, Product, Cart, CartItem, Order, Zone, Slot, Payment, Notification
```

## 3. Motyw (z `/branding`)
- Kolory: primary `#F97316`, secondary/success `#22C55E`, tekst/graphite `#3A3F4B`, tła `#FFFFFF`/`#F7F8FA`, border `#E5E7EB`, warning `#F59E0B`, danger `#EF4444`.
- Typografia: nagłówki **Poppins** (600/700), UI **Inter** (400/500/600); fallback system-ui. Fonty z Google Fonts (OFL) via `google_fonts` lub spięte lokalnie.
- Radiusy 6/10/16, cienie z tokenów. Skala typografii z `branding/typography.md`.

## 4. Mapa integracji API (co konsumuje aplikacja)
| Feature | Endpointy |
|---|---|
| Auth/sesja | `POST /identity/register`, `/login`, `/refresh`, `/google`, `GET /identity/me`, `/logout`, `/email/verify`, `/password/forgot`,`/password/reset` |
| Sklepy | `GET /catalog/stores`, `/catalog/stores/{idOrSlug}` |
| Oferta | `GET /catalog/stores/{id}/categories`, `/catalog/stores/{id}/products`, `/catalog/products/{id}` |
| Koszyk | `POST /ordering/carts`, `GET /ordering/carts/{id}?token=`, `POST/PUT/DELETE .../items` |
| Dostawa (wybór) | `GET /ordering/stores/{id}/zones`, `/ordering/stores/{id}/slots` |
| Checkout | `POST /ordering/carts/{id}/checkout` (Bearer, nagłówek `Idempotency-Key`) |
| Płatność | **[nowy]** `GET /payments/orders/{orderId}/mine` → `{status, redirectUrl}`; otwarcie redirectUrl; polling statusu |
| Zamówienia | `GET /ordering/orders/mine`, `/ordering/orders/{id}` |
| Śledzenie dostawy | `GET /delivery/orders/{orderId}` |
| Powiadomienia | `GET /notifications/mine`, `POST /notifications/{id}/read`, `POST /notifications/devices` |

## 5. Kluczowe zasady (zgodne z regułami produkcyjnymi)
- **Płatność webhook-autorytatywna**: aplikacja NIGDY nie oznacza sukcesu sama. Po otwarciu `redirectUrl` **odpytuje status** (`/payments/orders/{id}/mine`) aż `Authorized`/`Failed`. Ekran potwierdzenia dopiero po statusie z backendu.
- **Delivery fee OSOBNO** od wartości koszyka — pokazywane jako oddzielna pozycja (prowizję płaci sklep, nie klient).
- **Brak fałszywych funkcji**: Google/push/refundy — abstrakcja + jasny stan „niedostępne, wymaga konfiguracji", nie atrapa sukcesu.
- **Bezpieczeństwo**: refresh token w secure storage; brak logowania tokenów; access token tylko w pamięci.
- **Multi-tenant**: aplikacja operuje na publicznym read-modelu sklepów; wrażliwe operacje per-user chroni backend.

## 6. Wymagane dodatki backendu dla F1 (małe, w granicach modułów)
1. **Płatność dla klienta** — dziś `GET /payments/orders/{id}` jest store-only. Dodać:
   - `Payment.CustomerId` (nadawany z `OrderPlaced`, które już niesie `CustomerId`) — migracja addytywna.
   - `GET /payments/orders/{orderId}/mine` autoryzujący właściciela (`user.UserId == payment.CustomerId`), zwraca `{status, redirectUrl}`.
   - Zachowuje granice: Payments jest właścicielem danych płatności i URL-a; nic nie czyta wnętrza innych modułów.
2. **CORS/baseUrl dla klienta** — web: `localhost:5080`; emulator Android: `10.0.2.2:5080`. Zweryfikować politykę CORS dla dev.

## 7. Strategia weryfikacji („build zielony" na każdym kroku)
- `flutter analyze` — czysto (0 errorów).
- `flutter test` — unity: przeliczenia koszyka (subtotal + fee osobno), mapowanie modeli `fromJson`, logika auto-refresh tokenu, walidacja min-order.
- `flutter run -d chrome` — smoke całego lejka na żywo przeciw Dockerowemu API.

## 8. Kolejność dostaw (vertical slice → breadth)
1. **Slice pionowy happy-path**: config+theme+api client+auth → sklepy → oferta → koszyk → checkout → płatność (polling) → potwierdzenie → śledzenie. + stany error/empty/loading/offline.
2. **Breadth**: skrzynka powiadomień UI, profil/konto, weryfikacja e-mail/reset hasła, Google (za konfiguracją), rejestracja urządzenia push.

Każdy etap: `flutter analyze` + `flutter test` zielone + osobny commit.
