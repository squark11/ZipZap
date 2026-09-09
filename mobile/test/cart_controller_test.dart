import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:zipzap/core/api/api_client.dart';
import 'package:zipzap/core/providers.dart';
import 'package:zipzap/features/cart/cart_controller.dart';
import 'package:zipzap/features/cart/ordering_repository.dart';
import 'package:zipzap/models/cart.dart';

/// Fake, który zwraca koszyk odzwierciedlający stan „serwera" w chwili
/// przetworzenia dodania (snapshot przed opóźnieniem). Pierwsze dodanie jest
/// wolniejsze niż drugie — bez serializacji jego starsza odpowiedź nadpisałaby
/// nowszą (klasyczny wyścig).
class _FakeOrderingRepo extends OrderingRepository {
  _FakeOrderingRepo() : super(ApiClient());
  final List<String> _items = [];
  int _addCall = 0;

  @override
  Future<Cart> createCart(String storeId) async =>
      Cart(id: 'c1', storeId: storeId, cartToken: 't', status: 'Active', subtotal: 0, items: const []);

  @override
  Future<Cart> addItem(String cartId, String token, String productId, int quantity) async {
    _items.add(productId);
    final snapshot = List<String>.from(_items);
    final delayMs = _addCall == 0 ? 60 : 10;
    _addCall++;
    await Future.delayed(Duration(milliseconds: delayMs));
    return _cartOf(cartId, snapshot);
  }

  Cart _cartOf(String cartId, List<String> items) => Cart(
        id: cartId,
        storeId: 's1',
        cartToken: 't',
        status: 'Active',
        subtotal: items.length.toDouble(),
        items: items
            .map((p) => CartItem(
                productId: p, productName: p, unitPrice: 1, quantity: 1, lineTotal: 1))
            .toList(),
      );
}

void main() {
  test('concurrent adds are serialized (no stale overwrite)', () async {
    final container = ProviderContainer(overrides: [
      orderingRepositoryProvider.overrideWithValue(_FakeOrderingRepo()),
    ]);
    addTearDown(container.dispose);

    final controller = container.read(cartControllerProvider.notifier);
    await controller.ensureCartForStore('s1');

    // Dwa szybkie dodania bez czekania — pierwsze wolniejsze niż drugie.
    final f1 = controller.add('maslo');
    final f2 = controller.add('mleko');
    await Future.wait([f1, f2]);

    final state = container.read(cartControllerProvider);
    expect(state.cart!.items.length, 2,
        reason: 'Oba produkty muszą zostać w koszyku mimo wyścigu odpowiedzi.');
  });
}
