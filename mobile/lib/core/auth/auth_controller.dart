import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../providers.dart';
import 'auth_repository.dart';
import 'auth_state.dart';
import 'token_store.dart';

/// Zarządza sesją: bootstrap z refresh tokenu, logowanie, rejestracja, wylogowanie.
/// Wpina do [ApiClient] callback odświeżania (auto-refresh na 401).
class AuthController extends Notifier<AuthState> {
  String? _refreshToken;

  TokenStore get _store => ref.read(tokenStoreProvider);
  AuthRepository get _repo => ref.read(authRepositoryProvider);

  @override
  AuthState build() {
    ref.read(apiClientProvider).setRefreshCallback(_tryRefresh);
    return const AuthState.unknown();
  }

  /// Wywoływane przy starcie: próba przywrócenia sesji z zapisanego refresh tokenu.
  Future<void> bootstrap() async {
    final rt = await _store.readRefresh();
    if (rt == null) {
      state = const AuthState(AuthStatus.anonymous);
      return;
    }
    _refreshToken = rt;
    final ok = await _tryRefresh();
    if (!ok) state = const AuthState(AuthStatus.anonymous);
  }

  Future<void> login(String email, String password) async {
    final r = await _repo.login(email, password);
    await _apply(r);
  }

  Future<void> register(String email, String password, String fullName) async {
    final r = await _repo.register(email, password, fullName);
    await _apply(r);
  }

  Future<void> logout() async {
    try {
      await _repo.logout(_refreshToken);
    } catch (_) {
      // wylogowanie lokalne i tak następuje
    }
    await _clear();
  }

  Future<bool> _tryRefresh() async {
    if (_refreshToken == null) return false;
    try {
      final r = await _repo.refresh(_refreshToken!);
      await _apply(r);
      return true;
    } catch (_) {
      await _clear();
      return false;
    }
  }

  Future<void> _apply(AuthResult r) async {
    _refreshToken = r.refreshToken;
    await _store.writeRefresh(r.refreshToken);
    ref.read(apiClientProvider).setAccessToken(r.accessToken);
    state = AuthState(AuthStatus.authenticated, r.user);
  }

  Future<void> _clear() async {
    await _store.clear();
    _refreshToken = null;
    ref.read(apiClientProvider).setAccessToken(null);
    state = const AuthState(AuthStatus.anonymous);
  }
}
