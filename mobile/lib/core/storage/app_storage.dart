import 'package:flutter_secure_storage/flutter_secure_storage.dart';

/// Trwały magazyn małych wartości aplikacji (web: localStorage szyfrowany WebCrypto; mobile: Keychain/Keystore).
/// Przeżywa odświeżenie strony i ponowne uruchomienie. Błędy magazynu (np. przeglądarka w trybie prywatnym)
/// są połykane — aplikacja działa dalej, tylko bez zapamiętania.
class AppStorage {
  final FlutterSecureStorage _storage;

  AppStorage([FlutterSecureStorage? storage]) : _storage = storage ?? const FlutterSecureStorage();

  Future<String?> read(String key) async {
    try {
      return await _storage.read(key: key);
    } catch (_) {
      return null;
    }
  }

  Future<void> write(String key, String value) async {
    try {
      await _storage.write(key: key, value: value);
    } catch (_) {}
  }

  Future<void> delete(String key) async {
    try {
      await _storage.delete(key: key);
    } catch (_) {}
  }
}
