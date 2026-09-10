import 'package:flutter_test/flutter_test.dart';
import 'package:zipzap/models/order.dart';

String _pad(int n) => n.toString().padLeft(2, '0');

Map<String, dynamic> _base() => {
      'id': 'o1',
      'storeId': 's1',
      'status': 'Confirmed',
      'placedAtUtc': '2026-09-10T12:00:00Z',
    };

void main() {
  test('deliveryWindowLabel = "Dziś, 14:00–16:00" for today', () {
    final now = DateTime.now();
    final iso = '${now.year}-${_pad(now.month)}-${_pad(now.day)}';
    final o = Order.fromJson({
      ..._base(),
      'deliveryDate': iso,
      'deliveryStartTime': '14:00:00',
      'deliveryEndTime': '16:00:00',
    });
    expect(o.deliveryWindowLabel, 'Dziś, 14:00–16:00');
  });

  test('deliveryWindowLabel is null when the window is absent', () {
    final o = Order.fromJson(_base());
    expect(o.deliveryWindowLabel, isNull);
  });
}
