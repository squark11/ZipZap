import '../../core/api/api_client.dart';
import '../../models/cart.dart';
import '../../models/delivery.dart';
import '../../models/order.dart';

class OrderingRepository {
  final ApiClient _api;
  OrderingRepository(this._api);

  Future<Cart> createCart(String storeId) async {
    final data = await _api.post('/ordering/carts', body: {'storeId': storeId});
    return Cart.fromJson(data as Map<String, dynamic>);
  }

  Future<Cart> getCart(String cartId, String token) async {
    final data = await _api.get('/ordering/carts/$cartId', query: {'token': token});
    return Cart.fromJson(data as Map<String, dynamic>);
  }

  Future<Cart> addItem(String cartId, String token, String productId, int quantity) async {
    final data = await _api.post('/ordering/carts/$cartId/items',
        query: {'token': token}, body: {'productId': productId, 'quantity': quantity});
    return Cart.fromJson(data as Map<String, dynamic>);
  }

  Future<Cart> setQuantity(String cartId, String token, String productId, int quantity) async {
    final data = await _api.put('/ordering/carts/$cartId/items/$productId',
        query: {'token': token}, body: {'quantity': quantity});
    return Cart.fromJson(data as Map<String, dynamic>);
  }

  Future<Cart> removeItem(String cartId, String token, String productId) async {
    final data =
        await _api.delete('/ordering/carts/$cartId/items/$productId', query: {'token': token});
    return Cart.fromJson(data as Map<String, dynamic>);
  }

  Future<List<DeliveryZone>> listZones(String storeId) async {
    final data = await _api.get('/ordering/stores/$storeId/zones');
    return (data as List).map((e) => DeliveryZone.fromJson(e as Map<String, dynamic>)).toList();
  }

  Future<List<TimeSlot>> listSlots(String storeId, {String? zoneId, String? date}) async {
    final q = <String, dynamic>{};
    if (zoneId != null) q['zoneId'] = zoneId;
    if (date != null) q['date'] = date;
    final data = await _api.get('/ordering/stores/$storeId/slots', query: q.isEmpty ? null : q);
    return (data as List).map((e) => TimeSlot.fromJson(e as Map<String, dynamic>)).toList();
  }

  Future<Order> checkout({
    required String cartId,
    required String cartToken,
    required String deliveryZoneId,
    required String timeSlotId,
    required String deliveryAddress,
    required String contactPhone,
    required String idempotencyKey,
  }) async {
    final data = await _api.post('/ordering/carts/$cartId/checkout',
        headers: {'Idempotency-Key': idempotencyKey},
        body: {
          'token': cartToken,
          'deliveryZoneId': deliveryZoneId,
          'timeSlotId': timeSlotId,
          'deliveryAddress': deliveryAddress,
          'contactPhone': contactPhone,
        });
    return Order.fromJson(data as Map<String, dynamic>);
  }

  Future<Order> getOrder(String orderId) async {
    final data = await _api.get('/ordering/orders/$orderId');
    return Order.fromJson(data as Map<String, dynamic>);
  }

  Future<List<Order>> listMyOrders() async {
    final data = await _api.get('/ordering/orders/mine');
    return (data as List).map((e) => Order.fromJson(e as Map<String, dynamic>)).toList();
  }
}
