// Redesign, Etap 2 — fundamenty: tokeny (kontrast), czcionki w paczce, brak kolorów na sztywno, ruch,
// komponenty (pola dotyku, czytnik ekranu, powiększony tekst) i układ całej aplikacji w 360/768/1280/1920 px.
import 'dart:io';
import 'dart:math' as math;

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:zipzap/core/api/api_client.dart';
import 'package:zipzap/core/api/api_exception.dart';
import 'package:zipzap/core/providers.dart';
import 'package:zipzap/core/theme/zz_theme.dart';
import 'package:zipzap/core/ui/ui.dart';
import 'package:zipzap/core/widgets/connectivity_banner.dart';
import 'package:zipzap/core/widgets/skeleton.dart';
import 'package:zipzap/features/catalog/catalog_repository.dart';
import 'package:zipzap/features/search/search_screen.dart';
import 'package:zipzap/main.dart';
import 'package:zipzap/models/store.dart';

double _lum(Color c) {
  double ch(double v) => v <= 0.03928 ? v / 12.92 : math.pow((v + 0.055) / 1.055, 2.4).toDouble();
  return 0.2126 * ch(c.r) + 0.7152 * ch(c.g) + 0.0722 * ch(c.b);
}

double contrast(Color a, Color b) {
  final x = _lum(a), y = _lum(b);
  return (math.max(x, y) + 0.05) / (math.min(x, y) + 0.05);
}

Store _store(String id, String name, String city) => Store.fromJson({
      'id': id, 'slug': 's-$id', 'name': name, 'city': city, 'status': 'Open',
      'minimumOrderValue': 15, 'isAcceptingOrders': true,
    });

final _stores = [
  _store('1', 'Rapacz Market — Rabka', 'Rabka-Zdrój'),
  _store('2', 'Piekarnia Poranek', 'Spytkowice'),
  _store('3', 'Sklep Zielony', 'Klikuszowa'),
];

class _FakeCatalog extends CatalogRepository {
  _FakeCatalog() : super(ApiClient());
  @override
  Future<List<Store>> listStores({bool onlyActive = true, double? lat, double? lng, String? postalCode}) async =>
      _stores;
}

class _OfflineApi extends ApiClient {
  @override
  Future<dynamic> get(String path, {Map<String, dynamic>? query}) async =>
      throw ApiException(code: 'network', message: 'offline');
  @override
  Future<dynamic> post(String path, {Object? body, Map<String, dynamic>? query, Map<String, String>? headers}) async =>
      null;
}

Widget _host(Widget child, {double textScale = 1, bool reduceMotion = false, ThemeData? theme}) => MaterialApp(
      theme: theme ?? ZzTheme.light(),
      home: Builder(
        builder: (context) => MediaQuery(
          data: MediaQuery.of(context).copyWith(
            textScaler: TextScaler.linear(textScale),
            disableAnimations: reduceMotion,
          ),
          child: Scaffold(body: Center(child: child)),
        ),
      ),
    );

void main() {
  group('Tokeny (E2-1)', () {
    for (final (name, p) in [('jasny', ZzPalette.light), ('ciemny', ZzPalette.dark)]) {
      test('tryb $name: pary tekst/tło spełniają WCAG AA 4,5:1', () {
        final pairs = <String, (Color, Color)>{
          'tekst/tło': (p.text, p.surface),
          'tekst/karta': (p.text, p.card),
          'pomocniczy/karta': (p.textMuted, p.card),
          'pomocniczy/tło': (p.textMuted, p.surface),
          'przycisk główny': (p.onBrand, p.brandStrong),
          'link na karcie': (p.brandStrong, p.card),
          'chip marki': (p.brandStrong, p.brandTint),
          'sukces': (p.success, p.successTint),
          'błąd': (p.danger, p.dangerTint),
          'uwaga': (p.warning, p.warningTint),
          'informacja': (p.info, p.infoTint),
          'akcent': (ZzColors.onAccent, ZzColors.accent),
          // Treść na pełnych wypełnieniach (plakietki, pasek „brak internetu", węzeł statusu) używa onBrand.
          'onBrand/sukces': (p.onBrand, p.success),
          'onBrand/błąd': (p.onBrand, p.danger),
          'pasek koszyka': (Colors.white, ZzColors.graphite),
        };
        for (final e in pairs.entries) {
          expect(contrast(e.value.$1, e.value.$2), greaterThanOrEqualTo(4.5), reason: '${e.key} ($name)');
        }
      });
    }

    test('turkus marki nie nadaje się pod biały tekst (dlatego przyciski mają brandStrong)', () {
      expect(contrast(Colors.white, ZzColors.brand), lessThan(4.5));
      expect(ZzTheme.light().colorScheme.primary, ZzPalette.light.brandStrong);
    });

    test('poza motywem nie ma kolorów wpisanych na sztywno (Color(0x…))', () {
      final offenders = Directory('lib')
          .listSync(recursive: true)
          .whereType<File>()
          .where((f) => f.path.endsWith('.dart') && !f.path.replaceAll('\\', '/').contains('core/theme/'))
          .where((f) => f.readAsStringSync().contains('Color(0x'))
          .map((f) => f.path)
          .toList();
      expect(offenders, isEmpty);
    });
  });

  group('Biel na sztywno (E2-1)', () {
    test('Colors.white tylko tam, gdzie tło jest stałe w obu motywach', () {
      // Biały tekst na brandStrong w trybie ciemnym (#3FD3D4) miałby kontrast ok. 1,8:1 — ma być onBrand.
      const allowed = ['core/theme/', 'core/ui/zz_cart_bar.dart', 'core/widgets/skeleton.dart', 'core/widgets/zz_logo.dart'];
      final offenders = Directory('lib')
          .listSync(recursive: true)
          .whereType<File>()
          .where((f) => f.path.endsWith('.dart'))
          .where((f) => !allowed.any((a) => f.path.replaceAll('\\', '/').contains(a)))
          .where((f) => f.readAsStringSync().contains('Colors.white'))
          .map((f) => f.path)
          .toList();
      expect(offenders, isEmpty);
    });
  });

  group('Czcionki w paczce (E2-2)', () {
    test('bez google_fonts; Inter i Poppins zadeklarowane i obecne', () {
      final pubspec = File('pubspec.yaml').readAsStringSync();
      expect(pubspec, isNot(contains('google_fonts')));
      for (final family in ['Inter', 'Poppins']) {
        expect(pubspec, contains('- family: $family'));
        for (final w in ['Regular', 'Medium', 'SemiBold', 'Bold']) {
          final f = File('assets/fonts/$family-$w.ttf');
          expect(f.existsSync(), isTrue, reason: f.path);
          expect(f.lengthSync(), lessThan(80 * 1024), reason: 'odchudzony plik ${f.path}');
        }
      }
      expect(File('assets/fonts/OFL-Inter.txt').existsSync(), isTrue);
      expect(File('assets/fonts/OFL-Poppins.txt').existsSync(), isTrue);
    });

    test('start bez serwerów Google: rodzina zapasowa Roboto w paczce, CanvasKit lokalnie', () {
      // Bez rodziny "Roboto" silnik web pobiera ją z fonts.gstatic.com; bez flagi CanvasKit idzie z www.gstatic.com.
      expect(File('pubspec.yaml').readAsStringSync(), contains('- family: Roboto'));
      expect(File('hosting/build-app.ps1').readAsStringSync(), contains('--no-web-resources-cdn'));
    });

    test('motyw używa czcionek z paczki', () {
      final t = ZzTheme.light().textTheme;
      expect(t.bodyMedium?.fontFamily, ZzFonts.body);
      expect(t.titleLarge?.fontFamily, ZzFonts.heading);
    });
  });

  group('Ruch (E2-5)', () {
    testWidgets('„ogranicz ruch" zeruje czasy animacji', (tester) async {
      late Duration normal, reduced;
      await tester.pumpWidget(_host(Builder(builder: (c) {
        normal = ZzMotion.of(c, ZzMotion.base);
        return const SizedBox();
      })));
      await tester.pumpWidget(_host(Builder(builder: (c) {
        reduced = ZzMotion.of(c, ZzMotion.base);
        return const SizedBox();
      }), reduceMotion: true));
      expect(normal, ZzMotion.base);
      expect(reduced, Duration.zero);
      expect(ZzMotion.slow.inMilliseconds, lessThanOrEqualTo(250));
    });

    testWidgets('szkielety: jeden zegar na listę, bez zegara przy „ogranicz ruch"', (tester) async {
      await tester.pumpWidget(_host(const SizedBox(width: 360, height: 700, child: StoreListSkeleton())));
      expect(find.byType(ZzShimmer), findsOneWidget, reason: 'jeden wspólny efekt nad całą listą');
      expect(find.byType(ZzSkeleton), findsWidgets);
      final state = tester.state(find.byType(ZzShimmer)) as dynamic;
      expect(state.isTicking, isTrue);
      expect(tester.binding.transientCallbackCount, greaterThan(0));

      await tester.pumpWidget(_host(const SizedBox(width: 360, height: 700, child: StoreListSkeleton()), reduceMotion: true));
      await tester.pump();
      final reduced = tester.state(find.byType(ZzShimmer)) as dynamic;
      expect(reduced.isTicking, isFalse);
      expect(find.descendant(of: find.byType(ZzShimmer), matching: find.byType(FadeTransition)), findsNothing);
      await tester.pump(const Duration(seconds: 1));
      expect(tester.binding.transientCallbackCount, 0, reason: 'żaden zegar animacji nie pracuje w tle');
    });
  });

  group('Komponenty (E2-3)', () {
    testWidgets('licznik: pola dotyku ≥ 48 px, etykiety dla czytnika, działa przy tekście 130 %', (tester) async {
      var qty = 2;
      await tester.pumpWidget(_host(
        StatefulBuilder(
          builder: (_, set) => ZzQtyStepper(
            itemLabel: 'Jabłka',
            quantity: qty,
            onMinus: () => set(() => qty--),
            onPlus: () => set(() => qty++),
          ),
        ),
        textScale: 1.3,
      ));
      final semantics = tester.ensureSemantics();
      final plus = find.bySemanticsLabel('Więcej: Jabłka');
      expect(plus, findsOneWidget);
      expect(find.bySemanticsLabel('Mniej: Jabłka'), findsOneWidget);
      final size = tester.getSize(find.byType(InkResponse).last);
      expect(size.width, greaterThanOrEqualTo(48));
      expect(size.height, greaterThanOrEqualTo(48));
      await tester.tap(find.byType(InkResponse).last);
      await tester.pump(const Duration(milliseconds: 300));
      expect(qty, 3);
      expect(tester.takeException(), isNull);
      semantics.dispose();
    });

    testWidgets('licznik w koszyku: dotknięcie liczby (wpisanie ilości) ma pole ≥ 48 px', (tester) async {
      var tapped = 0;
      await tester.pumpWidget(_host(ZzQtyStepper(
        itemLabel: 'Mleko',
        quantity: 1,
        onMinus: () {},
        onPlus: () {},
        onValueTap: () => tapped++,
      )));
      final value = find.ancestor(of: find.text('1'), matching: find.byType(InkWell));
      expect(tester.getSize(value).width, greaterThanOrEqualTo(48));
      expect(tester.getSize(value).height, greaterThanOrEqualTo(48));
      await tester.tap(find.text('1'));
      expect(tapped, 1);
    });

    testWidgets('karta sklepu, chip kategorii, komunikat i pasek koszyka: tekst 130 % w 320 px bez przepełnień',
        (tester) async {
      tester.view.physicalSize = const Size(320, 900);
      tester.view.devicePixelRatio = 1;
      addTearDown(tester.view.reset);
      await tester.pumpWidget(_host(
        SingleChildScrollView(
          child: Column(children: [
            ZzStoreCard(store: _stores.first, onTap: () {}),
            const ZzCategoryChip(label: 'Pieczywo', icon: 'pieczywo', selected: true),
            const ZzNotice(kind: ZzNoticeKind.warning, title: 'Pilotaż', message: 'Zamówienie testowe — bez płatności.'),
            ZzCartBar(count: 3, subtotal: 22.46, missingToMinimum: 5.02, onTap: () {}),
          ]),
        ),
        textScale: 1.3,
      ));
      await tester.pump(const Duration(milliseconds: 300));
      expect(tester.takeException(), isNull);
      expect(find.textContaining('Brakuje 5,02'), findsOneWidget);
      expect(find.textContaining('3 produkty · 22,46'), findsOneWidget);
    });

    test('odmiana „produkt"', () {
      expect(ZzCartBar.productsLabel(1), '1 produkt');
      expect(ZzCartBar.productsLabel(3), '3 produkty');
      expect(ZzCartBar.productsLabel(5), '5 produktów');
      expect(ZzCartBar.productsLabel(12), '12 produktów');
      expect(ZzCartBar.productsLabel(22), '22 produkty');
    });

    testWidgets('tryb ciemny: komponenty renderują się bez błędów', (tester) async {
      await tester.pumpWidget(_host(
        Column(children: [
          ZzStoreCard(store: _stores.first, onTap: () {}),
          const ZzNotice(kind: ZzNoticeKind.danger, message: 'Błąd'),
        ]),
        theme: ZzTheme.dark(),
      ));
      expect(tester.takeException(), isNull);
    });
  });

  group('Szukaj (E2-6)', () {
    test('szuka po nazwie i miejscowości bez wielkości liter i polskich znaków', () {
      expect(searchStores(_stores, 'rabka').map((s) => s.id), ['1']);
      expect(searchStores(_stores, 'KLIKUSZOWA').map((s) => s.id), ['3']);
      expect(searchStores(_stores, 'piekarnia').map((s) => s.id), ['2']);
      expect(searchStores(_stores, 'zdroj').map((s) => s.id), ['1']);
      expect(searchStores(_stores, '').length, 3);
    });
  });

  group('Układ całej aplikacji (E2-4, E2-6)', () {
    setUp(() => FlutterSecureStorage.setMockInitialValues({}));

    for (final (width, sideNav) in [(360.0, false), (768.0, false), (1280.0, true), (1920.0, true)]) {
      testWidgets('${width.toInt()} px: nawigacja ${sideNav ? 'boczna' : 'dolna'}, 5 zakładek, karty nierozciągnięte',
          (tester) async {
        tester.view.physicalSize = Size(width, 900);
        tester.view.devicePixelRatio = 1;
        addTearDown(tester.view.reset);
        tester.binding.platformDispatcher.defaultRouteNameTestValue = '/stores';
        final c = ProviderContainer(overrides: [
          apiClientProvider.overrideWithValue(_OfflineApi()),
          catalogRepositoryProvider.overrideWith((_) => _FakeCatalog()),
          connectivityProvider.overrideWith((_) => Stream.value(true)),
        ]);
        // Zbieramy pełne raporty błędów układu — przy porażce wskazują widżet, który się przelewa.
        final errors = <String>[];
        final previous = FlutterError.onError;
        FlutterError.onError = (d) => errors.add(
            '${d.exceptionAsString()}\n${d.informationCollector?.call().map((e) => e.toString()).join('\n') ?? ''}');
        await tester.pumpWidget(UncontrolledProviderScope(container: c, child: const ZipZapApp()));
        for (var i = 0; i < 25; i++) {
          await tester.pump(const Duration(milliseconds: 40));
        }
        FlutterError.onError = previous;
        expect(errors, isEmpty, reason: 'brak przepełnień i błędów układu:\n${errors.join('\n---\n')}');
        expect(find.byType(NavigationRail), sideNav ? findsOneWidget : findsNothing);
        expect(find.byType(NavigationBar), sideNav ? findsNothing : findsOneWidget);
        for (final label in ['Start', 'Szukaj', 'Koszyk', 'Zamówienia', 'Konto']) {
          expect(find.text(label), findsWidgets, reason: label);
        }
        final cards = find.byType(ZzStoreCard);
        expect(cards, findsNWidgets(3));
        expect(tester.getSize(cards.first).width, lessThanOrEqualTo(math.min(width, 560)),
            reason: 'karta sklepu nie rozciąga się na cały ekran');
        // Brak poziomego przewijania: treść mieści się w szerokości okna.
        final content = tester.getRect(find.byType(ZzPage).first);
        expect(content.right, lessThanOrEqualTo(width + 0.5));
        await tester.pumpWidget(const SizedBox());
        c.dispose();
      });
    }

    // E2-5: całe ekrany (nie tylko komponenty) przy tekście powiększonym do 130 % na wąskim telefonie.
    for (final route in ['/stores', '/search', '/cart', '/orders', '/account', '/login', '/forgot-password', '/stores/1']) {
      testWidgets('tekst 130 % przy 360 px: $route bez przepełnień', (tester) async {
        tester.view.physicalSize = const Size(360, 780);
        tester.view.devicePixelRatio = 1;
        tester.platformDispatcher.textScaleFactorTestValue = 1.3;
        addTearDown(tester.view.reset);
        addTearDown(tester.platformDispatcher.clearTextScaleFactorTestValue);
        tester.binding.platformDispatcher.defaultRouteNameTestValue = route;
        final c = ProviderContainer(overrides: [
          apiClientProvider.overrideWithValue(_OfflineApi()),
          catalogRepositoryProvider.overrideWith((_) => _FakeCatalog()),
          connectivityProvider.overrideWith((_) => Stream.value(true)),
        ]);
        final errors = <String>[];
        final previous = FlutterError.onError;
        FlutterError.onError = (d) => errors.add(
            '${d.exceptionAsString()}\n${d.informationCollector?.call().map((e) => e.toString()).join('\n') ?? ''}');
        await tester.pumpWidget(UncontrolledProviderScope(container: c, child: const ZipZapApp()));
        for (var i = 0; i < 25; i++) {
          await tester.pump(const Duration(milliseconds: 40));
        }
        FlutterError.onError = previous;
        expect(MediaQuery.textScalerOf(tester.element(find.byType(Scaffold).last)).scale(10), closeTo(13, 0.01));
        expect(errors, isEmpty, reason: '$route przy 130 %:\n${errors.join('\n---\n')}');
        await tester.pumpWidget(const SizedBox());
        c.dispose();
      });
    }

    testWidgets('bezpośrednie wejście na /search: zakładka Szukaj, wpisanie miejscowości filtruje sklepy', (tester) async {
      tester.view.physicalSize = const Size(375, 800);
      tester.view.devicePixelRatio = 1;
      addTearDown(tester.view.reset);
      tester.binding.platformDispatcher.defaultRouteNameTestValue = '/search';
      final c = ProviderContainer(overrides: [
        apiClientProvider.overrideWithValue(_OfflineApi()),
        catalogRepositoryProvider.overrideWith((_) => _FakeCatalog()),
        connectivityProvider.overrideWith((_) => Stream.value(true)),
      ]);
      await tester.pumpWidget(UncontrolledProviderScope(container: c, child: const ZipZapApp()));
      for (var i = 0; i < 25; i++) {
        await tester.pump(const Duration(milliseconds: 40));
      }
      expect(find.byType(SearchScreen), findsOneWidget);
      expect(find.byType(NavigationBar), findsOneWidget);
      expect(find.byType(ZzStoreCard), findsNWidgets(3));
      await tester.enterText(find.byType(TextField), 'rabka');
      await tester.pump(const Duration(milliseconds: 100));
      expect(find.byType(ZzStoreCard), findsOneWidget);
      expect(find.text('Rapacz Market — Rabka'), findsOneWidget);
      await tester.pumpWidget(const SizedBox());
      c.dispose();
    });
  });

}
