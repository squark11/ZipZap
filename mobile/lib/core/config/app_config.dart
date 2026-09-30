import 'package:flutter/foundation.dart' show kIsWeb;

/// Konfiguracja aplikacji zależna od środowiska/targetu.
class AppConfig {
  /// Bazowy URL API.
  /// Deploy: podaj przy buildzie `--dart-define=API_BASE_URL=https://api.twojadomena/api`.
  /// Domyślnie (dev): web → `localhost:5080`, emulator Androida → `10.0.2.2`.
  static String get apiBaseUrl {
    const fromEnv = String.fromEnvironment('API_BASE_URL');
    if (fromEnv.isNotEmpty) return fromEnv;
    if (kIsWeb) return 'http://localhost:5080/api';
    return 'http://10.0.2.2:5080/api';
  }

  /// Środowisko buildu: `--dart-define=APP_ENV=preview` (podgląd na testowym API) — aplikacja pokazuje wtedy
  /// stały znacznik „PODGLĄD", żeby testerzy nie pomylili go z działającą usługą.
  static const String appEnv = String.fromEnvironment('APP_ENV');
  static bool get isPreview => appEnv == 'preview';

  /// Realne integracje wymagają konfiguracji — do tego czasu wyłączone
  /// (bez atrap sukcesu: przycisk pokazuje jasny komunikat).
  static const bool googleSignInEnabled = false;
  static const bool pushEnabled = false;

  /// Waluta pilota.
  static const String currency = 'PLN';

  /// Wersja aplikacji dołączana do zgłaszanych uwag (kontekst dla zespołu).
  static const String appVersion = '1.0.0';
}
