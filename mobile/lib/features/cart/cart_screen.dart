import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/theme/zz_theme.dart';
import '../../core/util/format.dart';
import '../../core/widgets/states.dart';
import '../catalog/store_detail_screen.dart';
import 'cart_controller.dart';

class CartScreen extends ConsumerWidget {
  const CartScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final cart = ref.watch(cartControllerProvider);
    final controller = ref.read(cartControllerProvider.notifier);
    final c = cart.cart;

    return Scaffold(
      appBar: AppBar(title: const Text('Koszyk')),
      body: (c == null || c.items.isEmpty)
          ? const EmptyView(
              icon: Icons.shopping_cart_outlined,
              title: 'Koszyk jest pusty',
              subtitle: 'Dodaj produkty ze sklepu, aby złożyć zamówienie.',
            )
          : Column(
              children: [
                if (cart.error != null)
                  Container(
                    width: double.infinity,
                    color: const Color(0xFFFEECEC),
                    padding: const EdgeInsets.all(12),
                    child: Text(cart.error!, style: const TextStyle(color: ZzColors.danger)),
                  ),
                Expanded(
                  child: ListView.separated(
                    padding: const EdgeInsets.all(16),
                    itemCount: c.items.length,
                    separatorBuilder: (_, _) => const Divider(height: 20),
                    itemBuilder: (_, i) {
                      final it = c.items[i];
                      return Row(
                        children: [
                          Expanded(
                            child: Column(
                              crossAxisAlignment: CrossAxisAlignment.start,
                              children: [
                                Text(it.productName,
                                    style: const TextStyle(fontWeight: FontWeight.w600)),
                                Text('${zl(it.unitPrice)} × ${it.quantity}',
                                    style: const TextStyle(
                                        color: ZzColors.textMuted, fontSize: 13)),
                              ],
                            ),
                          ),
                          Text(zl(it.lineTotal),
                              style: const TextStyle(fontWeight: FontWeight.w700)),
                          IconButton(
                            icon: const Icon(Icons.remove_circle_outline, color: ZzColors.textMuted),
                            onPressed: () =>
                                controller.setQuantity(it.productId, it.quantity - 1),
                          ),
                        ],
                      );
                    },
                  ),
                ),
                _CartFooter(storeId: c.storeId, subtotal: cart.subtotal),
              ],
            ),
    );
  }
}

class _CartFooter extends ConsumerWidget {
  final String storeId;
  final double subtotal;
  const _CartFooter({required this.storeId, required this.subtotal});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final store = ref.watch(storeDetailProvider(storeId));
    final minOrder = store.valueOrNull?.minimumOrderValue ?? 0;
    final belowMin = subtotal < minOrder;

    return SafeArea(
      minimum: const EdgeInsets.all(16),
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              const Text('Wartość produktów', style: TextStyle(color: ZzColors.textMuted)),
              Text(zl(subtotal), style: const TextStyle(fontWeight: FontWeight.w700)),
            ],
          ),
          const SizedBox(height: 4),
          const Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              Text('Opłata za dostawę', style: TextStyle(color: ZzColors.textMuted)),
              Text('liczona przy wyborze strefy',
                  style: TextStyle(color: ZzColors.textMuted, fontSize: 12)),
            ],
          ),
          if (belowMin) ...[
            const SizedBox(height: 8),
            Container(
              width: double.infinity,
              padding: const EdgeInsets.all(10),
              decoration: BoxDecoration(
                color: ZzColors.orange50,
                borderRadius: BorderRadius.circular(ZzRadius.sm),
              ),
              child: Text(
                'Minimalna wartość zamówienia to ${zl(minOrder)}. Dodaj jeszcze ${zl(minOrder - subtotal)}.',
                style: const TextStyle(color: ZzColors.orange600, fontSize: 13),
              ),
            ),
          ],
          const SizedBox(height: 12),
          ElevatedButton(
            onPressed: belowMin ? null : () => context.push('/checkout'),
            child: const Text('Przejdź do dostawy i płatności'),
          ),
        ],
      ),
    );
  }
}
