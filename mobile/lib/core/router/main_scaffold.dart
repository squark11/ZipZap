import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../features/cart/cart_controller.dart';
import '../widgets/zz_icon.dart';

/// Powłoka z dolną nawigacją dla głównych zakładek (Sklepy/Koszyk/Zamówienia/Konto).
class MainScaffold extends ConsumerWidget {
  final Widget child;
  const MainScaffold({super.key, required this.child});

  static const _tabs = ['/stores', '/cart', '/orders', '/account'];

  int _indexFor(String loc) {
    final i = _tabs.indexWhere((t) => loc == t || loc.startsWith('$t/'));
    return i < 0 ? 0 : i;
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final loc = GoRouterState.of(context).uri.path;
    final index = _indexFor(loc);
    final cartCount = ref.watch(cartControllerProvider).count;

    return Scaffold(
      body: child,
      bottomNavigationBar: NavigationBar(
        selectedIndex: index,
        onDestinationSelected: (i) => context.go(_tabs[i]),
        destinations: [
          const NavigationDestination(
            icon: ZzIcon('store'),
            label: 'Sklepy',
          ),
          NavigationDestination(
            icon: Badge(
              isLabelVisible: cartCount > 0,
              label: Text('$cartCount'),
              child: const ZzIcon('cart'),
            ),
            label: 'Koszyk',
          ),
          const NavigationDestination(
            icon: ZzIcon('orders'),
            label: 'Zamówienia',
          ),
          const NavigationDestination(
            icon: ZzIcon('account'),
            label: 'Konto',
          ),
        ],
      ),
    );
  }
}
