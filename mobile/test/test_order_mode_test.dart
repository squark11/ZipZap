import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:zipzap/core/config/public_config.dart';
import 'package:zipzap/core/theme/zz_theme.dart';
import 'package:zipzap/core/widgets/test_order_banner.dart';
import 'package:zipzap/models/order.dart';

Map<String, dynamic> _orderJson({String? paymentMode}) => {
      'id': '11111111-1111-1111-1111-111111111111',
      'storeId': '22222222-2222-2222-2222-222222222222',
      'status': 'Placed',
      'subtotal': 12,
      'commissionAmount': 1.2,
      'deliveryFee': 8,
      'total': 20,
      'currency': 'PLN',
      'deliveryZoneId': '33333333-3333-3333-3333-333333333333',
      'timeSlotId': '44444444-4444-4444-4444-444444444444',
      'deliveryAddress': 'ul. Testowa 1',
      'contactPhone': '600100200',
      'placedAtUtc': '2026-09-24T10:00:00Z',
      'items': [],
      'history': [],
      'paymentMode': ?paymentMode,
    };

Widget _wrap(Widget child) => MaterialApp(theme: ZzTheme.light(), home: Scaffold(body: child));

void main() {
  test('order without paymentMode defaults to online (older API)', () {
    final o = Order.fromJson(_orderJson());
    expect(o.paymentMode, 'online');
    expect(o.isTestOrder, isFalse);
  });

  test('order with paymentMode=test is a test order', () {
    expect(Order.fromJson(_orderJson(paymentMode: 'test')).isTestOrder, isTrue);
  });

  test('public config defaults to online; test mode detected', () {
    expect(PublicConfig.fromJson({}).isTestOrdering, isFalse);
    expect(PublicConfig.fromJson({'paymentMode': 'test'}).isTestOrdering, isTrue);
  });

  testWidgets('placed test order shows "bez opłaty" and never "Do zapłaty"', (tester) async {
    await tester.pumpWidget(_wrap(const TestOrderBanner(placed: true)));
    expect(find.text('Zamówienie testowe — bez opłaty'), findsOneWidget);
    expect(find.textContaining('Do zapłaty'), findsNothing);
    expect(find.textContaining('potwierdzenie płatności'), findsNothing);
  });

  testWidgets('non-tester sees invitation-only notice', (tester) async {
    await tester.pumpWidget(_wrap(const TestOrderBanner(blocked: true)));
    expect(find.text('Pilotaż dla zaproszonych testerów'), findsOneWidget);
  });
}
