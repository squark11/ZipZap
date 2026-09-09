import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/api/api_exception.dart';
import '../../core/providers.dart';
import '../../models/cart.dart';
import 'ordering_repository.dart';

class CartState {
  final Cart? cart;
  final bool busy;
  final String? error;

  const CartState({this.cart, this.busy = false, this.error});

  CartState copyWith({Cart? cart, bool? busy, String? error}) => CartState(
        cart: cart ?? this.cart,
        busy: busy ?? this.busy,
        error: error,
      );

  int get count => cart?.totalQuantity ?? 0;
  double get subtotal => cart?.subtotal ?? 0;
  bool get isEmpty => (cart?.items.isEmpty ?? true);
}

/// Koszyk klienta (jeden aktywny na sklep). Token koszyka trzymamy w [Cart].
class CartController extends Notifier<CartState> {
  OrderingRepository get _repo => ref.read(orderingRepositoryProvider);

  @override
  CartState build() => const CartState();

  /// Zapewnia aktywny koszyk dla danego sklepu. Zmiana sklepu tworzy nowy koszyk.
  Future<void> ensureCartForStore(String storeId) async {
    final current = state.cart;
    if (current != null && current.storeId == storeId && current.status == 'Active') return;
    state = state.copyWith(busy: true, error: null);
    try {
      final cart = await _repo.createCart(storeId);
      state = CartState(cart: cart);
    } on ApiException catch (e) {
      state = state.copyWith(busy: false, error: e.message);
    }
  }

  Future<void> add(String productId, {int quantity = 1}) async {
    final c = state.cart;
    if (c == null) return;
    await _mutate(() => _repo.addItem(c.id, c.cartToken, productId, quantity));
  }

  Future<void> setQuantity(String productId, int quantity) async {
    final c = state.cart;
    if (c == null) return;
    if (quantity <= 0) {
      await remove(productId);
      return;
    }
    await _mutate(() => _repo.setQuantity(c.id, c.cartToken, productId, quantity));
  }

  Future<void> remove(String productId) async {
    final c = state.cart;
    if (c == null) return;
    await _mutate(() => _repo.removeItem(c.id, c.cartToken, productId));
  }

  Future<void> reload() async {
    final c = state.cart;
    if (c == null) return;
    await _mutate(() => _repo.getCart(c.id, c.cartToken));
  }

  /// Po złożeniu zamówienia — czyścimy lokalny koszyk.
  void clearAfterCheckout() => state = const CartState();

  int quantityOf(String productId) {
    final items = state.cart?.items ?? const [];
    for (final i in items) {
      if (i.productId == productId) return i.quantity;
    }
    return 0;
  }

  // Serializacja mutacji: szybkie, współbieżne dodania/zmiany nie mogą nadpisać
  // stanu starszą odpowiedzią serwera (inaczej UI pokazywałby nieaktualną kwotę).
  Future<void> _lane = Future.value();

  Future<void> _mutate(Future<Cart> Function() op) {
    _lane = _lane.then((_) async {
      state = state.copyWith(busy: true, error: null);
      try {
        final cart = await op();
        state = CartState(cart: cart);
      } on ApiException catch (e) {
        state = state.copyWith(busy: false, error: e.message);
      }
    });
    return _lane;
  }
}

final cartControllerProvider =
    NotifierProvider<CartController, CartState>(CartController.new);
