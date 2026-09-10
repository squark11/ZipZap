import '../core/util/format.dart';

class OrderItem {
  final String productId;
  final String productName;
  final double unitPrice;
  final int quantity;
  final double lineTotal;

  OrderItem({
    required this.productId,
    required this.productName,
    required this.unitPrice,
    required this.quantity,
    required this.lineTotal,
  });

  factory OrderItem.fromJson(Map<String, dynamic> j) => OrderItem(
        productId: j['productId'].toString(),
        productName: j['productName'] ?? '',
        unitPrice: (j['unitPrice'] as num?)?.toDouble() ?? 0,
        quantity: (j['quantity'] as num?)?.toInt() ?? 0,
        lineTotal: (j['lineTotal'] as num?)?.toDouble() ?? 0,
      );
}

class OrderStatusChange {
  final String? fromStatus;
  final String toStatus;
  final DateTime changedAtUtc;

  OrderStatusChange({
    required this.fromStatus,
    required this.toStatus,
    required this.changedAtUtc,
  });

  factory OrderStatusChange.fromJson(Map<String, dynamic> j) => OrderStatusChange(
        fromStatus: j['fromStatus']?.toString(),
        toStatus: j['toStatus']?.toString() ?? '',
        changedAtUtc: DateTime.parse(j['changedAtUtc'].toString()),
      );
}

class Order {
  final String id;
  final String storeId;
  final String status;
  final double subtotal;
  final double commissionAmount;
  final double deliveryFee;
  final double total;
  final String currency;
  final String deliveryZoneId;
  final String timeSlotId;
  final String deliveryAddress;
  final String contactPhone;
  final DateTime placedAtUtc;
  final List<OrderItem> items;
  final List<OrderStatusChange> history;

  /// Okno dostawy (fala): data ISO + godziny (opcjonalne — zależne od API).
  final String? deliveryDate;
  final String? deliveryStartTime;
  final String? deliveryEndTime;

  /// Czytelne okno dostawy, np. „Dziś, 14:00–16:00" (null gdy brak danych).
  String? get deliveryWindowLabel {
    final st = deliveryStartTime, en = deliveryEndTime;
    if (st == null || en == null) return null;
    final s = st.length >= 5 ? st.substring(0, 5) : st;
    final e = en.length >= 5 ? en.substring(0, 5) : en;
    final d = deliveryDate;
    final datePart = (d != null && d.isNotEmpty) ? '${friendlyDate(d)}, ' : '';
    return '$datePart$s–$e';
  }

  Order({
    required this.id,
    required this.storeId,
    required this.status,
    required this.subtotal,
    required this.commissionAmount,
    required this.deliveryFee,
    required this.total,
    required this.currency,
    required this.deliveryZoneId,
    required this.timeSlotId,
    required this.deliveryAddress,
    required this.contactPhone,
    required this.placedAtUtc,
    required this.items,
    required this.history,
    this.deliveryDate,
    this.deliveryStartTime,
    this.deliveryEndTime,
  });

  factory Order.fromJson(Map<String, dynamic> j) => Order(
        id: j['id'].toString(),
        storeId: j['storeId'].toString(),
        status: j['status'] ?? '',
        subtotal: (j['subtotal'] as num?)?.toDouble() ?? 0,
        commissionAmount: (j['commissionAmount'] as num?)?.toDouble() ?? 0,
        deliveryFee: (j['deliveryFee'] as num?)?.toDouble() ?? 0,
        total: (j['total'] as num?)?.toDouble() ?? 0,
        currency: j['currency'] ?? 'PLN',
        deliveryZoneId: j['deliveryZoneId'].toString(),
        timeSlotId: j['timeSlotId'].toString(),
        deliveryAddress: j['deliveryAddress'] ?? '',
        contactPhone: j['contactPhone'] ?? '',
        placedAtUtc: DateTime.parse(j['placedAtUtc'].toString()),
        items: (j['items'] as List?)
                ?.map((e) => OrderItem.fromJson(e as Map<String, dynamic>))
                .toList() ??
            const [],
        history: (j['history'] as List?)
                ?.map((e) => OrderStatusChange.fromJson(e as Map<String, dynamic>))
                .toList() ??
            const [],
        deliveryDate: j['deliveryDate']?.toString(),
        deliveryStartTime: j['deliveryStartTime']?.toString(),
        deliveryEndTime: j['deliveryEndTime']?.toString(),
      );
}
