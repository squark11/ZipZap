import 'package:flutter/foundation.dart' show kIsWeb;

/// Konfiguracja aplikacji zależna od środowiska/targetu.
class AppConfig {
  /// Bazowy URL API.
  /// - web: `localhost:5080`
  /// - Android emulator: `10.0.2.2` (mapowanie na host loopback)
  static String get apiBaseUrl {
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
