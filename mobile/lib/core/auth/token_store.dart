import 'package:flutter_secure_storage/flutter_secure_storage.dart';

/// Trwałe, bezpieczne przechowywanie refresh tokenu. Access token żyje tylko
/// w pamięci procesu (nie zapisujemy go).
class TokenStore {
  static const _kRefresh = 'zz_refresh_token';
  final FlutterSecureStorage _storage;

  TokenStore([FlutterSecureStorage? storage])
      : _storage = storage ?? const FlutterSecureStorage();

  Future<String?> readRefresh() => _storage.read(key: _kRefresh);
  Future<void> writeRefresh(String value) => _storage.write(key: _kRefresh, value: value);
  Future<void> clear() => _storage.delete(key: _kRefresh);
}
