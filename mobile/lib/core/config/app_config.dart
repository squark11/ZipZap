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

  /// Realne integracje wymagają konfiguracji — do tego czasu wyłączone
  /// (bez atrap sukcesu: przycisk pokazuje jasny komunikat).
  static const bool googleSignInEnabled = false;
  static const bool pushEnabled = false;

  /// Waluta pilota.
  static const String currency = 'PLN';
}
