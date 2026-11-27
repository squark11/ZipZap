// Redesign, Etap 3 (E3-2) — ekran sklepu: zakładki kategorii (bez pustych), szukanie produktów w sklepie,
// lista na telefonie / siatka od 600 px, tekst 130 %, termin z rundy w nagłówku, „ogranicz ruch".
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:zipzap/core/api/api_client.dart';
import 'package:zipzap/core/api/api_exception.dart';
import 'package:zipzap/core/providers.dart';
import 'package:zipzap/core/theme/zz_theme.dart';
import 'package:zipzap/core/ui/ui.dart';
import 'package:zipzap/core/util/search_text.dart';
import 'package:zipzap/features/cart/ordering_repository.dart';
import 'package:zipzap/features/catalog/catalog_repository.dart';
import 'package:zipzap/features/catalog/product_add_button.dart';
import 'package:zipzap/features/catalog/store_detail_screen.dart';
import 'package:zipzap/models/product.dart';
import 'package:zipzap/models/purchasing_round.dart';
import 'package:zipzap/models/store.dart';
import 'package:zipzap/models/store_category.dart';

const _sid = '11111111-1111-1111-1111-111111111111';

Product _p(String id, String name, String? cat, {bool available = true}) => Product.fromJson({
  'id': id,
  'storeId': _sid,
  'categoryId': cat,
  'name': name,
  'price': 4.99,
  'unit': 'szt',
  'isAvailable': available,
});

final _products = [
  _p('p1', 'Chleb żytni', 'c-piek'),
  _p('p2', 'Bułka kajzerka', 'c-piek'),
  _p('p3', 'Żółty ser gouda', 'c-nab'),
  _p('p4', 'Mleko 2%', 'c-nab'),
  _p('p5', 'Jabłka', null),
];

const _categories = [
  StoreCategory(id: 'c-nab', name: 'Nabiał', sortOrder: 2),
  StoreCategory(id: 'c-piek', name: 'Pieczywo', sortOrder: 1),
  StoreCategory(id: 'c-pusta', name: 'Ryby', sortOrder: 3),
];

class _Catalog extends CatalogRepository {
  _Catalog({this.products = const [], this.categoriesFail = false}) : super(_Offline());
  final List<Product> products;
  final bool categoriesFail;
  @override
  Future<Store> getStore(String idOrSlug) async => Store.fromJson({
    'id': _sid,
    'slug': 'piekarnia',
    'name': 'Piekarnia Poranek',
    'city': 'Rabka-Zdrój',
    'status': 'Open',
    'minimumOrderValue': 15,
    'isAcceptingOrders': true,
    'description': 'Chleb na zakwasie od 1990 roku.',
  });
  @override
  Future<List<Product>> listProducts(String storeId, {String? categoryId}) async => products;
  @override
  Future<List<StoreCategory>> listCategories(String storeId) async {
    if (categoriesFail) throw ApiException(code: 'network', message: 'offline');
    return [..._categories]..sort((a, b) => a.sortOrder.compareTo(b.sortOrder));
  }
}

class _Ordering extends OrderingRepository {
  _Ordering() : super(_Offline());
  @override
  Future<RoundPreview> getRoundPreview(String storeId) async {
    final now = DateTime.now();
    final d = '${now.year}-${now.month.toString().padLeft(2, '0')}-${now.day.toString().padLeft(2, '0')}';
    return RoundPreview.fromJson({
      'available': true,
      'round': {
        'localDate': d,
        'localTime': '16:00:00',
        'cutoffLocalDate': d,
        'cutoffLocalTime': '15:30:00',
        'timeZone': 'Europe/Warsaw',
        'startsAtUtc': '${d}T15:00:00Z',
        'cutoffAtUtc': '${d}T14:30:00Z',
      },
      'earliestDelivery': {'localDate': d, 'localTime': '17:00:00'},
    });
  }
}

class _Offline extends ApiClient {
  @override
  Future<dynamic> get(String path, {Map<String, dynamic>? query}) async =>
      throw ApiException(code: 'network', message: 'offline');
}

Future<void> _pump(
  WidgetTester tester, {
  Size size = const Size(360, 1400),
  double textScale = 1,
  bool reduce = false,
  List<Product>? products,
  bool categoriesFail = false,
}) async {
  tester.view.physicalSize = size;
  tester.view.devicePixelRatio = 1;
  addTearDown(tester.view.reset);
  await tester.pumpWidget(
    ProviderScope(
      overrides: [
        apiClientProvider.overrideWithValue(_Offline()),
        catalogRepositoryProvider.overrideWithValue(
          _Catalog(products: products ?? _products, categoriesFail: categoriesFail),
        ),
        orderingRepositoryProvider.overrideWithValue(_Ordering()),
      ],
      child: MaterialApp.router(
        theme: ZzTheme.light(),
        builder: (context, child) => MediaQuery(
          data: MediaQuery.of(context).copyWith(textScaler: TextScaler.linear(textScale), disableAnimations: reduce),
          child: child!,
        ),
        routerConfig: GoRouter(
          routes: [
            GoRoute(
              path: '/',
              builder: (_, _) => const StoreDetailScreen(storeRef: 'piekarnia'),
            ),
          ],
        ),
      ),
    ),
  );
  await tester.pumpAndSettle();
}

void main() {
  group('logika (E3-2)', () {
    test('zakładki: tylko kategorie z produktami, w kolejności sklepu', () {
      expect(tabsFor(_categories, _products).map((c) => c.name), [
        'Nabiał',
        'Pieczywo',
      ], reason: 'kolejność wejściowa zachowana, pusta „Ryby" pominięta');
      final sorted = [..._categories]..sort((a, b) => a.sortOrder.compareTo(b.sortOrder));
      expect(tabsFor(sorted, _products).map((c) => c.name), ['Pieczywo', 'Nabiał'], reason: 'bez pustej „Ryby"');
    });

    test('szukanie: bez wielkości liter i polskich znaków, słowa w dowolnej kolejności', () {
      expect(filterProducts(_products, query: 'zolty').map((p) => p.id), ['p3']);
      expect(filterProducts(_products, query: 'GOUDA ser').map((p) => p.id), ['p3']);
      expect(filterProducts(_products, query: 'chleb  ').map((p) => p.id), ['p1']);
      expect(filterProducts(_products, query: '').length, 5);
      expect(filterProducts(_products, categoryId: 'c-piek', query: 'bul').map((p) => p.id), ['p2']);
      expect(filterProducts(_products, categoryId: 'c-nab', query: 'chleb'), isEmpty);
      expect(normalizeSearch('  Żółć   Ęą '), 'zolc ea');
    });
  });

  testWidgets('360 px: wiersze, zakładki bez pustych kategorii, wybór zakładki filtruje', (tester) async {
    await _pump(tester);
    expect(find.byType(ZzProductRow), findsNWidgets(5));
    expect(find.byType(ZzProductTile), findsNothing);
    expect(find.widgetWithText(ZzFilterPill, 'Ryby'), findsNothing);

    await tester.tap(find.widgetWithText(ZzFilterPill, 'Pieczywo'));
    await tester.pumpAndSettle();
    expect(find.byType(ZzProductRow), findsNWidgets(2));
    expect(find.text('Żółty ser gouda'), findsNothing);

    await tester.tap(find.widgetWithText(ZzFilterPill, 'Wszystko'));
    await tester.pumpAndSettle();
    expect(find.byType(ZzProductRow), findsNWidgets(5));
  });

  testWidgets('szukanie w sklepie i pusty wynik z powrotem do całej oferty', (tester) async {
    await _pump(tester);
    await tester.enterText(find.byType(TextField), 'ser zolty');
    await tester.pumpAndSettle();
    expect(find.byType(ZzProductRow), findsOneWidget);
    expect(find.text('Żółty ser gouda'), findsOneWidget);

    await tester.enterText(find.byType(TextField), 'kawior');
    await tester.pumpAndSettle();
    expect(find.text('Nic nie znaleźliśmy'), findsOneWidget);
    await tester.tap(find.text('Pokaż wszystkie produkty'));
    await tester.pumpAndSettle();
    expect(find.byType(ZzProductRow), findsNWidgets(5));
    expect(tester.widget<TextField>(find.byType(TextField)).controller!.text, isEmpty);
  });

  testWidgets('nagłówek: termin z rundy zamiast ogólnej notki', (tester) async {
    await _pump(tester);
    expect(find.text('Zamów do 15:30 · dostawa od dziś, 17:00'), findsOneWidget);
    expect(find.textContaining('wybierzesz przy zamówieniu'), findsNothing);
  });

  testWidgets('błąd kategorii: oferta bez zakładek, produkty widoczne', (tester) async {
    await _pump(tester, categoriesFail: true);
    expect(find.byType(ZzFilterPill), findsNothing);
    expect(find.byType(ZzProductRow), findsNWidgets(5));
  });

  for (final (w, scale) in [(840.0, 1.0), (840.0, 1.3), (640.0, 1.3), (599.0, 1.0), (360.0, 1.3)]) {
    testWidgets(
      '${w.toInt()} px, tekst ${(scale * 100).round()} %: ${w - 2 * ZzSpace.screen >= productGridFrom ? 'siatka' : 'lista'} bez przepełnień (próg: treść ≥ 600 px)',
      (tester) async {
        final errors = <String>[];
        final previous = FlutterError.onError;
        FlutterError.onError = (d) => errors.add(d.exceptionAsString());
        await _pump(tester, size: Size(w, 1600), textScale: scale);
        FlutterError.onError = previous;
        expect(errors, isEmpty);
        if (w - 2 * ZzSpace.screen >= productGridFrom) {
          expect(find.byType(ZzProductTile), findsWidgets);
          expect(find.byType(ZzProductRow), findsNothing);
        } else {
          expect(find.byType(ZzProductRow), findsWidgets);
        }
      },
    );
  }

  testWidgets('duży asortyment (2000 produktów): lista buduje tylko widoczne wiersze', (tester) async {
    final many = [for (var i = 0; i < 2000; i++) _p('x$i', 'Produkt $i', i.isEven ? 'c-piek' : 'c-nab')];
    await _pump(tester, size: const Size(360, 780), products: many);
    expect(find.byType(ZzProductRow).evaluate().length, lessThan(20));
    await tester.enterText(find.byType(TextField), 'produkt 1999');
    await tester.pumpAndSettle();
    expect(find.text('Produkt 1999'), findsOneWidget);
  });

  testWidgets('„ogranicz ruch": przejście „Dodaj" → licznik bez animacji', (tester) async {
    await _pump(tester, reduce: true);
    final sw = tester.widget<AnimatedSwitcher>(
      find.descendant(of: find.byType(ProductAddButton).first, matching: find.byType(AnimatedSwitcher)),
    );
    expect(sw.duration, Duration.zero);
  });

  testWidgets('animacja przejścia trwa ≤ 250 ms przy zwykłym ruchu', (tester) async {
    await _pump(tester);
    final sw = tester.widget<AnimatedSwitcher>(
      find.descendant(of: find.byType(ProductAddButton).first, matching: find.byType(AnimatedSwitcher)),
    );
    expect(sw.duration.inMilliseconds, inInclusiveRange(1, 250));
  });
}
