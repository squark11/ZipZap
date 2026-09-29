// S2 — wejście z kodu QR do oferty sklepu w aplikacji (web/PWA):
// bezpośredni link `/s/{slug}?src=qr`, odświeżenie strony (nowa instancja aplikacji z tym samym magazynem),
// powrót po logowaniu do oferty wybranego sklepu, stany ekranu sklepu i bezpieczne przekierowania.
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:zipzap/core/api/api_client.dart';
import 'package:zipzap/core/api/api_exception.dart';
import 'package:zipzap/core/auth/auth_repository.dart';
import 'package:zipzap/core/providers.dart';
import 'package:zipzap/core/router/app_router.dart';
import 'package:zipzap/core/router/redirects.dart';
import 'package:zipzap/core/widgets/connectivity_banner.dart';
import 'package:zipzap/core/widgets/install_hint.dart';
import 'package:zipzap/features/cart/ordering_repository.dart';
import 'package:zipzap/features/catalog/catalog_repository.dart';
import 'package:zipzap/main.dart';
import 'package:zipzap/models/cart.dart';
import 'package:zipzap/models/product.dart';
import 'package:zipzap/models/store.dart';
import 'package:zipzap/models/user.dart';

const _openId = '11111111-1111-1111-1111-111111111111';

Store _store(String id, String slug, String name, {String status = 'Open', bool accepting = true}) =>
    Store.fromJson({
      'id': id, 'slug': slug, 'name': name, 'city': 'Koło', 'status': status,
      'minimumOrderValue': 0, 'isAcceptingOrders': accepting,
    });

Product _product(String id, String storeId, String name) => Product.fromJson({
      'id': id, 'storeId': storeId, 'name': name, 'price': 6.5, 'unit': 'szt', 'isAvailable': true,
    });

/// „Serwer": sklepy, produkty i koszyki — współdzielony między „odświeżeniami" (nowymi instancjami aplikacji).
class _Backend {
  final stores = <Store>[
    _store(_openId, 'piekarnia-poranek', 'Piekarnia Poranek'),
    _store('22222222-2222-2222-2222-222222222222', 'sklep-zamkniety', 'Sklep Zamknięty',
        status: 'Closed', accepting: false),
    _store('33333333-3333-3333-3333-333333333333', 'pusty-sklep', 'Pusty Sklep'),
    _store('44444444-4444-4444-4444-444444444444', 'wolny-sklep', 'Wolny Sklep'),
  ];
  late final products = <String, List<Product>>{
    _openId: [_product('p1', _openId, 'Chleb żytni'), _product('p2', _openId, 'Bułka kajzerka')],
    '22222222-2222-2222-2222-222222222222': [_product('p3', '22222222-2222-2222-2222-222222222222', 'Mleko')],
    '44444444-4444-4444-4444-444444444444': [_product('p4', '44444444-4444-4444-4444-444444444444', 'Masło')],
  };
  final carts = <String, Cart>{};
  final entries = <String>[];
  int networkFailuresLeft = 0;
  int getStoreCalls = 0;
}

class _FakeCatalog extends CatalogRepository {
  final _Backend b;
  _FakeCatalog(this.b) : super(ApiClient());

  @override
  Future<Store> getStore(String idOrSlug) async {
    b.getStoreCalls++;
    if (idOrSlug == 'wolny-sklep' && b.networkFailuresLeft > 0) {
      b.networkFailuresLeft--;
      throw ApiException(code: 'network', message: 'Brak połączenia z serwerem. Sprawdź sieć i spróbuj ponownie.');
    }
    for (final s in b.stores) {
      if (s.id == idOrSlug || s.slug == idOrSlug) return s;
    }
    throw ApiException(code: 'not_found', message: 'Sklep nie istnieje.', statusCode: 404);
  }

  @override
  Future<List<Product>> listProducts(String storeId, {String? categoryId}) async {
    if (!RegExp(r'^[0-9a-f-]{36}$').hasMatch(storeId)) {
      throw ApiException(code: 'not_found', message: 'Nie znaleziono zasobu.', statusCode: 404); // jak API: tylko id
    }
    return b.products[storeId] ?? const [];
  }
}

class _FakeOrdering extends OrderingRepository {
  final _Backend b;
  _FakeOrdering(this.b) : super(ApiClient());

  @override
  Future<Cart> createCart(String storeId) async {
    final c = Cart(id: 'cart-${b.carts.length + 1}', storeId: storeId, cartToken: 'tok-${b.carts.length + 1}',
        status: 'Active', subtotal: 0, items: const []);
    return b.carts[c.id] = c;
  }

  @override
  Future<Cart> getCart(String cartId, String token) async {
    final c = b.carts[cartId];
    if (c == null || c.cartToken != token) throw ApiException(code: 'not_found', message: 'x', statusCode: 404);
    return c;
  }

  @override
  Future<Cart> addItem(String cartId, String token, String productId, int quantity, {String? unit}) async {
    final c = await getCart(cartId, token);
    final items = [
      ...c.items,
      CartItem(productId: productId, productName: productId, unitPrice: 6.5, unit: 'szt', quantity: quantity,
          lineTotal: 6.5 * quantity),
    ];
    return b.carts[cartId] = Cart(id: c.id, storeId: c.storeId, cartToken: c.cartToken, status: c.status,
        subtotal: items.fold(0.0, (a, i) => a + i.lineTotal), items: items);
  }
}

class _FakeAuth extends AuthRepository {
  _FakeAuth() : super(ApiClient());

  AuthResult _ok(String email) => AuthResult(
      accessToken: 'at', refreshToken: 'rt',
      user: AppUser(id: 'u1', email: email, fullName: 'Tester', roles: const ['Customer']));

  @override
  Future<AuthResult> login(String email, String password) async => _ok(email);

  @override
  Future<AuthResult> refresh(String refreshToken) async =>
      refreshToken == 'rt' ? _ok('klient@example.com') : throw ApiException(code: 'x', message: 'x', statusCode: 401);
}

/// Klient API tylko do pomiaru wejść (POST .../entries) i publicznej konfiguracji (tu: brak sieci → domyślna).
class _FakeApi extends ApiClient {
  final _Backend b;
  _FakeApi(this.b);

  @override
  Future<dynamic> post(String path, {Object? body, Map<String, dynamic>? query, Map<String, String>? headers}) async {
    b.entries.add('$path ${(body as Map?)?['source']}');
    return null;
  }

  @override
  Future<dynamic> get(String path, {Map<String, dynamic>? query}) async =>
      throw ApiException(code: 'network', message: 'offline');
}

/// Jedna „instancja aplikacji" (np. karta przeglądarki po odświeżeniu) nad wspólnym backendem i magazynem.
Future<ProviderContainer> _launch(WidgetTester tester, _Backend b, String url) async {
  tester.binding.platformDispatcher.defaultRouteNameTestValue = url;
  final c = ProviderContainer(overrides: [
    apiClientProvider.overrideWithValue(_FakeApi(b)),
    catalogRepositoryProvider.overrideWith((_) => _FakeCatalog(b)),
    orderingRepositoryProvider.overrideWith((_) => _FakeOrdering(b)),
    authRepositoryProvider.overrideWith((_) => _FakeAuth()),
    connectivityProvider.overrideWith((_) => Stream.value(true)),
  ]);
  await tester.pumpWidget(UncontrolledProviderScope(container: c, child: const ZipZapApp()));
  await _settle(tester);
  return c;
}

/// Ekrany mają animacje nieskończone (szkielety, wskaźniki) — zamiast pumpAndSettle kilka klatek.
Future<void> _settle(WidgetTester tester) async {
  for (var i = 0; i < 25; i++) {
    await tester.pump(const Duration(milliseconds: 40));
  }
}

String _location(ProviderContainer c) => c.read(routerProvider).routerDelegate.currentConfiguration.uri.toString();

Future<void> _close(WidgetTester tester, ProviderContainer c) async {
  await tester.pumpWidget(const SizedBox());
  c.dispose();
}

void main() {
  setUp(() => FlutterSecureStorage.setMockInitialValues({}));

  testWidgets('QR: bezpośrednie wejście pokazuje ofertę sklepu, zlicza źródło raz i czyści adres', (tester) async {
    addTearDown(tester.binding.platformDispatcher.clearDefaultRouteNameTestValue);
    final b = _Backend();
    final c = await _launch(tester, b, '/s/piekarnia-poranek?src=qr');

    expect(find.text('Piekarnia Poranek'), findsWidgets);
    expect(find.text('Chleb żytni'), findsOneWidget);
    expect(_location(c), '/s/piekarnia-poranek', reason: 'parametr źródła usunięty z adresu po zliczeniu');
    expect(b.entries, ['/catalog/stores/piekarnia-poranek/entries qr']);

    // Ponowne wejście z tym samym źródłem w tej samej sesji nie jest liczone drugi raz.
    c.read(routerProvider).go('/s/piekarnia-poranek?src=qr');
    await _settle(tester);
    expect(b.entries, hasLength(1));
    await _close(tester, c);
  });

  testWidgets('Odświeżenie strony: ten sam adres i koszyk są odtworzone w nowej instancji aplikacji', (tester) async {
    addTearDown(tester.binding.platformDispatcher.clearDefaultRouteNameTestValue);
    final b = _Backend();
    var c = await _launch(tester, b, '/s/piekarnia-poranek?src=qr');
    await tester.tap(find.widgetWithText(OutlinedButton, 'Dodaj').first);
    await _settle(tester);
    expect(find.text('Koszyk (1)'), findsOneWidget);
    await _close(tester, c);

    // „F5": nowa aplikacja startuje z adresu w pasku (już bez źródła), z tym samym magazynem przeglądarki.
    c = await _launch(tester, b, '/s/piekarnia-poranek');
    expect(find.text('Chleb żytni'), findsOneWidget);
    expect(find.text('Koszyk (1)'), findsOneWidget, reason: 'koszyk odtworzony z zapamiętanego odnośnika');
    expect(b.entries, hasLength(1), reason: 'odświeżenie nie jest nowym skanem');
    await _close(tester, c);

    // Odświeżenie na ekranie koszyka też go nie gubi.
    c = await _launch(tester, b, '/cart');
    expect(find.text('p1'), findsOneWidget);
    await _close(tester, c);
  });

  testWidgets('Logowanie z karty sklepu wraca do oferty tego sklepu z zachowanym koszykiem', (tester) async {
    addTearDown(tester.binding.platformDispatcher.clearDefaultRouteNameTestValue);
    final b = _Backend();
    final c = await _launch(tester, b, '/s/piekarnia-poranek?src=qr');
    await tester.tap(find.widgetWithText(OutlinedButton, 'Dodaj').first);
    await _settle(tester);

    await tester.tap(find.byTooltip('Zaloguj się'));
    await _settle(tester);
    expect(find.widgetWithText(TextField, 'E-mail'), findsOneWidget, reason: 'ekran logowania nad kartą sklepu');
    await tester.enterText(find.widgetWithText(TextField, 'E-mail'), 'klient@example.com');
    await tester.enterText(find.widgetWithText(TextField, 'Hasło'), 'Haslo123!');
    await tester.tap(find.widgetWithText(ElevatedButton, 'Zaloguj się'));
    await _settle(tester);

    expect(_location(c), '/s/piekarnia-poranek');
    expect(find.text('Chleb żytni'), findsOneWidget);
    expect(find.text('Koszyk (1)'), findsOneWidget);
    expect(find.byTooltip('Konto'), findsOneWidget);
    await _close(tester, c);
  });

  testWidgets('Logowanie bez wskazanego celu wraca do ostatnio oglądanego sklepu, nie na listę', (tester) async {
    addTearDown(tester.binding.platformDispatcher.clearDefaultRouteNameTestValue);
    final b = _Backend();
    var c = await _launch(tester, b, '/s/piekarnia-poranek?src=qr');
    await _close(tester, c);

    // Np. klient odświeżył stronę logowania albo wszedł na nią z innego miejsca.
    c = await _launch(tester, b, '/login');
    await tester.enterText(find.widgetWithText(TextField, 'E-mail'), 'klient@example.com');
    await tester.enterText(find.widgetWithText(TextField, 'Hasło'), 'Haslo123!');
    await tester.tap(find.widgetWithText(ElevatedButton, 'Zaloguj się'));
    await _settle(tester);
    expect(_location(c), '/s/piekarnia-poranek');
    await _close(tester, c);
  });

  testWidgets('Przekierowanie po logowaniu nie wyprowadza poza aplikację', (tester) async {
    addTearDown(tester.binding.platformDispatcher.clearDefaultRouteNameTestValue);
    final b = _Backend();
    final c = await _launch(tester, b, '/login?redirect=${Uri.encodeComponent('https://evil.example/x')}');
    await tester.enterText(find.widgetWithText(TextField, 'E-mail'), 'klient@example.com');
    await tester.enterText(find.widgetWithText(TextField, 'Hasło'), 'Haslo123!');
    await tester.tap(find.widgetWithText(ElevatedButton, 'Zaloguj się'));
    await _settle(tester);
    expect(_location(c), '/stores');
    await _close(tester, c);
  });

  testWidgets('Stany: sklep nie istnieje, sklep zamknięty, brak produktów, nieznana strona', (tester) async {
    addTearDown(tester.binding.platformDispatcher.clearDefaultRouteNameTestValue);
    final b = _Backend();
    var c = await _launch(tester, b, '/s/nie-ma-takiego?src=qr');
    expect(find.text('Nie znaleziono sklepu'), findsOneWidget);
    expect(find.text('Zobacz sklepy'), findsOneWidget);
    await _close(tester, c);

    c = await _launch(tester, b, '/s/sklep-zamkniety');
    expect(find.textContaining('Sklep jest teraz zamknięty.'), findsOneWidget);
    final add = tester.widget<OutlinedButton>(find.widgetWithText(OutlinedButton, 'Dodaj'));
    expect(add.onPressed, isNull, reason: 'zamknięty sklep: oferta widoczna, dodawanie wyłączone');
    await _close(tester, c);

    c = await _launch(tester, b, '/s/pusty-sklep');
    expect(find.text('Brak produktów'), findsOneWidget);
    await _close(tester, c);

    c = await _launch(tester, b, '/cos/nieznanego');
    expect(find.text('Nie ma takiej strony'), findsOneWidget);
    await _close(tester, c);
  });

  testWidgets('Błąd sieci przy wejściu z QR: komunikat i ponowienie', (tester) async {
    addTearDown(tester.binding.platformDispatcher.clearDefaultRouteNameTestValue);
    final b = _Backend()..networkFailuresLeft = 1;
    final c = await _launch(tester, b, '/s/wolny-sklep?src=qr');
    expect(find.textContaining('Brak połączenia z serwerem'), findsOneWidget);
    await tester.tap(find.text('Spróbuj ponownie'));
    await _settle(tester);
    expect(find.text('Masło'), findsOneWidget);
    await _close(tester, c);
  });

  testWidgets('Instrukcja „dodaj do ekranu głównego" dla iPhone (Safari) i Androida (Chrome)', (tester) async {
    await tester.pumpWidget(const MaterialApp(home: Scaffold(body: InstallHintButton(forceShow: true))));
    await tester.tap(find.byTooltip('Dodaj do ekranu głównego'));
    await tester.pumpAndSettle();
    expect(find.text('iPhone — Safari'), findsOneWidget);
    expect(find.text('Android — Chrome'), findsOneWidget);
    expect(find.textContaining('Nie musisz niczego instalować'), findsOneWidget);
  });

  test('safeInternalRedirect: tylko ścieżki wewnątrz aplikacji', () {
    expect(safeInternalRedirect('/s/piekarnia'), '/s/piekarnia');
    expect(safeInternalRedirect('/checkout'), '/checkout');
    expect(safeInternalRedirect('https://evil.example'), isNull);
    expect(safeInternalRedirect('//evil.example/x'), isNull);
    expect(safeInternalRedirect(r'/\evil.example'), isNull);
    expect(safeInternalRedirect('/login?redirect=/x'), isNull);
    expect(safeInternalRedirect(''), isNull);
    expect(safeInternalRedirect(null), isNull);
  });

  test('GoRouter zna trasę stałego linku /s/{slug}', () {
    final c = ProviderContainer();
    addTearDown(c.dispose);
    final router = c.read(routerProvider);
    expect(router.configuration.findMatch(Uri.parse('/s/piekarnia')).matches, isNotEmpty);
    expect(sourceParam, 'src');
    expect(router, isA<GoRouter>());
  });
}
