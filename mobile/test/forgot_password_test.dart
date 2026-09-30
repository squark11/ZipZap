import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:zipzap/core/api/api_client.dart';
import 'package:zipzap/core/api/api_exception.dart';
import 'package:zipzap/core/auth/auth_repository.dart';
import 'package:zipzap/core/config/public_config.dart';
import 'package:zipzap/core/providers.dart';
import 'package:zipzap/core/theme/zz_theme.dart';
import 'package:zipzap/features/account/forgot_password_screen.dart';

/// Repozytorium, które zwraca to, co zwróciłoby API: sukces (dla KAŻDEGO adresu) albo wskazany błąd.
class _FakeAuth extends AuthRepository {
  _FakeAuth([this.error]) : super(ApiClient());
  final ApiException? error;
  final requested = <String>[];

  @override
  Future<void> forgotPassword(String email) async {
    requested.add(email);
    if (error != null) throw error!;
  }
}

Future<_FakeAuth> _pump(WidgetTester tester, {String delivery = 'live', ApiException? error, String? initialEmail}) async {
  final auth = _FakeAuth(error);
  await tester.pumpWidget(ProviderScope(
    overrides: [
      authRepositoryProvider.overrideWith((_) => auth),
      publicConfigProvider.overrideWith((_) async => PublicConfig(emailDelivery: delivery)),
    ],
    child: MaterialApp(theme: ZzTheme.light(), home: ForgotPasswordScreen(initialEmail: initialEmail)),
  ));
  await tester.pumpAndSettle();
  return auth;
}

Future<void> _send(WidgetTester tester, String email) async {
  await tester.enterText(find.byType(TextField), email);
  await tester.tap(find.text('Wyślij link'));
  await tester.pump();
  await tester.pump();
}

void main() {
  test('public config: emailDelivery=mock means the reset mail will not be sent', () {
    expect(PublicConfig.fromJson({'emailDelivery': 'mock'}).mailIsMock, isTrue);
    expect(PublicConfig.fromJson({'emailDelivery': 'live'}).mailIsMock, isFalse);
    expect(PublicConfig.fromJson({}).mailIsMock, isFalse, reason: 'starsze API bez pola — bez fałszywego alarmu');
  });

  testWidgets('shows the same generic answer and a resend countdown after sending', (tester) async {
    final auth = await _pump(tester);
    await _send(tester, 'ktokolwiek@example.com');

    expect(auth.requested, ['ktokolwiek@example.com']);
    expect(find.text('Sprawdź skrzynkę'), findsOneWidget);
    expect(find.textContaining('Jeśli istnieje konto z adresem ktokolwiek@example.com'), findsOneWidget);
    expect(find.textContaining('strona Dowózka.pl otworzy się w przeglądarce'), findsOneWidget);
    expect(find.text('Wyślij ponownie za 60 s'), findsOneWidget);
    expect(find.byKey(const Key('mock-mail-notice')), findsNothing);

    await tester.pump(const Duration(seconds: 60));
    expect(find.text('Wyślij ponownie'), findsOneWidget);
    await tester.pumpWidget(const SizedBox()); // zatrzymuje licznik
  });

  testWidgets('warns clearly when mail is only a mock', (tester) async {
    await _pump(tester, delivery: 'mock');
    expect(find.byKey(const Key('mock-mail-notice')), findsOneWidget);
    expect(find.textContaining('Poczta w tym środowisku to atrapa'), findsOneWidget);
  });

  testWidgets('rate limit is explained and the form stays', (tester) async {
    await _pump(tester, error: ApiException(code: 'rate', message: 'x', statusCode: 429));
    await _send(tester, 'a@example.com');
    expect(find.text('Zbyt wiele prób. Spróbuj ponownie za minutę.'), findsOneWidget);
    expect(find.text('Sprawdź skrzynkę'), findsNothing);
  });

  testWidgets('network error is explained and the form stays', (tester) async {
    await _pump(tester, error: ApiException(code: 'net', message: 'x'));
    await _send(tester, 'a@example.com');
    expect(find.textContaining('Nie udało się połączyć z serwerem'), findsOneWidget);
    expect(find.text('Wyślij link'), findsOneWidget);
  });

  testWidgets('prefills the email typed on the login screen and validates it', (tester) async {
    final auth = await _pump(tester, initialEmail: 'jan@example.com');
    expect(find.text('jan@example.com'), findsOneWidget);

    await _send(tester, 'bez-malpy');
    expect(find.text('Podaj poprawny adres e-mail.'), findsOneWidget);
    expect(auth.requested, isEmpty);
  });
}
