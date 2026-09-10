import 'package:flutter/material.dart';
import 'package:flutter_svg/flutter_svg.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:intl/date_symbol_data_local.dart';
import 'package:zipzap/core/util/format.dart';
import 'package:zipzap/features/orders/order_track_screen.dart';

Widget _host(Widget child) =>
    MaterialApp(home: Scaffold(body: SingleChildScrollView(child: child)));

void main() {
  setUpAll(() async => initializeDateFormatting('pl_PL'));

  const labels = [
    'Złożone',
    'Potwierdzone',
    'Kompletowane',
    'Gotowe do odbioru',
    'W dostawie',
    'Dostarczone',
  ];

  testWidgets('timeline shows all steps + an icon per step (mid-flow)',
      (tester) async {
    await tester.pumpWidget(_host(const OrderTimeline(currentStatus: 'Picking')));
    await tester.pump(); // one frame; do NOT settle (active node pulses)

    for (final l in labels) {
      expect(find.text(l), findsOneWidget, reason: l);
    }
    // 6 step icons.
    expect(find.byType(SvgPicture), findsNWidgets(labels.length));
    // Active step marker.
    expect(find.text('W trakcie'), findsOneWidget);
    expect(tester.takeException(), isNull);
  });

  testWidgets('delivered timeline renders without a pulsing node', (tester) async {
    await tester.pumpWidget(_host(const OrderTimeline(currentStatus: 'Delivered')));
    await tester.pump();
    expect(find.text('W trakcie'), findsNothing);
    expect(find.byType(SvgPicture), findsNWidgets(labels.length));
    expect(tester.takeException(), isNull);
  });

  testWidgets('shows timestamps for completed steps', (tester) async {
    final placed = DateTime.utc(2026, 9, 10, 8, 0);
    final confirmed = DateTime.utc(2026, 9, 10, 8, 5);
    await tester.pumpWidget(_host(OrderTimeline(
      currentStatus: 'Picking',
      timestamps: {'Placed': placed, 'Confirmed': confirmed},
    )));
    await tester.pump();
    expect(find.text(shortTime(placed)), findsOneWidget);
    expect(find.text(shortTime(confirmed)), findsOneWidget);
    expect(tester.takeException(), isNull);
  });
}
