import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/api/api_exception.dart';
import '../../core/navigation/last_store.dart';
import '../../core/providers.dart';
import '../../core/theme/zz_theme.dart';
import '../../core/util/format.dart';
import '../../core/widgets/account_button.dart';
import '../../core/widgets/cart_button.dart';
import '../../core/widgets/install_hint.dart';
import '../../core/widgets/skeleton.dart';
import '../../core/widgets/states.dart';
import 'store_header.dart';
import '../../models/product.dart';
import '../../models/store.dart';
import '../cart/cart_controller.dart';

/// Sklep po slugu (stały link z kodu QR) albo po id (z listy).
final storeDetailProvider = FutureProvider.autoDispose.family<Store, String>(
    (ref, idOrSlug) => ref.read(catalogRepositoryProvider).getStore(idOrSlug));

/// Produkty sklepu — zawsze po id (API przyjmuje tu tylko identyfikator, nie slug).
final productsProvider = FutureProvider.autoDispose.family<List<Product>, String>(
    (ref, storeId) => ref.read(catalogRepositoryProvider).listProducts(storeId));

/// Oferta sklepu. Wejście z kodu QR (`/s/{slug}`) i z listy sklepów (`/stores/{id}`) prowadzi tutaj.
/// Stany: ładowanie, sklep nie istnieje, błąd sieci (ponów), sklep nie przyjmuje zamówień, brak produktów.
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
            ? const _StoreNotFound()
            : ErrorView(
                message: e.toString(),
                onRetry: () => ref.invalidate(storeDetailProvider(storeRef)),
              ),
      ),
      data: (s) => _StoreOffer(store: s, storeRef: storeRef),
    );
  }
}

/// Wspólna rama ekranu: powrót (albo „Wszystkie sklepy", gdy klient wszedł bezpośrednio z kodu QR),
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

class _StoreNotFound extends StatelessWidget {
  const _StoreNotFound();

  @override
  Widget build(BuildContext context) => Center(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            const EmptyView(
              icon: Icons.storefront_outlined,
              title: 'Nie znaleziono sklepu',
              subtitle: 'Kod QR lub link może być nieaktualny. Zobacz sklepy dostępne w Dowózka.pl.',
            ),
            ElevatedButton(
              onPressed: () => context.go('/stores'),
              child: const Text('Zobacz sklepy'),
            ),
          ],
        ),
      );
}

class _StoreOffer extends ConsumerStatefulWidget {
  final Store store;
  final String storeRef;
  const _StoreOffer({required this.store, required this.storeRef});

  @override
  ConsumerState<_StoreOffer> createState() => _StoreOfferState();
}

class _StoreOfferState extends ConsumerState<_StoreOffer> {
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

  // Po zalogowaniu (i po odświeżeniu strony) klient wraca do oferty tego sklepu.
  void _remember() => Future.microtask(() => ref.read(lastStoreProvider.notifier).remember(widget.store));

  // Koszyk NIE jest tworzony przy wejściu do sklepu — dopiero przy pierwszym dodaniu
  // produktu (patrz `_ProductRow._add`). Dzięki temu wejście na stronę innego sklepu
  // nie kasuje po cichu koszyka z poprzedniego sklepu.
  @override
  Widget build(BuildContext context) {
    final s = widget.store;
    final products = ref.watch(productsProvider(s.id));
    final cart = ref.watch(cartControllerProvider);

    Future<void> refresh() async {
      ref.invalidate(productsProvider(s.id));
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
        data: (list) => RefreshIndicator(
          color: ZzColors.orange,
          onRefresh: refresh,
          child: ListView.builder(
            padding: const EdgeInsets.fromLTRB(16, 16, 16, 100),
            itemCount: list.isEmpty ? 2 : list.length + 1,
            itemBuilder: (_, i) {
              if (i == 0) {
                return Padding(
                  padding: const EdgeInsets.only(bottom: 16),
                  child: Column(children: [
                    StoreHeader(store: s),
                    if (!s.isAcceptingOrders) ...[
                      const SizedBox(height: 12),
                      StoreClosedNotice(store: s),
                    ],
                  ]),
                );
              }
              if (list.isEmpty) {
                return const Padding(
                  padding: EdgeInsets.only(top: 24),
                  child: EmptyView(
                    svgAsset: 'assets/svg/empty_box.svg',
                    icon: Icons.inventory_2_outlined,
                    title: 'Brak produktów',
                    subtitle: 'Ten sklep nie dodał jeszcze oferty. Zajrzyj później.',
                  ),
                );
              }
              return Padding(
                padding: const EdgeInsets.only(bottom: 10),
                child: _ProductRow(
                  product: list[i - 1],
                  storeId: s.id,
                  storeName: s.name,
                  canOrder: s.isAcceptingOrders,
                ),
              );
            },
          ),
        ),
      ),
      bottom: AnimatedSwitcher(
        duration: const Duration(milliseconds: 240),
        switchInCurve: Curves.easeOutBack,
        transitionBuilder: (child, anim) => SizeTransition(
          sizeFactor: anim,
          child: FadeTransition(opacity: anim, child: child),
        ),
        child: cart.count > 0
            ? _CartBar(
                key: const ValueKey('cartbar'),
                count: cart.count,
                subtotal: cart.subtotal)
            : const SizedBox.shrink(key: ValueKey('nobar')),
      ),
    );
  }
}

/// Sklep nie przyjmuje teraz zamówień (zamknięty / chwilowo niedostępny / nieaktywny) — oferta widoczna,
/// dodawanie do koszyka wyłączone, zamiast błędu dopiero przy zamówieniu.
class StoreClosedNotice extends StatelessWidget {
  final Store store;
  const StoreClosedNotice({super.key, required this.store});

  String get _reason => switch (store.status) {
        'Closed' => 'Sklep jest teraz zamknięty.',
        'TemporarilyUnavailable' => 'Sklep jest chwilowo niedostępny.',
        _ => 'Sklep nie przyjmuje teraz zamówień.',
      };

  @override
  Widget build(BuildContext context) => Container(
        width: double.infinity,
        padding: const EdgeInsets.all(12),
        decoration: BoxDecoration(
          color: context.zz.orangeTint,
          borderRadius: BorderRadius.circular(ZzRadius.md),
        ),
        child: Text(
          '$_reason Możesz przejrzeć ofertę — zamówienie złożysz, gdy sklep wznowi przyjmowanie zamówień.',
          style: const TextStyle(color: ZzColors.orange600, fontSize: 13.5),
        ),
      );
}

class _ProductRow extends ConsumerWidget {
  final Product product;
  final String storeId;
  final String storeName;
  final bool canOrder;
  const _ProductRow({required this.product, required this.storeId, required this.storeName, this.canOrder = true});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final qty = ref.watch(cartControllerProvider).cart == null
        ? 0
        : ref.read(cartControllerProvider.notifier).quantityOf(product.id);
    final controller = ref.read(cartControllerProvider.notifier);
    final available = product.isAvailable;
    final placeholder = Container(
      width: 46,
      height: 46,
      decoration: BoxDecoration(
        color: context.zz.surface,
        borderRadius: BorderRadius.circular(ZzRadius.md),
      ),
      child: Icon(Icons.local_grocery_store_outlined, color: context.zz.textMuted),
    );
    final hasImage = product.imageUrl != null && product.imageUrl!.isNotEmpty;

    return Card(
      child: Padding(
        padding: const EdgeInsets.all(12),
        child: Row(
          children: [
            ClipRRect(
              borderRadius: BorderRadius.circular(ZzRadius.md),
              child: SizedBox(
                width: 46,
                height: 46,
                child: hasImage
                    ? Image.network(
                        product.imageUrl!,
                        fit: BoxFit.cover,
                        errorBuilder: (_, _, _) => placeholder,
                        loadingBuilder: (context, child, progress) => progress == null ? child : placeholder,
                      )
                    : placeholder,
              ),
            ),
            const SizedBox(width: 12),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(product.name,
                      style: const TextStyle(fontWeight: FontWeight.w600, fontSize: 15)),
                  const SizedBox(height: 2),
                  Text(
                      product.hasMultipleUnits
                          ? product.unitOptions.map((o) => '${zl(o.price)}/${o.unit}').join('  ·  ')
                          : '${zl(product.price)} / ${product.unit}',
                      style: TextStyle(color: context.zz.textMuted, fontSize: 13)),
                  if (!available)
                    const Text('Niedostępny',
                        style: TextStyle(color: ZzColors.danger, fontSize: 12)),
                ],
              ),
            ),
            if (available)
              qty == 0
                  ? OutlinedButton(
                      onPressed: canOrder ? () => _add(context, ref) : null,
                      style: OutlinedButton.styleFrom(
                        minimumSize: const Size(40, 40),
                        padding: const EdgeInsets.symmetric(horizontal: 14),
                      ),
                      child: const Text('Dodaj'),
                    )
                  : _Stepper(
                      quantity: qty,
                      onMinus: () => controller.setQuantity(product.id, qty - 1),
                      onPlus: () => controller.setQuantity(product.id, qty + 1),
                    ),
          ],
        ),
      ),
    );
  }

  /// Dodaje produkt do koszyka. Jeśli koszyk zawiera produkty z innego sklepu,
  /// prosi o potwierdzenie opróżnienia (koszyk jest jednosklepowy).
  Future<void> _add(BuildContext context, WidgetRef ref) async {
    final controller = ref.read(cartControllerProvider.notifier);
    if (controller.hasItemsFromOtherStore(storeId)) {
      final ok = await showDialog<bool>(
        context: context,
        builder: (ctx) => AlertDialog(
          title: const Text('Zacząć nowy koszyk?'),
          content: Text(
              'Twój koszyk zawiera produkty z innego sklepu. Dodanie „${product.name}" '
              'opróżni obecny koszyk i zacznie zakupy w „$storeName".'),
          actions: [
            TextButton(onPressed: () => Navigator.pop(ctx, false), child: const Text('Anuluj')),
            ElevatedButton(onPressed: () => Navigator.pop(ctx, true), child: const Text('Opróżnij i dodaj')),
          ],
        ),
      );
      if (ok != true) return;
    }

    // Produkt z kilkoma jednostkami — klient wybiera którą kupuje.
    String? unit;
    if (product.hasMultipleUnits) {
      if (!context.mounted) return;
      unit = await _pickUnit(context);
      if (unit == null) return; // anulowano
    }
    await controller.addFromStore(storeId, product.id, unit: unit);
  }

  /// Bottom sheet z wyborem jednostki (np. „1 kg — 4,99 zł" / „1 szt — 1,20 zł").
  Future<String?> _pickUnit(BuildContext context) => showModalBottomSheet<String>(
        context: context,
        builder: (ctx) => SafeArea(
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              Padding(
                padding: const EdgeInsets.fromLTRB(20, 18, 20, 6),
                child: Text('Wybierz jednostkę — ${product.name}',
                    style: const TextStyle(fontWeight: FontWeight.w700, fontSize: 16)),
              ),
              ...product.unitOptions.map((o) => ListTile(
                    title: Text('1 ${o.unit}'),
                    trailing: Text(zl(o.price),
                        style: const TextStyle(fontWeight: FontWeight.w700, color: ZzColors.orange600)),
                    onTap: () => Navigator.pop(ctx, o.unit),
                  )),
              const SizedBox(height: 8),
            ],
          ),
        ),
      );
}

class _Stepper extends StatelessWidget {
  final int quantity;
  final VoidCallback onMinus;
  final VoidCallback onPlus;
  const _Stepper({required this.quantity, required this.onMinus, required this.onPlus});

  @override
  Widget build(BuildContext context) {
    return Container(
      decoration: BoxDecoration(
        border: Border.all(color: context.zz.border),
        borderRadius: BorderRadius.circular(ZzRadius.md),
      ),
      child: Row(
        children: [
          _sqBtn(Icons.remove, onMinus),
          SizedBox(
            width: 28,
            child: Text('$quantity',
                textAlign: TextAlign.center,
                style: const TextStyle(fontWeight: FontWeight.w700)),
          ),
          _sqBtn(Icons.add, onPlus),
        ],
      ),
    );
  }

  Widget _sqBtn(IconData icon, VoidCallback onTap) => InkWell(
        onTap: onTap,
        child: Padding(
          padding: const EdgeInsets.all(8),
          child: Icon(icon, size: 18, color: ZzColors.orange),
        ),
      );
}

class _CartBar extends StatelessWidget {
  final int count;
  final double subtotal;
  const _CartBar({super.key, required this.count, required this.subtotal});

  @override
  Widget build(BuildContext context) {
    return SafeArea(
      minimum: const EdgeInsets.all(16),
      child: SizedBox(
        height: 52,
        child: ElevatedButton(
          onPressed: () => context.push('/cart'),
          child: Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              Text('Koszyk ($count)'),
              Text('${zl(subtotal)} →', style: const TextStyle(fontWeight: FontWeight.w700)),
            ],
          ),
        ),
      ),
    );
  }
}
