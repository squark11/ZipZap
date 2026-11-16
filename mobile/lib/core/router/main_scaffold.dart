import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_svg/flutter_svg.dart';
import 'package:go_router/go_router.dart';

import '../../features/cart/cart_controller.dart';
import '../theme/zz_theme.dart';
import '../ui/zz_layout.dart';
import '../widgets/zz_icon.dart';
import '../widgets/zz_logo.dart';

/// Zakładka nawigacji głównej.
class _Tab {
  final String path;
  final String label;
  final String icon;
  final double maxWidth; // szerokość treści na tablecie / komputerze
  const _Tab(this.path, this.label, this.icon, this.maxWidth);
}

/// Powłoka z nawigacją główną (E2-4, E2-6): Start · Szukaj · Koszyk · Zamówienia · Konto.
/// Poniżej 900 px — dolny pasek; od 900 px — nawigacja boczna (od 1200 px rozwinięta, z logo).
/// Treść zakładki jest wyśrodkowana i ograniczona szerokością (bez rozciągania list na cały monitor).
class MainScaffold extends ConsumerWidget {
  final Widget child;
  const MainScaffold({super.key, required this.child});

  static const _tabs = [
    _Tab('/stores', 'Start', 'home', ZzBreakpoints.content),
    _Tab('/search', 'Szukaj', 'search', ZzBreakpoints.singleColumn),
    _Tab('/cart', 'Koszyk', 'cart', ZzBreakpoints.singleColumn),
    _Tab('/orders', 'Zamówienia', 'orders', ZzBreakpoints.singleColumn),
    _Tab('/account', 'Konto', 'account', ZzBreakpoints.singleColumn),
  ];

  static int indexFor(String loc) {
    final i = _tabs.indexWhere((t) => loc == t.path || loc.startsWith('${t.path}/'));
    return i < 0 ? 0 : i;
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final loc = GoRouterState.of(context).uri.path;
    final index = indexFor(loc);
    final cartCount = ref.watch(cartControllerProvider).count;

    Widget icon(_Tab t, {required bool selected}) {
      final glyph = ZzIcon(t.icon, color: selected ? context.zz.brandStrong : context.zz.textMuted);
      if (t.path != '/cart') return glyph;
      return Badge(
        isLabelVisible: cartCount > 0,
        backgroundColor: ZzColors.accent,
        textColor: ZzColors.onAccent,
        label: Text('$cartCount'),
        child: glyph,
      );
    }

    final content = ZzPage(maxWidth: _tabs[index].maxWidth, child: child);

    if (!context.zzSideNav) {
      return Scaffold(
        body: content,
        bottomNavigationBar: NavigationBar(
          selectedIndex: index,
          onDestinationSelected: (i) => context.go(_tabs[i].path),
          destinations: [
            for (final t in _tabs)
              NavigationDestination(
                icon: icon(t, selected: false),
                selectedIcon: icon(t, selected: true),
                label: t.label,
              ),
          ],
        ),
      );
    }

    final extended = context.zzWidth == ZzWidth.large;
    return Scaffold(
      body: Row(
        children: [
          NavigationRail(
            selectedIndex: index,
            extended: extended,
            minExtendedWidth: 220,
            labelType: extended ? NavigationRailLabelType.none : NavigationRailLabelType.all,
            leading: Padding(
              padding: const EdgeInsets.symmetric(vertical: ZzSpace.lg),
              child: extended
                  ? const ZzLogo(height: 30)
                  : SvgPicture.asset('assets/svg/logo_mark.svg', height: 30, semanticsLabel: 'Dowózka.pl'),
            ),
            onDestinationSelected: (i) => context.go(_tabs[i].path),
            destinations: [
              for (final t in _tabs)
                NavigationRailDestination(
                  icon: icon(t, selected: false),
                  selectedIcon: icon(t, selected: true),
                  label: Text(t.label),
                ),
            ],
          ),
          VerticalDivider(width: 1, color: context.zz.border),
          Expanded(child: content),
        ],
      ),
    );
  }
}
