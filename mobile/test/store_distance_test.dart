import 'package:flutter_test/flutter_test.dart';
import 'package:zipzap/models/store.dart';

Store _store(double? km) => Store(
      id: '1', name: 'X', slug: 'x', city: 'C', status: 'Open',
      description: null, address: null, phone: null,
      minimumOrderValue: 0, isAcceptingOrders: true, distanceKm: km,
    );

void main() {
  test('distanceLabel formatuje metry i kilometry', () {
    expect(_store(null).distanceLabel, isNull);
    expect(_store(0.85).distanceLabel, '850 m');
    expect(_store(0.05).distanceLabel, '50 m');
    expect(_store(2.34).distanceLabel, '2,3 km');
  });

  test('fromJson czyta distanceKm', () {
    final store = Store.fromJson({
      'id': '1', 'name': 'X', 'slug': 'x', 'city': 'C', 'status': 'Open',
      'minimumOrderValue': 0, 'isAcceptingOrders': true, 'distanceKm': 2.5,
    });
    expect(store.distanceKm, 2.5);
    expect(store.distanceLabel, '2,5 km');
  });
}
