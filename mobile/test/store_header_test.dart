import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:zipzap/features/catalog/store_header.dart';
import 'package:zipzap/models/store.dart';

Store _store({
  bool open = true,
  String status = 'Open',
  String? description,
  String? phone,
}) =>
    Store(
      id: 's',
      name: 'Piekarnia Poranek',
      slug: 'p',
      city: 'Koło',
      status: status,
      description: description,
      address: null,
      phone: phone,
      minimumOrderValue: 15,
      isAcceptingOrders: open,
    );

Widget _host(Store s) =>
    MaterialApp(home: Scaffold(body: SingleChildScrollView(child: StoreHeader(store: s))));

void main() {
  testWidgets('shows name, city, min order, status and delivery note', (tester) async {
    await tester.pumpWidget(_host(_store(phone: '600 100 200')));
    expect(find.text('Piekarnia Poranek'), findsOneWidget);
    expect(find.text('Koło'), findsOneWidget);
    expect(find.textContaining('Min.'), findsOneWidget);
    expect(find.text('600 100 200'), findsOneWidget);
    expect(find.text('Otwarte'), findsOneWidget);
    expect(find.textContaining('Dostawa'), findsOneWidget);
    expect(tester.takeException(), isNull);
  });

  testWidgets('closed store shows Zamknięte', (tester) async {
    await tester.pumpWidget(_host(_store(open: false, status: 'Closed')));
    expect(find.text('Zamknięte'), findsOneWidget);
  });

  testWidgets('temporarily unavailable shows Niedostępne', (tester) async {
    await tester.pumpWidget(_host(_store(open: false, status: 'TemporarilyUnavailable')));
    expect(find.text('Niedostępne'), findsOneWidget);
  });
}
