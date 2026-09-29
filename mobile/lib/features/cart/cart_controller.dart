import 'dart:async';
import 'dart:convert';

import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/api/api_exception.dart';
import '../../core/providers.dart';
import '../../models/cart.dart';
import 'ordering_repository.dart';

class CartState {
  final Cart? cart;
  final bool busy;
  final String? error;

  /// Trwa odtwarzanie zapamiętanego koszyka (np. zaraz po odświeżeniu strony) — ekrany pokazują ładowanie,
  /// a nie mylący komunikat „Koszyk jest pusty".
  final bool restoring;

  const CartState({this.cart, this.busy = false, this.error, this.restoring = false});

  CartState copyWith({Cart? cart, bool? busy, String? error, bool? restoring}) => CartState(
        cart: cart ?? this.cart,
        busy: busy ?? this.busy,
        error: error,
        restoring: restoring ?? this.restoring,
      );

  int get count => cart?.totalQuantity ?? 0;
  double get subtotal => cart?.subtotal ?? 0;
  bool get isEmpty => (cart?.items.isEmpty ?? true);
}

/// Koszyk klienta (jeden aktywny na sklep). Token koszyka trzymamy w [Cart].
///
/// Odnośnik do koszyka (id + token, bez zawartości) jest zapisywany w trwałym magazynie, więc odświeżenie strony
/// w przeglądarce, przejście przez logowanie czy ponowne otwarcie aplikacji nie gubią koszyka — zawartość
/// zawsze pobieramy od nowa z serwera.
class CartController extends Notifier<CartState> {
  static const storageKey = 'zz_cart';

  OrderingRepository get _repo => ref.read(orderingRepositoryProvider);

  @override
  CartState build() {
    unawaited(_restore().whenComplete(() {
      if (state.restoring) state = state.copyWith(restoring: false);
    }));
    return const CartState(restoring: true);
  }

  /// Odtwarza koszyk zapisany przed odświeżeniem strony. Nie nadpisuje koszyka założonego w międzyczasie.
  Future<void> _restore() async {
    final raw = await ref.read(appStorageProvider).read(storageKey);
    if (raw == null) return;
    String? id, token;
    try {
      final j = jsonDecode(raw);
      if (j is Map && j['id'] is String && j['token'] is String) {
        id = j['id'] as String;
        token = j['token'] as String;
      }
    } catch (_) {}
    if (id == null || token == null) return _forget();
    try {
      final cart = await _repo.getCart(id, token);
      if (state.cart != null) return;
      if (cart.status == 'Active') {
        state = CartState(cart: cart);
      } else {
        await _forget(); // koszyk już zamówiony / porzucony
      }
    } on ApiException catch (e) {
      // Koszyk nie istnieje lub token jest nieważny — zapominamy. Błąd sieci/serwera: próbujemy przy następnym starcie.
      final gone = e.isNotFound || e.isForbidden || e.statusCode == 400 || e.statusCode == 410;
      if (gone) await _forget();
    } catch (_) {}
  }

  void _setCart(Cart cart) {
    state = CartState(cart: cart);
    unawaited(ref.read(appStorageProvider)
        .write(storageKey, jsonEncode({'id': cart.id, 'token': cart.cartToken})));
  }

  Future<void> _forget() => ref.read(appStorageProvider).delete(storageKey);

  /// Zapewnia aktywny koszyk dla danego sklepu. Zmiana sklepu tworzy nowy koszyk.
  Future<void> ensureCartForStore(String storeId) async {
    final current = state.cart;
    if (current != null && current.storeId == storeId && current.status == 'Active') return;
    state = state.copyWith(busy: true, error: null);
    try {
      final cart = await _repo.createCart(storeId);
      _setCart(cart);
    } on ApiException catch (e) {
      state = state.copyWith(busy: false, error: e.message);
    }
  }

  Future<void> add(String productId, {int quantity = 1, String? unit}) async {
    final c = state.cart;
    if (c == null) return;
    await _mutate(() => _repo.addItem(c.id, c.cartToken, productId, quantity, unit: unit));
  }

  /// Koszyk jest zawsze **jednosklepowy** (backend odrzuca produkt z innego sklepu).
  /// True, jeśli w koszyku są już produkty z INNEGO sklepu niż [storeId] — wtedy UI
  /// musi potwierdzić opróżnienie koszyka, zanim zacznie zakupy w nowym sklepie.
  bool hasItemsFromOtherStore(String storeId) {
    final c = state.cart;
    return c != null && c.storeId != storeId && state.count > 0;
  }

  /// Dodaje produkt, dbając o zgodność sklepu: jeśli aktywny koszyk jest z innego
  /// sklepu (lub go nie ma / jest zamknięty), tworzy **nowy** koszyk dla [storeId]
  /// i dopiero dodaje. Konflikt (niepusty koszyk z innego sklepu) powinien być już
  /// potwierdzony przez UI — patrz [hasItemsFromOtherStore].
  Future<void> addFromStore(String storeId, String productId, {int quantity = 1, String? unit}) async {
    final c = state.cart;
    if (c == null || c.storeId != storeId || c.status != 'Active') {
      await ensureCartForStore(storeId);
      if (state.cart == null) return; // błąd utworzenia koszyka — komunikat już w stanie
    }
    await add(productId, quantity: quantity, unit: unit);
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

  /// Po złożeniu zamówienia — czyścimy lokalny koszyk (i zapamiętany odnośnik).
  void clearAfterCheckout() {
    state = const CartState();
    unawaited(_forget());
  }

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
        _setCart(cart);
      } on ApiException catch (e) {
        state = state.copyWith(busy: false, error: e.message);
      }
    });
    return _lane;
  }
}

final cartControllerProvider =
    NotifierProvider<CartController, CartState>(CartController.new);
