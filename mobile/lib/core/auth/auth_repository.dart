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

  Future<AuthResult> register(String email, String password, String fullName, {String? captchaToken}) async {
    final body = <String, dynamic>{'email': email, 'password': password, 'fullName': fullName};
    if (captchaToken != null && captchaToken.isNotEmpty) body['captchaToken'] = captchaToken;
    final data = await _api.post('/identity/register', body: body);
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

  /// Zawsze kończy się sukcesem po stronie backendu (bez enumeracji kont).
  Future<void> forgotPassword(String email) =>
      _api.post('/identity/password/forgot', body: {'email': email});

  /// Czy adres e-mail zalogowanego konta jest potwierdzony (stan z bazy; null — starsze API bez tego pola).
  Future<bool?> isEmailVerified() async {
    final data = await _api.get('/identity/me');
    return (data as Map<String, dynamic>)['isEmailVerified'] as bool?;
  }

  /// Ponowna wysyłka linku potwierdzającego. `false` — adres jest już potwierdzony (nic nie wysłano).
  /// Limit na konto: [ApiException] z kodem `validation.verify_resend_limit`.
  Future<bool> resendVerification() async {
    final data = await _api.post('/identity/email/resend-verification', body: const {});
    return (data as Map<String, dynamic>)['sent'] != false;
  }

  /// Zmiana hasła zalogowanego użytkownika (backend unieważnia pozostałe sesje).
  Future<void> changePassword(String currentPassword, String newPassword) =>
      _api.post('/identity/password/change',
          body: {'currentPassword': currentPassword, 'newPassword': newPassword});
}
