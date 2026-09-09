# ZipZap — Mobile (Flutter)

Aplikacja klienta (**CUSTOMER**) — lejek:
**wybór sklepu → koszyk → adres i termin → płatność → potwierdzenie → status live**.
Przeglądanie oferty bez logowania; rejestracja/logowanie dopiero przy płatności.

## Stack
- Flutter (stable) / Dart
- **Riverpod** (state), **go_router** (routing + guardy), **dio** (HTTP + auto-refresh na 401)
- **flutter_secure_storage** (refresh token), **google_fonts** (Poppins/Inter), **url_launcher** (płatność)
- Motyw z `/branding` (pomarańcz #F97316, zieleń #22C55E, grafit #3A3F4B)

## Struktura
`lib/core` (config, api, auth, theme, router, widgets) · `lib/features/*` (stores, catalog,
cart, checkout, payment, orders, notifications, account, splash) · `lib/models`.

## Konfiguracja API
`lib/core/config/app_config.dart` — baseUrl: web `localhost:5080`, emulator Android `10.0.2.2`.
Realne integracje (Google, push) są za flagami (`googleSignInEnabled`, `pushEnabled`) — do czasu
konfiguracji UI pokazuje jasny komunikat, bez atrap sukcesu.

## Uruchomienie (dev)
Backend: `docker compose up` w katalogu głównym (API na :5080). Następnie:

```bash
flutter pub get
flutter run -d chrome        # lub: -d web-server --web-port 8098
```

## Weryfikacja
```bash
flutter analyze   # 0 błędów
flutter test      # testy jednostkowe (modele, serializacja koszyka)
```

## Zasady produkcyjne
- Płatność **webhook-autorytatywna**: aplikacja odpytuje status (`/payments/orders/{id}/mine`),
  nigdy nie ustawia sukcesu sama.
- **Opłata za dostawę osobno** od wartości koszyka (prowizję płaci sklep).
- Refresh token w secure storage; brak logowania tokenów.
