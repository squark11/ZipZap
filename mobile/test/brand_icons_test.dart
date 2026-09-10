import 'package:flutter/material.dart';
import 'package:flutter_svg/flutter_svg.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:zipzap/core/widgets/states.dart';
import 'package:zipzap/core/widgets/zz_icon.dart';

Widget _host(Widget child) => MaterialApp(home: Scaffold(body: Center(child: child)));

void main() {
  testWidgets('ZzIcon renders an SvgPicture from the icons folder', (tester) async {
    await tester.pumpWidget(_host(const ZzIcon('cart')));
    expect(find.byType(SvgPicture), findsOneWidget);
    expect(tester.takeException(), isNull);
  });

  testWidgets('every brand icon loads without throwing', (tester) async {
    const names = <String>[
      // batch 1
      'store', 'cart', 'orders', 'account', 'search', 'bell', 'clock',
      'location', 'package', 'heart',
      // batch 2
      'delivery', 'filter', 'star', 'star_fill', 'plus', 'minus', 'trash',
      'chevron_right', 'chevron_left', 'card', 'info', 'home', 'check',
      'check_circle', 'x_circle', 'close',
      // batch 3
      'eye', 'eye_off', 'lock', 'mail', 'phone', 'tag', 'edit', 'cutlery',
    ];
    for (final n in names) {
      await tester.pumpWidget(_host(ZzIcon(n)));
      expect(find.byType(SvgPicture), findsOneWidget, reason: n);
      expect(tester.takeException(), isNull, reason: n);
    }
  });

  testWidgets('StatusPill renders label + icon for every known status', (tester) async {
    const statuses = <String, String>{
      'Placed': 'Złożone',
      'Confirmed': 'Potwierdzone',
      'Picking': 'Kompletowane',
      'ReadyForPickup': 'Gotowe do odbioru',
      'InDelivery': 'W dostawie',
      'Delivered': 'Dostarczone',
      'Completed': 'Zakończone',
      'Cancelled': 'Anulowane',
    };
    for (final entry in statuses.entries) {
      await tester.pumpWidget(_host(StatusPill(entry.key)));
      expect(find.text(entry.value), findsOneWidget, reason: entry.key);
      expect(find.byType(SvgPicture), findsOneWidget, reason: entry.key);
      expect(tester.takeException(), isNull, reason: entry.key);
    }
  });

  testWidgets('StatusPill falls back gracefully for an unknown status', (tester) async {
    await tester.pumpWidget(_host(StatusPill('Weird')));
    expect(find.text('Weird'), findsOneWidget);
    expect(tester.takeException(), isNull);
  });
}
