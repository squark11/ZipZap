import '../../models/user.dart';

enum AuthStatus { unknown, authenticated, anonymous }

class AuthState {
  final AuthStatus status;
  final AppUser? user;

  const AuthState(this.status, [this.user]);
  const AuthState.unknown() : this(AuthStatus.unknown);

  bool get isAuthenticated => status == AuthStatus.authenticated;
  bool get isKnown => status != AuthStatus.unknown;
}
