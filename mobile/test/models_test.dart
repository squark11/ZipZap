import 'package:flutter_test/flutter_test.dart';
import 'package:zipzap/models/cart.dart';
import 'package:zipzap/models/order.dart';
import 'package:zipzap/models/payment.dart';
import 'package:zipzap/models/store.dart';

void main() {
  group('Cart', () {
    test('parses items and sums quantity', () {
      final cart = Cart.fromJson({
        'id': 'c1',
        'storeId': 's1',
        'cartToken': 'tok',
        'status': 'Active',
        'subtotal': 16.5,
        'items': [
          {'productId': 'p1', 'productName': 'Chleb', 'unitPrice': 5.5, 'quantity': 3, 'lineTotal': 16.5},
        ],
      });
      expect(cart.items.length, 1);
      expect(cart.totalQuantity, 3);
      expect(cart.subtotal, 16.5);
    });

    test('empty items default to zero quantity', () {
      final cart = Cart.fromJson({
        'id': 'c1',
        'storeId': 's1',
        'cartToken': 'tok',
        'status': 'Active',
        'subtotal': 0,
      });
      expect(cart.totalQuantity, 0);
    });
  });

  group('Store', () {
    test('isAcceptingOrders reflects backend flag', () {
      final open = Store.fromJson({'id': 's1', 'name': 'A', 'city': 'X', 'isAcceptingOrders': true});
      final closed = Store.fromJson({'id': 's2', 'name': 'B', 'city': 'X', 'isAcceptingOrders': false});
      expect(open.isAcceptingOrders, true);
      expect(closed.isAcceptingOrders, false);
    });
  });

  group('Payment', () {
    test('status helpers', () {
      final pending = PaymentInfo.fromJson({'orderId': 'o1', 'status': 'Pending', 'amount': 23.5, 'deliveryFee': 7});
      final auth = PaymentInfo.fromJson({'orderId': 'o1', 'status': 'Authorized', 'amount': 23.5, 'deliveryFee': 7});
      final failed = PaymentInfo.fromJson({'orderId': 'o1', 'status': 'Failed', 'amount': 23.5, 'deliveryFee': 7});
      expect(pending.isPending, true);
      expect(auth.isAuthorized, true);
      expect(failed.isFailed, true);
    });
  });

  group('Order', () {
    test('keeps delivery fee separate from subtotal', () {
      final order = Order.fromJson({
        'id': 'o1',
        'storeId': 's1',
        'status': 'Placed',
        'subtotal': 16.5,
        'commissionAmount': 1.65,
        'deliveryFee': 7.0,
        'total': 23.5,
        'currency': 'PLN',
        'deliveryZoneId': 'z1',
        'timeSlotId': 't1',
        'deliveryAddress': 'ul. X 1',
        'contactPhone': '600',
        'placedAtUtc': '2026-09-09T10:00:00Z',
        'items': const [],
        'history': const [],
      });
      // Klient płaci osobno za dostawę; prowizja nie wchodzi do total klienta.
      expect(order.subtotal + order.deliveryFee, order.total);
    });
  });
}
