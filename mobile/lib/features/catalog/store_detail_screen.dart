import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/providers.dart';
import '../../core/theme/zz_theme.dart';
import '../../core/util/format.dart';
import '../../core/widgets/cart_button.dart';
import '../../core/widgets/skeleton.dart';
import '../../core/widgets/states.dart';
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
  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) {
      ref.read(cartControllerProvider.notifier).ensureCartForStore(widget.storeId);
    });
  }

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
          return RefreshIndicator(
            color: ZzColors.orange,
            onRefresh: () async => ref.invalidate(productsProvider(widget.storeId)),
            child: ListView.separated(
              padding: const EdgeInsets.fromLTRB(16, 16, 16, 100),
              itemCount: list.length,
              separatorBuilder: (_, _) => const SizedBox(height: 10),
              itemBuilder: (_, i) => _ProductRow(product: list[i]),
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
  const _ProductRow({required this.product});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final qty = ref.watch(cartControllerProvider).cart == null
        ? 0
        : ref.read(cartControllerProvider.notifier).quantityOf(product.id);
    final controller = ref.read(cartControllerProvider.notifier);
    final available = product.isAvailable;

    return Card(
      child: Padding(
        padding: const EdgeInsets.all(12),
        child: Row(
          children: [
            Container(
              width: 46,
              height: 46,
              decoration: BoxDecoration(
                color: context.zz.surface,
                borderRadius: BorderRadius.circular(ZzRadius.md),
              ),
              child: Icon(Icons.local_grocery_store_outlined, color: context.zz.textMuted),
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
                      onPressed: () => controller.add(product.id),
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
