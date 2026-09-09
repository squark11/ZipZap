class PaymentInfo {
  final String orderId;
  final String status; // Pending | Authorized | Failed | Settled | Refunded
  final String? redirectUrl;
  final double amount;
  final double deliveryFee;

  PaymentInfo({
    required this.orderId,
    required this.status,
    required this.redirectUrl,
    required this.amount,
    required this.deliveryFee,
  });

  bool get isAuthorized => status == 'Authorized' || status == 'Settled';
  bool get isFailed => status == 'Failed';
  bool get isPending => status == 'Pending';

  factory PaymentInfo.fromJson(Map<String, dynamic> j) => PaymentInfo(
        orderId: j['orderId'].toString(),
        status: j['status'] ?? 'Pending',
        redirectUrl: j['redirectUrl'],
        amount: (j['amount'] as num?)?.toDouble() ?? 0,
        deliveryFee: (j['deliveryFee'] as num?)?.toDouble() ?? 0,
      );
}
