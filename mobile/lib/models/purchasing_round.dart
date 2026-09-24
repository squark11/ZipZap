import '../core/util/format.dart';
import 'delivery.dart';

String _hhmm(String t) => t.length >= 5 ? t.substring(0, 5) : t;

/// Runda zakupowa (kiedy sklep kompletuje zamówienia) — godziny LOKALNE strefy sklepu
/// (Europe/Warsaw), niezależnie od strefy telefonu. To NIE jest okno dostawy.
class PurchasingRound {
  final String localDate; // yyyy-MM-dd
  final String localTime; // HH:mm:ss
  final String cutoffLocalDate;
  final String cutoffLocalTime;
  final String timeZone;
  final DateTime startsAtUtc;
  final DateTime cutoffAtUtc;

  PurchasingRound({
    required this.localDate,
    required this.localTime,
    required this.cutoffLocalDate,
    required this.cutoffLocalTime,
    required this.timeZone,
    required this.startsAtUtc,
    required this.cutoffAtUtc,
  });

  /// „Dziś, 12:00".
  String get label => '${friendlyDate(localDate)}, ${_hhmm(localTime)}';

  /// „11:30" albo „Jutro, 23:45", gdy termin graniczny wypada innego dnia niż runda.
  String get cutoffLabel => cutoffLocalDate == localDate
      ? _hhmm(cutoffLocalTime)
      : '${friendlyDate(cutoffLocalDate)}, ${_hhmm(cutoffLocalTime)}';

  factory PurchasingRound.fromJson(Map<String, dynamic> j) => PurchasingRound(
        localDate: j['localDate']?.toString() ?? '',
        localTime: j['localTime']?.toString() ?? '',
        cutoffLocalDate: j['cutoffLocalDate']?.toString() ?? '',
        cutoffLocalTime: j['cutoffLocalTime']?.toString() ?? '',
        timeZone: j['timeZone']?.toString() ?? 'Europe/Warsaw',
        startsAtUtc: DateTime.parse(j['startsAtUtc'].toString()).toUtc(),
        cutoffAtUtc: DateTime.parse(j['cutoffAtUtc'].toString()).toUtc(),
      );
}

/// Podgląd przed zamówieniem: najbliższa runda + najwcześniejszy możliwy początek dostawy.
class RoundPreview {
  final bool available;
  final String? reason; // store_closed | no_round
  final String? message;
  final PurchasingRound? round;
  final String? earliestDeliveryDate; // yyyy-MM-dd (lokalnie)
  final String? earliestDeliveryTime; // HH:mm:ss (lokalnie)

  RoundPreview({
    required this.available,
    this.reason,
    this.message,
    this.round,
    this.earliestDeliveryDate,
    this.earliestDeliveryTime,
  });

  String? get earliestDeliveryLabel {
    final d = earliestDeliveryDate, t = earliestDeliveryTime;
    if (d == null || t == null) return null;
    return '${friendlyDate(d)}, ${_hhmm(t)}';
  }

  /// Czy termin dostawy zaczyna się nie wcześniej niż po zakupach w rundzie.
  /// Porównanie lokalnych dat/godzin ISO (ta sama strefa sklepu) — serwer i tak to egzekwuje.
  bool allowsSlot(TimeSlot s) {
    final d = earliestDeliveryDate, t = earliestDeliveryTime;
    if (!available || d == null || t == null) return false;
    return '${s.date} ${_hhmm(s.startTime)}'.compareTo('$d ${_hhmm(t)}') >= 0;
  }

  factory RoundPreview.fromJson(Map<String, dynamic> j) {
    final e = j['earliestDelivery'] as Map<String, dynamic>?;
    final r = j['round'] as Map<String, dynamic>?;
    return RoundPreview(
      available: j['available'] == true,
      reason: j['reason']?.toString(),
      message: j['message']?.toString(),
      round: r == null ? null : PurchasingRound.fromJson(r),
      earliestDeliveryDate: e?['localDate']?.toString(),
      earliestDeliveryTime: e?['localTime']?.toString(),
    );
  }
}
