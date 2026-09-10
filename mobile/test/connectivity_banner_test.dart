import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:zipzap/core/widgets/connectivity_banner.dart';

Widget _host(bool online) => ProviderScope(
      overrides: [
        connectivityProvider.overrideWith((ref) => Stream.value(online)),
      ],
      child: const MaterialApp(home: Scaffold(body: OfflineBar())),
    );

const _text = 'Brak połączenia z internetem';

void main() {
  testWidgets('offline → banner is shown', (tester) async {
    await tester.pumpWidget(_host(false));
    await tester.pumpAndSettle();
    expect(find.text(_text), findsOneWidget);
  });

  testWidgets('online → banner is hidden', (tester) async {
    await tester.pumpWidget(_host(true));
    await tester.pumpAndSettle();
    expect(find.text(_text), findsNothing);
  });
}
