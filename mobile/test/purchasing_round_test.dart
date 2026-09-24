import 'package:flutter_test/flutter_test.dart';
import 'package:zipzap/models/delivery.dart';
import 'package:zipzap/models/order.dart';
import 'package:zipzap/models/purchasing_round.dart';

Map<String, dynamic> _round({String cutoffDate = '2030-04-03', String cutoffTime = '11:30:00'}) => {
      'localDate': '2030-04-03',
      'localTime': '12:00:00',
      'cutoffLocalDate': cutoffDate,
      'cutoffLocalTime': cutoffTime,
      'timeZone': 'Europe/Warsaw',
      'startsAtUtc': '2030-04-03T10:00:00Z',
      'cutoffAtUtc': '2030-04-03T09:30:00Z',
    };

TimeSlot _slot(String date, String start) => TimeSlot(
      id: 's',
      storeId: 'st',
      deliveryZoneId: 'z',
      date: date,
      startTime: start,
      endTime: '23:00:00',
      maxOrders: 10,
      reservedCount: 0,
      remainingCapacity: 10,
    );

void main() {
  final preview = RoundPreview.fromJson({
    'available': true,
    'round': _round(),
    'earliestDelivery': {'localDate': '2030-04-03', 'localTime': '13:00:00', 'atUtc': '2030-04-03T11:00:00Z'},
  });

  test('parses round with UTC instants and local cutoff', () {
    final r = preview.round!;
    expect(r.startsAtUtc, DateTime.utc(2030, 4, 3, 10));
    expect(r.cutoffAtUtc.isUtc, isTrue);
    expect(r.cutoffLabel, '11:30');
  });

  test('cutoff on a different day than the round shows its date', () {
    final r = PurchasingRound.fromJson(_round(cutoffDate: '2030-04-02', cutoffTime: '23:45:00'));
    expect(r.cutoffLabel, endsWith('23:45'));
    expect(r.cutoffLabel, isNot('23:45'));
  });

  test('slots before round + lead are not allowed; at or after are allowed', () {
    expect(preview.allowsSlot(_slot('2030-04-03', '12:00:00')), isFalse);
    expect(preview.allowsSlot(_slot('2030-04-03', '13:00:00')), isTrue);
    expect(preview.allowsSlot(_slot('2030-04-04', '08:00:00')), isTrue);
    expect(preview.allowsSlot(_slot('2030-04-02', '18:00:00')), isFalse);
  });

  test('unavailable preview (store closed) allows no slot and keeps the message', () {
    final closed = RoundPreview.fromJson({
      'available': false,
      'reason': 'store_closed',
      'message': 'Sklep jest teraz zamknięty — nie przyjmuje zamówień.',
      'round': null,
      'earliestDelivery': null,
    });
    expect(closed.round, isNull);
    expect(closed.message, contains('zamknięty'));
    expect(closed.allowsSlot(_slot('2030-04-05', '18:00:00')), isFalse);
  });

  test('order carries its purchasing round (absent for legacy orders)', () {
    Map<String, dynamic> order([Map<String, dynamic>? round]) => {
          'id': 'o',
          'storeId': 'st',
          'status': 'Placed',
          'deliveryZoneId': 'z',
          'timeSlotId': 's',
          'placedAtUtc': '2030-04-03T08:00:00Z',
          'purchasingRound': ?round,
        };
    expect(Order.fromJson(order(_round())).purchasingRound!.localTime, '12:00:00');
    expect(Order.fromJson(order()).purchasingRound, isNull);
  });
}
