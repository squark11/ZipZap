import '../../core/api/api_client.dart';
import '../../models/payment.dart';

class PaymentsRepository {
  final ApiClient _api;
  PaymentsRepository(this._api);

  /// Odczyt WŁASNEJ płatności klienta (status + URL dostawcy).
  /// Płatność jest webhook-autorytatywna: odpytujemy status, nie ustawiamy go.
  Future<PaymentInfo> getMyPayment(String orderId) async {
    final data = await _api.get('/payments/orders/$orderId/mine');
    return PaymentInfo.fromJson(data as Map<String, dynamic>);
  }
}
