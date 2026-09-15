import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/providers.dart';
import '../../core/theme/zz_theme.dart';
import '../../core/util/format.dart';
import '../../core/widgets/cart_button.dart';
import '../../core/widgets/skeleton.dart';
import '../../core/widgets/states.dart';
import 'store_header.dart';
import '../../models/product.dart';
import '../../models/store.dart';
import '../cart/cart_controller.dart';

final storeDetailProvider = FutureProvider.autoDispose.family<Store, String>(
    (ref, id) => ref.read(catalogRepositoryProvider).getStore(id));

final productsProvider = FutureProvider.autoDispose.family<List<Product>, String>(
    (ref, storeId) => ref.read(catalogRepositoryProvider).listProducts(storeId));

class StoreDetailScreen extends ConsumerStatefulWidget {
  final String storeId;
  const StoreDetailScreen({super.key, required this.storeId});

  @override
  ConsumerState<StoreDetailScreen> createState() => _StoreDetailScreenState();
}

class _StoreDetailScreenState extends ConsumerState<StoreDetailScreen> {
  // Koszyk NIE jest tworzony przy wejściu do sklepu — dopiero przy pierwszym dodaniu
  // produktu (patrz `_ProductRow._add`). Dzięki temu wejście na stronę innego sklepu
  // nie kasuje po cichu koszyka z poprzedniego sklepu.
  @override
  Widget build(BuildContext context) {
    final store = ref.watch(storeDetailProvider(widget.storeId));
    final products = ref.watch(productsProvider(widget.storeId));
    final cart = ref.watch(cartControllerProvider);

    return Scaffold(
      appBar: AppBar(
        title: Text(store.valueOrNull?.name ?? 'Sklep'),
        actions: const [CartButton()],
      ),
      body: products.when(
        loading: () => const ProductListSkeleton(),
        error: (e, _) => ErrorView(
          message: e.toString(),
          onRetry: () => ref.invalidate(productsProvider(widget.storeId)),
        ),
        data: (list) {
          if (list.isEmpty) {
            return const EmptyView(
              svgAsset: 'assets/svg/empty_box.svg',
              icon: Icons.inventory_2_outlined,
              title: 'Brak produktów',
              subtitle: 'Ten sklep nie dodał jeszcze oferty.',
            );
          }
          final storeVal = store.valueOrNull;
          return RefreshIndicator(
            color: ZzColors.orange,
            onRefresh: () async {
              ref.invalidate(productsProvider(widget.storeId));
              ref.invalidate(storeDetailProvider(widget.storeId));
            },
            child: ListView.builder(
              padding: const EdgeInsets.fromLTRB(16, 16, 16, 100),
              itemCount: list.length + 1,
              itemBuilder: (_, i) {
                if (i == 0) {
                  if (storeVal == null) return const SizedBox.shrink();
                  return Padding(
                    padding: const EdgeInsets.only(bottom: 16),
                    child: StoreHeader(store: storeVal),
                  );
                }
                return Padding(
                  padding: const EdgeInsets.only(bottom: 10),
                  child: _ProductRow(
                    product: list[i - 1],
                    storeId: widget.storeId,
                    storeName: storeVal?.name ?? 'tym sklepie',
                  ),
                );
              },
            ),
          );
        },
      ),
      bottomNavigationBar: AnimatedSwitcher(
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

class _ProductRow extends ConsumerWidget {
  final Product product;
  final String storeId;
  final String storeName;
  const _ProductRow({required this.product, required this.storeId, required this.storeName});

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
                  Text('${zl(product.price)} / ${product.unit}',
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
                      onPressed: () => _add(context, ref),
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
    await controller.addFromStore(storeId, product.id);
  }
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
