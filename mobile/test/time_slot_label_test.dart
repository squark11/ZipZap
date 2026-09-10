import 'package:flutter_test/flutter_test.dart';
import 'package:zipzap/models/delivery.dart';

String _iso(DateTime d) =>
    '${d.year.toString().padLeft(4, '0')}-${d.month.toString().padLeft(2, '0')}-${d.day.toString().padLeft(2, '0')}';

TimeSlot _slot(String date) => TimeSlot(
      id: 's',
      storeId: 'st',
      deliveryZoneId: 'z',
      date: date,
      startTime: '14:00:00',
      endTime: '16:00:00',
      maxOrders: 5,
      reservedCount: 2,
      remainingCapacity: 3,
    );

void main() {
  final now = DateTime.now();

  test('today → "Dziś, 14:00–16:00"', () {
    expect(_slot(_iso(now)).label, 'Dziś, 14:00–16:00');
  });

  test('tomorrow → "Jutro, …"', () {
    expect(_slot(_iso(now.add(const Duration(days: 1)))).label, startsWith('Jutro, '));
  });

  test('later date → "dd.MM, …" (no ISO year)', () {
    final d = now.add(const Duration(days: 5));
    final label = _slot(_iso(d)).label;
    expect(label, startsWith('${d.day.toString().padLeft(2, '0')}.${d.month.toString().padLeft(2, '0')}, '));
    expect(label, isNot(contains('-'))); // nie surowy ISO
    expect(label, endsWith('14:00–16:00'));
  });
}
