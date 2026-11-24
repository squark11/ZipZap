import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/api/api_exception.dart';
import '../../core/navigation/last_store.dart';
import '../../core/providers.dart';
import '../../core/theme/zz_theme.dart';
import '../../core/ui/ui.dart';
import '../../core/util/search_text.dart';
import '../../core/widgets/account_button.dart';
import '../../core/widgets/cart_button.dart';
import '../../core/widgets/install_hint.dart';
import '../../core/widgets/skeleton.dart';
import '../../core/widgets/states.dart';
import '../../models/product.dart';
import '../../models/store.dart';
import '../../models/store_category.dart';
import '../cart/cart_controller.dart';
import '../home/home_providers.dart';
import 'product_add_button.dart';
import 'store_header.dart';

/// Sklep po slugu (stały link z kodu QR) albo po id (z listy).
final storeDetailProvider = FutureProvider.autoDispose.family<Store, String>(
    (ref, idOrSlug) => ref.read(catalogRepositoryProvider).getStore(idOrSlug));

/// Produkty sklepu — zawsze po id (API przyjmuje tu tylko identyfikator, nie slug).
final productsProvider = FutureProvider.autoDispose.family<List<Product>, String>(
    (ref, storeId) => ref.read(catalogRepositoryProvider).listProducts(storeId));

/// Kategorie sklepu (zakładki). Błąd = brak zakładek — oferta działa dalej bez nich.
final storeCategoriesProvider = FutureProvider.autoDispose.family<List<StoreCategory>, String>((ref, storeId) async {
  try {
    return await ref.read(catalogRepositoryProvider).listCategories(storeId);
  } catch (_) {
    return const [];
  }
});

/// Zakładki do pokazania: tylko kategorie, w których są produkty (bez pustych zakładek), w kolejności sklepu.
List<StoreCategory> tabsFor(List<StoreCategory> categories, List<Product> products) {
  final used = {for (final p in products) p.categoryId};
  return [for (final c in categories) if (used.contains(c.id)) c];
}

/// Produkty po wybranej kategorii (null = wszystkie) i zapytaniu (nazwa, bez wielkości liter i polskich znaków,
/// słowa w dowolnej kolejności). Kolejność z serwera bez zmian.
List<Product> filterProducts(List<Product> products, {String? categoryId, String query = ''}) => [
      for (final p in products)
        if ((categoryId == null || p.categoryId == categoryId) && matchesSearch(p.name, query)) p,
    ];

/// Od tej szerokości treści produkty idą w siatkę kafelków zamiast listy wierszy.
const productGridFrom = 600.0;

/// Oferta sklepu (E3-2). Wejście z kodu QR (`/s/{slug}`) i z listy sklepów (`/stores/{id}`) prowadzi tutaj.
/// Stany: ładowanie, sklep nie istnieje, błąd sieci (ponów), sklep nie przyjmuje zamówień, brak produktów,
/// brak wyników wyszukiwania.
class StoreDetailScreen extends ConsumerWidget {
  /// Slug (z kodu QR) albo id sklepu.
  final String storeRef;
  const StoreDetailScreen({super.key, required this.storeRef});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final store = ref.watch(storeDetailProvider(storeRef));
    return store.when(
      loading: () => const _Frame(title: 'Sklep', body: ProductListSkeleton()),
      error: (e, _) => _Frame(
        title: 'Sklep',
        body: e is ApiException && e.isNotFound
            ? EmptyView(
                icon: Icons.storefront_outlined,
                svgAsset: 'assets/svg/illustrations/state_no_stores.svg',
                title: 'Nie znaleziono sklepu',
                subtitle: 'Kod QR lub link może być nieaktualny. Zobacz sklepy dostępne w Dowózka.pl.',
                action: ElevatedButton(onPressed: () => context.go('/stores'), child: const Text('Zobacz sklepy')),
              )
            : ErrorView(
                message: e.toString(),
                onRetry: () => ref.invalidate(storeDetailProvider(storeRef)),
              ),
      ),
      data: (s) => _StoreOffer(store: s, storeRef: storeRef),
    );
  }
}

/// Rama ekranu (nie karta): powrót (albo „Wszystkie sklepy", gdy klient wszedł bezpośrednio z kodu QR),
/// skrót na ekran główny (web), konto/logowanie, koszyk.
class _Frame extends StatelessWidget {
  final String title;
  final Widget body;
  final Widget? bottom;
  const _Frame({required this.title, required this.body, this.bottom});

  @override
  Widget build(BuildContext context) => Scaffold(
        appBar: AppBar(
          leading: context.canPop()
              ? null
              : IconButton(
                  tooltip: 'Wszystkie sklepy',
                  icon: const Icon(Icons.storefront_outlined),
                  onPressed: () => context.go('/stores'),
                ),
          title: Text(title, overflow: TextOverflow.ellipsis),
          actions: const [InstallHintButton(), AccountButton(), CartButton()],
        ),
        body: body,
        bottomNavigationBar: bottom,
      );
}

/// Stan ekranu oferty: wyszukiwanie i wybrana zakładka (nie karta).
class _StoreOffer extends ConsumerStatefulWidget {
  final Store store;
  final String storeRef;
  const _StoreOffer({required this.store, required this.storeRef});

  @override
  ConsumerState<_StoreOffer> createState() => _StoreOfferState();
}

class _StoreOfferState extends ConsumerState<_StoreOffer> {
  final _search = TextEditingController();
  String? _category;

  @override
  void initState() {
    super.initState();
    _remember();
  }

  @override
  void didUpdateWidget(covariant _StoreOffer old) {
    super.didUpdateWidget(old);
    if (old.store.id != widget.store.id) _remember();
  }

  @override
  void dispose() {
    _search.dispose();
    super.dispose();
  }

  // Po zalogowaniu (i po odświeżeniu strony) klient wraca do oferty tego sklepu.
  void _remember() => Future.microtask(() => ref.read(lastStoreProvider.notifier).remember(widget.store));

  void _clearFilters() => setState(() {
        _search.clear();
        _category = null;
      });

  // Koszyk NIE jest tworzony przy wejściu do sklepu — dopiero przy pierwszym dodaniu produktu
  // (ProductAddButton). Wejście na stronę innego sklepu nie kasuje po cichu koszyka z poprzedniego.
  @override
  Widget build(BuildContext context) {
    final s = widget.store;
    final products = ref.watch(productsProvider(s.id));
    final categories = ref.watch(storeCategoriesProvider(s.id)).valueOrNull ?? const [];
    final round = ref.watch(storeRoundProvider(s.id)).valueOrNull;
    final cart = ref.watch(cartControllerProvider);

    Future<void> refresh() async {
      ref.invalidate(productsProvider(s.id));
      ref.invalidate(storeCategoriesProvider(s.id));
      ref.invalidate(storeRoundProvider(s.id));
      ref.invalidate(storeDetailProvider(widget.storeRef));
    }

    return _Frame(
      title: s.name,
      body: products.when(
        loading: () => const ProductListSkeleton(),
        error: (e, _) => ErrorView(
          message: e.toString(),
          onRetry: () => ref.invalidate(productsProvider(s.id)),
        ),
        data: (all) {
          final tabs = tabsFor(categories, all);
          final category = tabs.any((c) => c.id == _category) ? _category : null;
          final shown = filterProducts(all, categoryId: category, query: _search.text);
          return RefreshIndicator(
            color: context.zz.brandStrong,
            onRefresh: refresh,
            child: CustomScrollView(
              physics: const AlwaysScrollableScrollPhysics(),
              slivers: [
                SliverPadding(
                  padding: const EdgeInsets.fromLTRB(ZzSpace.screen, ZzSpace.screen, ZzSpace.screen, 0),
                  sliver: SliverList.list(children: [
                    StoreHeader(store: s, roundLine: round == null ? null : roundLine(round)),
                    if (!s.isAcceptingOrders) ...[
                      const SizedBox(height: ZzSpace.md),
                      ZzNotice(kind: ZzNoticeKind.warning, message: closedMessage(s)),
                    ],
                    if (all.isNotEmpty) ...[
                      const SizedBox(height: ZzSpace.md),
                      ZzSearchField(
                        controller: _search,
                        hint: 'Szukaj w ${s.name}',
                        onChanged: (_) => setState(() {}),
                      ),
                    ],
                  ]),
                ),
                if (tabs.isNotEmpty)
                  SliverPersistentHeader(
                    pinned: true,
                    delegate: _TabsHeader(
                      background: context.zz.surface,
                      children: [
                        ZzFilterPill(
                          label: 'Wszystko',
                          selected: category == null,
                          onTap: () => setState(() => _category = null),
                        ),
                        for (final c in tabs)
                          ZzFilterPill(
                            label: c.name,
                            selected: c.id == category,
                            onTap: () => setState(() => _category = c.id == category ? null : c.id),
                          ),
                      ],
                    ),
                  )
                else
                  const SliverToBoxAdapter(child: SizedBox(height: ZzSpace.md)),
                if (all.isEmpty)
                  const SliverFillRemaining(
                    hasScrollBody: false,
                    child: EmptyView(
                      svgAsset: 'assets/svg/empty_box.svg',
                      icon: Icons.inventory_2_outlined,
                      title: 'Brak produktów',
                      subtitle: 'Ten sklep nie dodał jeszcze oferty. Zajrzyj później.',
                    ),
                  )
                else if (shown.isEmpty)
                  SliverFillRemaining(
                    hasScrollBody: false,
                    child: EmptyView(
                      icon: Icons.search_off,
                      title: 'Nic nie znaleźliśmy',
                      subtitle: _search.text.trim().isEmpty
                          ? 'W tej kategorii nie ma teraz produktów.'
                          : 'Sprawdź pisownię albo wpisz krótszą nazwę, np. „ser".',
                      action: OutlinedButton(onPressed: _clearFilters, child: const Text('Pokaż wszystkie produkty')),
                    ),
                  )
                else
                  SliverPadding(
                    padding: const EdgeInsets.fromLTRB(ZzSpace.screen, 0, ZzSpace.screen, 100),
                    sliver: SliverLayoutBuilder(
                      builder: (context, c) => c.crossAxisExtent >= productGridFrom
                          ? _grid(context, shown, c.crossAxisExtent)
                          : SliverList.separated(
                              itemCount: shown.length,
                              separatorBuilder: (_, _) => const SizedBox(height: ZzSpace.sm),
                              itemBuilder: (_, i) => ZzProductRow(product: shown[i], action: _action(shown[i])),
                            ),
                    ),
                  ),
              ],
            ),
          );
        },
      ),
      bottom: AnimatedSwitcher(
        duration: ZzMotion.of(context, ZzMotion.slow),
        switchInCurve: ZzMotion.enter,
        transitionBuilder: (child, anim) => SizeTransition(
          sizeFactor: anim,
          child: FadeTransition(opacity: anim, child: child),
        ),
        child: cart.count > 0
            ? ZzCartBar(
                key: const ValueKey('cartbar'),
                count: cart.count,
                subtotal: cart.subtotal,
                missingToMinimum: cart.cart?.storeId == s.id ? s.minimumOrderValue - cart.subtotal : null,
                onTap: () => context.push('/cart'))
            : const SizedBox.shrink(key: ValueKey('nobar')),
      ),
    );
  }

  Widget _action(Product p) => ProductAddButton(
        product: p,
        storeId: widget.store.id,
        storeName: widget.store.name,
        canOrder: widget.store.isAcceptingOrders,
      );

  /// Siatka kafelków: kolumny maks. 240 px; wysokość kafelka liczona z szerokości zdjęcia (4:3) i skali tekstu,
  /// żeby powiększony tekst (130 %) nie przelewał kafelka.
  Widget _grid(BuildContext context, List<Product> shown, double width) {
    const maxTile = 240.0, gap = ZzSpace.md;
    final cols = ((width + gap) / (maxTile + gap)).ceil();
    final tile = (width - (cols - 1) * gap) / cols;
    final text = MediaQuery.textScalerOf(context).scale(84);
    final extent = 2 * ZzSpace.md + (tile - 2 * ZzSpace.md) * 3 / 4 + ZzSpace.sm + text + zzMinTouch;
    return SliverGrid.builder(
      gridDelegate: SliverGridDelegateWithFixedCrossAxisCount(
        crossAxisCount: cols,
        mainAxisSpacing: gap,
        crossAxisSpacing: gap,
        mainAxisExtent: extent,
      ),
      itemCount: shown.length,
      itemBuilder: (_, i) => ZzProductTile(product: shown[i], action: _action(shown[i])),
    );
  }
}

/// Komunikat, gdy sklep nie przyjmuje zamówień (zamknięty / chwilowo niedostępny / nieaktywny) — oferta widoczna,
/// dodawanie do koszyka wyłączone, zamiast błędu dopiero przy zamówieniu.
String closedMessage(Store store) {
  final reason = switch (store.status) {
    'Closed' => 'Sklep jest teraz zamknięty.',
    'TemporarilyUnavailable' => 'Sklep jest chwilowo niedostępny.',
    _ => 'Sklep nie przyjmuje teraz zamówień.',
  };
  return '$reason Możesz przejrzeć ofertę — zamówienie złożysz, gdy sklep wznowi przyjmowanie zamówień.';
}

/// Przyklejony pasek zakładek kategorii (zostaje pod paskiem aplikacji przy przewijaniu oferty).
class _TabsHeader extends SliverPersistentHeaderDelegate {
  final List<Widget> children;
  final Color background;
  _TabsHeader({required this.children, required this.background});

  static const _height = 64.0;

  @override
  double get minExtent => _height;
  @override
  double get maxExtent => _height;

  @override
  Widget build(BuildContext context, double shrinkOffset, bool overlapsContent) => ColoredBox(
        color: background,
        child: ListView.separated(
          scrollDirection: Axis.horizontal,
          padding: const EdgeInsets.symmetric(horizontal: ZzSpace.screen, vertical: ZzSpace.sm),
          itemCount: children.length,
          separatorBuilder: (_, _) => const SizedBox(width: ZzSpace.sm),
          itemBuilder: (_, i) => children[i],
        ),
      );

  @override
  bool shouldRebuild(covariant _TabsHeader old) => true;
}
