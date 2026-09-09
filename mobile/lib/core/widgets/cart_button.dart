import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../theme/zz_theme.dart';
import '../../features/cart/cart_controller.dart';

/// Ikona koszyka z licznikiem pozycji (dla AppBar).
class CartButton extends ConsumerWidget {
  const CartButton({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final count = ref.watch(cartControllerProvider).count;
    return Stack(
      alignment: Alignment.center,
      children: [
        IconButton(
          icon: const Icon(Icons.shopping_cart_outlined),
          onPressed: () => context.push('/cart'),
          tooltip: 'Koszyk',
        ),
        if (count > 0)
          Positioned(
            right: 6,
            top: 8,
            child: Container(
              padding: const EdgeInsets.symmetric(horizontal: 5, vertical: 1),
              decoration: BoxDecoration(
                color: ZzColors.orange,
                borderRadius: BorderRadius.circular(10),
              ),
              constraints: const BoxConstraints(minWidth: 16),
              child: Text('$count',
                  textAlign: TextAlign.center,
                  style: const TextStyle(
                      color: Colors.white, fontSize: 11, fontWeight: FontWeight.w700)),
            ),
          ),
      ],
    );
  }
}
