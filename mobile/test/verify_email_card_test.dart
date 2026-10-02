import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:zipzap/core/api/api_client.dart';
import 'package:zipzap/core/api/api_exception.dart';
import 'package:zipzap/core/auth/auth_repository.dart';
import 'package:zipzap/core/config/public_config.dart';
import 'package:zipzap/core/providers.dart';
import 'package:zipzap/core/theme/zz_theme.dart';
import 'package:zipzap/features/account/verify_email_card.dart';

/// Repozytorium zwracające to, co zwróciłoby API ponownej wysyłki: wysłano / już potwierdzony / błąd.
class _FakeAuth extends AuthRepository {
  _FakeAuth({this.sent = true, this.error}) : super(ApiClient());
  final bool sent;
  final ApiException? error;
  int calls = 0;

  @override
  Future<bool> resendVerification() async {
    calls++;
    if (error != null) throw error!;
    return sent;
  }
}

Future<void> _pump(WidgetTester tester, _FakeAuth auth, {String delivery = 'live'}) async {
  await tester.pumpWidget(ProviderScope(
    overrides: [
      authRepositoryProvider.overrideWith((_) => auth),
      publicConfigProvider.overrideWith((_) async => PublicConfig(emailDelivery: delivery)),
    ],
    child: MaterialApp(
      theme: ZzTheme.light(),
      home: const Scaffold(body: VerifyEmailCard(email: 'jan@example.com')),
    ),
  ));
  await tester.pumpAndSettle();
}

void main() {
  testWidgets('reminds about the link and resends it through the existing endpoint with a cooldown', (tester) async {
    final auth = _FakeAuth();
    await _pump(tester, auth);
    expect(find.text('Potwierdź adres e-mail'), findsOneWidget);
    expect(find.textContaining('wysłaliśmy link na jan@example.com'), findsOneWidget);

    await tester.tap(find.text('Wyślij link ponownie'));
    await tester.pump();
    await tester.pump();

    expect(auth.calls, 1);
    expect(find.text('Link wysłany'), findsOneWidget);
    expect(find.text('Wyślij ponownie za 60 s'), findsOneWidget);

    await tester.tap(find.text('Wyślij ponownie za 60 s'), warnIfMissed: false);
    await tester.pump();
    expect(auth.calls, 1, reason: 'przycisk jest zablokowany na czas odliczania');

    await tester.pump(const Duration(seconds: 60));
    expect(find.text('Wyślij link ponownie'), findsOneWidget);
    await tester.pumpWidget(const SizedBox()); // zatrzymuje licznik
  });

  testWidgets('explains the per-account limit', (tester) async {
    await _pump(tester, _FakeAuth(error: ApiException(
        code: 'validation.verify_resend_limit', message: 'x', statusCode: 400)));
    await tester.tap(find.text('Wyślij link ponownie'));
    await tester.pump();
    await tester.pump();
    expect(find.textContaining('Wysłaliśmy już kilka linków'), findsOneWidget);
    expect(find.text('Link wysłany'), findsNothing);
  });

  testWidgets('does not claim a link was sent when the address is already verified', (tester) async {
    final auth = _FakeAuth(sent: false);
    await _pump(tester, auth);
    await tester.tap(find.text('Wyślij link ponownie'));
    await tester.pump();
    await tester.pump();
    expect(auth.calls, 1);
    expect(find.text('Link wysłany'), findsNothing);
    expect(find.textContaining('za 60 s'), findsNothing);
  });

  testWidgets('says clearly when mail is only a mock', (tester) async {
    await _pump(tester, _FakeAuth(), delivery: 'mock');
    expect(find.textContaining('Poczta w tym środowisku to atrapa'), findsOneWidget);
  });
}
