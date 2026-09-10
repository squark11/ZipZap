import '../core/util/format.dart';

class DeliveryZone {
  final String id;
  final String storeId;
  final String name;
  final double deliveryFee;
  final bool isActive;

  DeliveryZone({
    required this.id,
    required this.storeId,
    required this.name,
    required this.deliveryFee,
    required this.isActive,
  });

  factory DeliveryZone.fromJson(Map<String, dynamic> j) => DeliveryZone(
        id: j['id'].toString(),
        storeId: j['storeId'].toString(),
        name: j['name'] ?? '',
        deliveryFee: (j['deliveryFee'] as num?)?.toDouble() ?? 0,
        isActive: j['isActive'] == true,
      );
}

class TimeSlot {
  final String id;
  final String storeId;
  final String deliveryZoneId;
  final String date; // yyyy-MM-dd
  final String startTime; // HH:mm:ss
  final String endTime;
  final int maxOrders;
  final int reservedCount;
  final int remainingCapacity;

  TimeSlot({
    required this.id,
    required this.storeId,
    required this.deliveryZoneId,
    required this.date,
    required this.startTime,
    required this.endTime,
    required this.maxOrders,
    required this.reservedCount,
    required this.remainingCapacity,
  });

  /// Czytelna etykieta fali dostawy, np. „Dziś, 14:00–16:00" / „10.09, 10:00–12:00".
  String get label {
    final s = startTime.length >= 5 ? startTime.substring(0, 5) : startTime;
    final e = endTime.length >= 5 ? endTime.substring(0, 5) : endTime;
    return '${friendlyDate(date)}, $s–$e';
  }

  factory TimeSlot.fromJson(Map<String, dynamic> j) => TimeSlot(
        id: j['id'].toString(),
        storeId: j['storeId'].toString(),
        deliveryZoneId: j['deliveryZoneId'].toString(),
        date: j['date']?.toString() ?? '',
        startTime: j['startTime']?.toString() ?? '',
        endTime: j['endTime']?.toString() ?? '',
        maxOrders: (j['maxOrders'] as num?)?.toInt() ?? 0,
        reservedCount: (j['reservedCount'] as num?)?.toInt() ?? 0,
        remainingCapacity: (j['remainingCapacity'] as num?)?.toInt() ?? 0,
      );
}
