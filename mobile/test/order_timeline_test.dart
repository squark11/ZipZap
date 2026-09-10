import 'package:flutter/material.dart';
import 'package:flutter_svg/flutter_svg.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:zipzap/features/orders/order_track_screen.dart';

Widget _host(Widget child) =>
    MaterialApp(home: Scaffold(body: SingleChildScrollView(child: child)));

void main() {
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
}
