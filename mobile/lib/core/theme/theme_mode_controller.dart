import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';

/// Tryb kolorystyczny wybrany przez użytkownika (System / Jasny / Ciemny),
/// utrwalany między uruchomieniami.
class ThemeModeController extends Notifier<ThemeMode> {
  static const _key = 'zz_theme_mode';
  final FlutterSecureStorage _storage = const FlutterSecureStorage();

  @override
  ThemeMode build() {
    _load();
    return ThemeMode.system;
  }

  Future<void> _load() async {
    final v = await _storage.read(key: _key);
    final loaded = switch (v) {
      'light' => ThemeMode.light,
      'dark' => ThemeMode.dark,
      _ => ThemeMode.system,
    };
    if (loaded != state) state = loaded;
  }

  Future<void> setMode(ThemeMode mode) async {
    state = mode;
    await _storage.write(key: _key, value: mode.name);
  }
}

final themeModeProvider =
    NotifierProvider<ThemeModeController, ThemeMode>(ThemeModeController.new);
