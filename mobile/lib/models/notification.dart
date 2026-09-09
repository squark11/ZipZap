class AppNotification {
  final String id;
  final String template;
  final String payload;
  final String status;
  final bool isRead;
  final DateTime createdAtUtc;

  AppNotification({
    required this.id,
    required this.template,
    required this.payload,
    required this.status,
    required this.isRead,
    required this.createdAtUtc,
  });

  /// Czytelny tytuł powiadomienia na podstawie szablonu.
  String get title {
    switch (template) {
      case 'customer.welcome':
        return 'Witaj w ZipZap';
      case 'order.placed':
        return 'Zamówienie złożone';
      case 'payment.authorized':
        return 'Płatność potwierdzona';
      case 'order.ready':
        return 'Zamówienie gotowe';
      case 'order.in_delivery':
        return 'Zamówienie w dostawie';
      case 'order.delivered':
        return 'Zamówienie dostarczone';
      default:
        return template;
    }
  }

  factory AppNotification.fromJson(Map<String, dynamic> j) => AppNotification(
        id: j['id'].toString(),
        template: j['template'] ?? '',
        payload: j['payload']?.toString() ?? '',
        status: j['status'] ?? '',
        isRead: j['isRead'] == true,
        createdAtUtc: DateTime.parse(j['createdAtUtc'].toString()),
      );
}
