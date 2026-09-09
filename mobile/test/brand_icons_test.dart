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
