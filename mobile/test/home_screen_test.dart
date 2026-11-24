// Redesign, Etap 3 (E3-1) — ekran Start: nagłówek „Dostawa do" zamiast dwóch banerów (logika kodu i lokalizacji
// bez zmian), wybór zapisanego adresu w 2 dotknięciach, wpisanie kodu, linia terminu z rundy, sklepy widoczne
// bez przewijania przy wysokości 640 px.
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:zipzap/core/api/api_client.dart';
import 'package:zipzap/core/api/api_exception.dart';
import 'package:zipzap/core/auth/auth_controller.dart';
import 'package:zipzap/core/auth/auth_state.dart';
import 'package:zipzap/core/providers.dart';
import 'package:zipzap/core/theme/zz_theme.dart';
import 'package:zipzap/core/ui/ui.dart';
import 'package:zipzap/features/account/addresses_screen.dart';
import 'package:zipzap/features/cart/ordering_repository.dart';
import 'package:zipzap/features/catalog/catalog_repository.dart';
import 'package:zipzap/features/home/home_providers.dart';
import 'package:zipzap/features/home/home_screen.dart';
import 'package:zipzap/core/widgets/notifications_bell.dart';
import 'package:zipzap/models/address.dart';
import 'package:zipzap/models/purchasing_round.dart';
import 'package:zipzap/models/store.dart';
import 'package:zipzap/models/user.dart';

Store _store(String id, String name) => Store.fromJson({
      'id': id, 'slug': 's-$id', 'name': name, 'city': 'Rabka-Zdrój', 'status': 'Open',
      'minimumOrderValue': 15, 'isAcceptingOrders': true,
    });

class _Catalog extends CatalogRepository {
  _Catalog({this.mode = 'ok'}) : super(_OfflineApi());
  final String mode; // ok | empty | error
  final postals = <String?>[];
  @override
  Future<List<Store>> listStores({bool onlyActive = true, double? lat, double? lng, String? postalCode}) async {
    postals.add(postalCode);
    if (mode == 'error') throw ApiException(code: 'network', message: 'Brak połączenia z serwerem.');
    if (mode == 'empty') return [];
    return [_store('1', 'Rapacz Market — Rabka'), _store('2', 'Piekarnia Poranek')];
  }
}

String _iso(DateTime d) => '${d.year}-${d.month.toString().padLeft(2, '0')}-${d.day.toString().padLeft(2, '0')}';

class _Ordering extends OrderingRepository {
  _Ordering({this.available = true}) : super(_OfflineApi());
  final bool available;
  final asked = <String>[];
  @override
  Future<RoundPreview> getRoundPreview(String storeId) async {
    asked.add(storeId);
    if (!available) return RoundPreview(available: false, reason: 'no_round');
    final today = _iso(DateTime.now());
    final cutoff = storeId == '2' ? '10:30:00' : '11:30:00';
    return RoundPreview.fromJson({
      'available': true,
      'round': {
        'localDate': today, 'localTime': '12:00:00', 'cutoffLocalDate': today, 'cutoffLocalTime': cutoff,
        'timeZone': 'Europe/Warsaw',
        'startsAtUtc': '${today}T11:00:00Z', 'cutoffAtUtc': '${today}T${storeId == '2' ? '09' : '10'}:30:00Z',
      },
      'earliestDelivery': {'localDate': today, 'localTime': '17:00:00'},
    });
  }
}

class _OfflineApi extends ApiClient {
  @override
  Future<dynamic> get(String path, {Map<String, dynamic>? query}) async =>
      throw ApiException(code: 'network', message: 'offline');
}

class _SignedIn extends AuthController {
  @override
  AuthState build() => AuthState(
      AuthStatus.authenticated, AppUser(id: 'u1', email: 'anna@example.test', fullName: 'Anna Nowak', roles: const []));
}

Address _addr(String id, String label, String postal, {bool def = false}) => Address(
    id: id, label: label, street: 'Krótka', buildingNo: '1', postalCode: postal, city: 'Kraków', isDefault: def);

Future<(_Catalog, _Ordering)> _pump(WidgetTester tester,
    {bool signedIn = false, bool rounds = true, Size size = const Size(360, 780), String catalogMode = 'ok'}) async {
  tester.view.physicalSize = size;
  tester.view.devicePixelRatio = 1;
  addTearDown(tester.view.reset);
  final catalog = _Catalog(mode: catalogMode);
  final ordering = _Ordering(available: rounds);
  await tester.pumpWidget(ProviderScope(
    overrides: [
      apiClientProvider.overrideWithValue(_OfflineApi()),
      catalogRepositoryProvider.overrideWithValue(catalog),
      orderingRepositoryProvider.overrideWithValue(ordering),
      if (signedIn) ...[
        authControllerProvider.overrideWith(_SignedIn.new),
        addressesProvider.overrideWith((_) async =>
            [_addr('a1', 'Praca', '31-042'), _addr('a2', 'Dom', '34700', def: true)]),
        unreadCountProvider.overrideWith((_) async => 0),
      ],
    ],
    child: MaterialApp(theme: ZzTheme.light(), home: const HomeScreen()),
  ));
  await tester.pumpAndSettle();
  return (catalog, ordering);
}

void main() {
  testWidgets('bez logowania: nagłówek zachęca do kodu, wpisany kod filtruje sklepy', (tester) async {
    final (catalog, _) = await _pump(tester);
    expect(find.text('Wybierz kod pocztowy'), findsOneWidget);
    expect(find.text('Co dziś kupujemy?'), findsOneWidget);
    expect(catalog.postals.last, isNull);

    await tester.tap(find.byType(ZzPlaceHeader));
    await tester.pumpAndSettle();
    await tester.enterText(find.byType(TextField).last, '31042');
    await tester.tap(find.text('Pokaż'));
    await tester.pumpAndSettle();

    expect(catalog.postals.last, '31-042');
    expect(find.text('31-042'), findsOneWidget);
    expect(find.text('Sklepy, które tu dowożą'), findsOneWidget);
  });

  testWidgets('błędny kod: komunikat w arkuszu, filtr bez zmian', (tester) async {
    final (catalog, _) = await _pump(tester);
    await tester.tap(find.byType(ZzPlaceHeader));
    await tester.pumpAndSettle();
    await tester.enterText(find.byType(TextField).last, '310');
    await tester.tap(find.text('Pokaż'));
    await tester.pumpAndSettle();
    expect(find.text('Kod pocztowy ma 5 cyfr, np. 31-042.'), findsOneWidget);
    expect(catalog.postals.last, isNull);
  });

  testWidgets('zalogowany: kod z domyślnego adresu, inny zapisany adres w 2 dotknięciach', (tester) async {
    final (catalog, _) = await _pump(tester, signedIn: true);
    expect(find.text('Co dziś kupujemy, Anna?'), findsOneWidget);
    expect(find.text('Dom · 34-700'), findsOneWidget, reason: 'auto-uzupełnienie z domyślnego adresu');
    expect(catalog.postals.last, '34-700');

    await tester.tap(find.byType(ZzPlaceHeader)); // 1
    await tester.pumpAndSettle();
    await tester.tap(find.text('Praca')); // 2
    await tester.pumpAndSettle();

    expect(find.text('Praca · 31-042'), findsOneWidget);
    expect(catalog.postals.last, '31-042');
  });

  testWidgets('„Pokaż wszystkie sklepy" czyści filtr', (tester) async {
    final (catalog, _) = await _pump(tester, signedIn: true);
    await tester.tap(find.byType(ZzPlaceHeader));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Pokaż wszystkie sklepy'));
    await tester.pumpAndSettle();
    expect(find.text('Wybierz kod pocztowy'), findsOneWidget);
    expect(catalog.postals.last, isNull);
  });

  testWidgets('runda z API: linia terminu na kartach, baner wskazuje najbliższą; jedno zapytanie na sklep',
      (tester) async {
    final (_, ordering) = await _pump(tester);
    expect(find.text('Zamów do 11:30 · dostawa od dziś, 17:00'), findsOneWidget);
    expect(find.text('Zamów do 10:30 · dostawa od dziś, 17:00'), findsOneWidget);
    expect(find.text('Najbliższa runda · Piekarnia Poranek'), findsOneWidget);
    expect(ordering.asked..sort(), ['1', '2'], reason: 'karta i baner czytają ten sam wpis');
  });

  testWidgets('brak rundy: bez banera i bez linii terminu', (tester) async {
    await _pump(tester, rounds: false);
    expect(find.textContaining('Najbliższa runda'), findsNothing);
    expect(find.textContaining('Zamów do'), findsNothing);
    expect(find.byType(ZzStoreCard), findsNWidgets(2));
  });

  testWidgets('360 × 640: sklepy widoczne bez przewijania (nad dolną nawigacją)', (tester) async {
    await _pump(tester, size: const Size(360, 640));
    // Ekran testowy nie ma dolnej nawigacji aplikacji (ok. 80 px) — liczymy ją ręcznie. Czcionka testowa jest
    // szersza od Inter, więc w przeglądarce karta zaczyna się wyżej (zrzut w raporcie).
    const nav = 80.0;
    final top = tester.getTopLeft(find.byType(ZzStoreCard).first).dy;
    expect(top + zzMinTouch, lessThanOrEqualTo(640 - nav), reason: 'karta zaczyna się na $top px');
  });

  testWidgets('nagłówek: pole dotyku ≥ 48 px i etykieta dla czytnika', (tester) async {
    final handle = tester.ensureSemantics();
    await _pump(tester);
    expect(tester.getSize(find.byType(ZzPlaceHeader)).height, greaterThanOrEqualTo(zzMinTouch));
    expect(find.bySemanticsLabel(RegExp('^Dostawa do: Wybierz kod pocztowy')), findsOneWidget);
    handle.dispose();
  });

  test('termin graniczny jutro: „Zamów jutro do", nie samo „do 11:30"', () {
    final t = _iso(DateTime.now().add(const Duration(days: 1)));
    final p = RoundPreview.fromJson({
      'available': true,
      'round': {
        'localDate': t, 'localTime': '12:00:00', 'cutoffLocalDate': t, 'cutoffLocalTime': '11:30:00',
        'timeZone': 'Europe/Warsaw', 'startsAtUtc': '${t}T11:00:00Z', 'cutoffAtUtc': '${t}T10:30:00Z',
      },
      'earliestDelivery': {'localDate': t, 'localTime': '13:00:00'},
    });
    expect(roundLine(p), 'Zamów jutro do 11:30 · dostawa od jutro, 13:00');
  });

  testWidgets('pusty: ilustracja i treść zależna od filtra', (tester) async {
    await _pump(tester, catalogMode: 'empty');
    expect(find.text('Brak sklepów'), findsOneWidget);
    await tester.tap(find.byType(ZzPlaceHeader));
    await tester.pumpAndSettle();
    await tester.enterText(find.byType(TextField).last, '31-042');
    await tester.tap(find.text('Pokaż'));
    await tester.pumpAndSettle();
    expect(find.text('Brak sklepów dowożących'), findsOneWidget);
  });

  testWidgets('błąd: komunikat i ponowienie wysyła nowe zapytanie', (tester) async {
    final (catalog, _) = await _pump(tester, catalogMode: 'error');
    expect(find.textContaining('Brak połączenia z serwerem'), findsOneWidget);
    final before = catalog.postals.length;
    await tester.tap(find.text('Spróbuj ponownie'));
    await tester.pumpAndSettle();
    expect(catalog.postals.length, before + 1);
  });
}
