// Redesign, Etap 3 (E3-3) — koszyk: pusty stan z ilustracją i przyciskami, pasek postępu do minimum zamówienia,
// pozycje (usuń ≥ 48 px), termin rundy, tekst 130 %, „ogranicz ruch". `cart_controller` bez zmian — test
// podstawia tylko stan koszyka i zapisuje wywołane akcje.
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:zipzap/core/api/api_client.dart';
import 'package:zipzap/core/api/api_exception.dart';
import 'package:zipzap/core/navigation/last_store.dart';
import 'package:zipzap/core/providers.dart';
import 'package:zipzap/core/theme/zz_theme.dart';
import 'package:zipzap/core/ui/ui.dart';
import 'package:zipzap/core/util/format.dart';
import 'package:zipzap/features/cart/cart_controller.dart';
import 'package:zipzap/features/cart/cart_screen.dart';
import 'package:zipzap/features/cart/ordering_repository.dart';
import 'package:zipzap/features/catalog/catalog_repository.dart';
import 'package:zipzap/models/cart.dart';
import 'package:zipzap/models/purchasing_round.dart';
import 'package:zipzap/models/store.dart';

const _sid = '22222222-2222-2222-2222-222222222222';

Cart _cart(List<(String, int, double)> items) => Cart.fromJson({
      'id': 'c1', 'storeId': _sid, 'cartToken': 't', 'status': 'Active',
      'subtotal': items.fold<double>(0, (a, i) => a + i.$2 * i.$3),
      'items': [
        for (final (name, q, price) in items)
          {'productId': 'p-$name', 'productName': name, 'unitPrice': price, 'unit': 'szt', 'quantity': q,
            'lineTotal': q * price},
      ],
    });

class _FakeCart extends CartController {
  _FakeCart(this.initial);
  final CartState initial;
  final calls = <String>[];
  @override
  CartState build() => initial;
  @override
  Future<void> setQuantity(String productId, int quantity) async => calls.add('set $productId $quantity');
  @override
  Future<void> remove(String productId) async => calls.add('remove $productId');
}

class _NoLastStore extends LastStoreController {
  @override
  LastStore? build() => null;
}

class _LastStore extends LastStoreController {
  @override
  LastStore? build() => const LastStore(id: _sid, slug: 'lewiatan', name: 'Lewiatan');
}

class _Catalog extends CatalogRepository {
  _Catalog() : super(_Offline());
  @override
  Future<Store> getStore(String idOrSlug) async => Store.fromJson({
        'id': _sid, 'slug': 'lewiatan', 'name': 'Lewiatan — Podwawelskie', 'city': 'Kraków', 'status': 'Open',
        'minimumOrderValue': 15, 'isAcceptingOrders': true,
      });
}

class _Ordering extends OrderingRepository {
  _Ordering() : super(_Offline());
  @override
  Future<RoundPreview> getRoundPreview(String storeId) async {
    final n = DateTime.now();
    final d = '${n.year}-${n.month.toString().padLeft(2, '0')}-${n.day.toString().padLeft(2, '0')}';
    return RoundPreview.fromJson({
      'available': true,
      'round': {'localDate': d, 'localTime': '16:00:00', 'cutoffLocalDate': d, 'cutoffLocalTime': '15:30:00',
        'timeZone': 'Europe/Warsaw', 'startsAtUtc': '${d}T15:00:00Z', 'cutoffAtUtc': '${d}T14:30:00Z'},
      'earliestDelivery': {'localDate': d, 'localTime': '17:00:00'},
    });
  }
}

class _Offline extends ApiClient {
  @override
  Future<dynamic> get(String path, {Map<String, dynamic>? query}) async =>
      throw ApiException(code: 'network', message: 'offline');
}

Future<_FakeCart> _pump(WidgetTester tester, CartState state,
    {bool lastStore = false, double textScale = 1, bool reduce = false, Size size = const Size(360, 900)}) async {
  tester.view.physicalSize = size;
  tester.view.devicePixelRatio = 1;
  addTearDown(tester.view.reset);
  final fake = _FakeCart(state);
  await tester.pumpWidget(ProviderScope(
    overrides: [
      apiClientProvider.overrideWithValue(_Offline()),
      catalogRepositoryProvider.overrideWithValue(_Catalog()),
      orderingRepositoryProvider.overrideWithValue(_Ordering()),
      cartControllerProvider.overrideWith(() => fake),
      lastStoreProvider.overrideWith(lastStore ? _LastStore.new : _NoLastStore.new),
    ],
    child: MaterialApp.router(
      theme: ZzTheme.light(),
      builder: (context, child) => MediaQuery(
        data: MediaQuery.of(context).copyWith(textScaler: TextScaler.linear(textScale), disableAnimations: reduce),
        child: child!,
      ),
      routerConfig: GoRouter(routes: [
        GoRoute(path: '/', builder: (_, _) => const CartScreen()),
        GoRoute(path: '/stores', builder: (_, _) => const Scaffold(body: Text('LISTA SKLEPÓW'))),
        GoRoute(path: '/s/:slug', builder: (_, s) => Scaffold(body: Text('SKLEP ${s.pathParameters['slug']}'))),
        GoRoute(path: '/checkout', builder: (_, _) => const Scaffold(body: Text('CHECKOUT'))),
      ]),
    ),
  ));
  await tester.pumpAndSettle();
  return fake;
}

void main() {
  testWidgets('pusty koszyk: ilustracja i „Przeglądaj sklepy"', (tester) async {
    await _pump(tester, const CartState());
    expect(find.text('Koszyk jest pusty'), findsOneWidget);
    expect(find.textContaining('Wróć do'), findsNothing);
    await tester.tap(find.text('Przeglądaj sklepy'));
    await tester.pumpAndSettle();
    expect(find.text('LISTA SKLEPÓW'), findsOneWidget);
  });

  testWidgets('pusty koszyk po wizycie w sklepie: „Wróć do: {sklep}" prowadzi do jego oferty', (tester) async {
    await _pump(tester, const CartState(), lastStore: true);
    await tester.tap(find.text('Wróć do: Lewiatan'));
    await tester.pumpAndSettle();
    expect(find.text('SKLEP lewiatan'), findsOneWidget);
  });

  testWidgets('poniżej minimum: pasek z brakującą kwotą, przejście dalej zablokowane', (tester) async {
    await _pump(tester, CartState(cart: _cart([('Jabłka', 2, 4.99)])));
    expect(find.text('Brakuje ${zl(5.02)} do minimum ${zl(15)}'), findsOneWidget);
    final btn = tester.widget<ElevatedButton>(find.widgetWithText(ElevatedButton, 'Przejdź do dostawy i płatności'));
    expect(btn.onPressed, isNull);
    expect(find.text('Razem bez dostawy'), findsOneWidget);
  });

  testWidgets('minimum osiągnięte: komunikat i przejście do zamówienia', (tester) async {
    await _pump(tester, CartState(cart: _cart([('Jabłka', 2, 4.99), ('Parmezan', 1, 12.90)])));
    expect(find.text('Minimum zamówienia osiągnięte (${zl(15)})'), findsOneWidget);
    await tester.tap(find.text('Przejdź do dostawy i płatności'));
    await tester.pumpAndSettle();
    expect(find.text('CHECKOUT'), findsOneWidget);
  });

  testWidgets('pozycja: usuń ≥ 48 px z etykietą, licznik woła ten sam kontroler', (tester) async {
    final cart = await _pump(tester, CartState(cart: _cart([('Mleko 2%', 1, 3.49)])));
    expect(find.text('1 × ${zl(3.49)} / szt'), findsOneWidget);
    final remove = find.byTooltip('Usuń: Mleko 2%');
    expect(tester.getSize(remove).width, greaterThanOrEqualTo(zzMinTouch));
    expect(tester.getSize(remove).height, greaterThanOrEqualTo(zzMinTouch));
    await tester.tap(remove);
    await tester.tap(find.bySemanticsLabel('Więcej: Mleko 2%'));
    expect(cart.calls, ['remove p-Mleko 2%', 'set p-Mleko 2% 2']);
  });

  testWidgets('termin rundy z serwera pod pozycjami', (tester) async {
    await _pump(tester, CartState(cart: _cart([('Mleko 2%', 1, 3.49)])));
    expect(find.text('Zakupy w sklepie: Dziś, 16:00. Zamów do 15:30. Godzinę dostawy wybierzesz w następnym kroku.'),
        findsOneWidget);
  });

  for (final w in [360.0, 768.0]) {
    testWidgets('${w.toInt()} px, tekst 130 %: bez przepełnień', (tester) async {
      final errors = <String>[];
      final previous = FlutterError.onError;
      FlutterError.onError = (d) => errors.add(d.exceptionAsString());
      await _pump(tester, CartState(cart: _cart([('Bardzo długa nazwa produktu z regionu', 12, 1234.5)])),
          textScale: 1.3, size: Size(w, 900));
      FlutterError.onError = previous;
      expect(errors, isEmpty);
    });
  }

  testWidgets('„ogranicz ruch": pasek postępu bez animacji', (tester) async {
    await _pump(tester, CartState(cart: _cart([('Jabłka', 2, 4.99)])), reduce: true);
    final tween = tester.widget<TweenAnimationBuilder<double>>(find.descendant(
        of: find.byType(ZzMinOrderProgress), matching: find.byType(TweenAnimationBuilder<double>)));
    expect(tween.duration, Duration.zero);
  });

  testWidgets('podsumowanie: wartość przy prawej krawędzi także na szerokim ekranie', (tester) async {
    await _pump(tester, CartState(cart: _cart([('Mleko 2%', 1, 3.49)])), size: const Size(640, 900));
    final row = tester.getRect(find.widgetWithText(ZzSummaryRow, 'Razem bez dostawy'));
    final value = tester.getRect(find.descendant(of: find.widgetWithText(ZzSummaryRow, 'Razem bez dostawy'),
        matching: find.text(zl(3.49))));
    expect(row.right - value.right, lessThan(1));
  });
}
