import '../api/api_client.dart';
import '../../models/user.dart';

/// Wynik uwierzytelnienia (koperta AuthResponse backendu).
class AuthResult {
  final String accessToken;
  final String refreshToken;
  final AppUser user;

  AuthResult({
    required this.accessToken,
    required this.refreshToken,
    required this.user,
  });

  factory AuthResult.fromJson(Map<String, dynamic> j) => AuthResult(
        accessToken: j['accessToken'].toString(),
        refreshToken: j['refreshToken'].toString(),
        user: AppUser.fromJson(j['user'] as Map<String, dynamic>),
      );
}

class AuthRepository {
  final ApiClient _api;
  AuthRepository(this._api);

  Future<AuthResult> login(String email, String password) async {
    final data = await _api.post('/identity/login',
        body: {'email': email, 'password': password});
    return AuthResult.fromJson(data as Map<String, dynamic>);
  }

  Future<AuthResult> register(String email, String password, String fullName) async {
    final data = await _api.post('/identity/register',
        body: {'email': email, 'password': password, 'fullName': fullName});
    return AuthResult.fromJson(data as Map<String, dynamic>);
  }

  Future<AuthResult> refresh(String refreshToken) async {
    final data = await _api.post('/identity/refresh', body: {'refreshToken': refreshToken});
    return AuthResult.fromJson(data as Map<String, dynamic>);
  }

  Future<AuthResult> google(String idToken) async {
    final data = await _api.post('/identity/google', body: {'idToken': idToken});
    return AuthResult.fromJson(data as Map<String, dynamic>);
  }

  Future<void> logout(String? refreshToken) =>
      _api.post('/identity/logout', body: {'refreshToken': refreshToken});
}
