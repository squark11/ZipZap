import 'package:flutter/material.dart';

import '../theme/zz_theme.dart';

/// Statyczny prostokąt „skeleton” (miejsce na treść podczas ładowania). Sam się nie animuje — ruch daje
/// jeden wspólny [ZzShimmer] nad całą listą (wydajność: 1 zegar animacji zamiast kilkudziesięciu).
class ZzSkeleton extends StatelessWidget {
  final double? width;
  final double height;
  final BorderRadiusGeometry? borderRadius;

  const ZzSkeleton({super.key, this.width, this.height = 14, this.borderRadius});

  @override
  Widget build(BuildContext context) {
    // Cienkie paski → pełne zaokrąglenie („pigułka”), żeby nie wyglądały jak linijka tekstu.
    final radius =
        borderRadius ?? (height <= 20 ? BorderRadius.circular(height / 2) : BorderRadius.circular(ZzRadius.field));
    return Container(
      width: width,
      height: height,
      decoration: BoxDecoration(color: context.zz.skeleton, borderRadius: radius),
    );
  }
}

/// Delikatne „oddychanie” (przezroczystość 1 → 0,55 → 1, okres 1,2 s) nad całym blokiem szkieletów.
/// Jeden zegar na listę; zmienia się tylko przezroczystość warstwy — dzieci nie są przebudowywane ani
/// przemalowywane. Przy systemowym „ogranicz ruch” zegar w ogóle nie powstaje (statyczne szkielety).
class ZzShimmer extends StatefulWidget {
  final Widget child;
  const ZzShimmer({super.key, required this.child});

  @override
  State<ZzShimmer> createState() => _ZzShimmerState();
}

class _ZzShimmerState extends State<ZzShimmer> with TickerProviderStateMixin {
  AnimationController? _c;
  Animation<double>? _opacity;

  @override
  void didChangeDependencies() {
    super.didChangeDependencies();
    final reduced = ZzMotion.reduced(context);
    if (reduced && _c != null) {
      _c!.dispose();
      _c = null;
      _opacity = null;
    } else if (!reduced && _c == null) {
      _c = AnimationController(vsync: this, duration: const Duration(milliseconds: 600))..repeat(reverse: true);
      _opacity = Tween<double>(begin: 1, end: 0.55).animate(CurvedAnimation(parent: _c!, curve: Curves.easeInOut));
    }
  }

  @override
  void dispose() {
    _c?.dispose();
    super.dispose();
  }

  /// Do testów: czy zegar animacji istnieje i pracuje.
  @visibleForTesting
  bool get isTicking => _c?.isAnimating ?? false;

  @override
  Widget build(BuildContext context) =>
      _opacity == null ? widget.child : FadeTransition(opacity: _opacity!, child: widget.child);
}

/// Lista „szkieletów” kart sklepu — zastępuje spinner podczas ładowania.
class StoreListSkeleton extends StatelessWidget {
  final int count;
  const StoreListSkeleton({super.key, this.count = 6});

  @override
  Widget build(BuildContext context) {
    return ZzShimmer(
      child: ListView.separated(
        padding: const EdgeInsets.all(16),
        itemCount: count,
        separatorBuilder: (_, _) => const SizedBox(height: 12),
        itemBuilder: (_, _) => const _StoreCardSkeleton(),
      ),
    );
  }
}

class _StoreCardSkeleton extends StatelessWidget {
  const _StoreCardSkeleton();

  @override
  Widget build(BuildContext context) {
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Row(
          children: [
            const ZzSkeleton(width: 48, height: 48),
            const SizedBox(width: 14),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: const [
                  ZzSkeleton(width: 150, height: 15),
                  SizedBox(height: 8),
                  ZzSkeleton(width: 100, height: 12),
                ],
              ),
            ),
            const SizedBox(width: 12),
            const ZzSkeleton(width: 62, height: 22),
          ],
        ),
      ),
    );
  }
}

/// „Szkielety” wierszy produktu — dla ekranu sklepu podczas ładowania oferty.
class ProductListSkeleton extends StatelessWidget {
  final int count;
  const ProductListSkeleton({super.key, this.count = 7});

  @override
  Widget build(BuildContext context) {
    return ZzShimmer(
      child: ListView.separated(
        padding: const EdgeInsets.fromLTRB(16, 16, 16, 100),
        itemCount: count,
        separatorBuilder: (_, _) => const SizedBox(height: 10),
        itemBuilder: (_, _) => const _ProductRowSkeleton(),
      ),
    );
  }
}

class _ProductRowSkeleton extends StatelessWidget {
  const _ProductRowSkeleton();

  @override
  Widget build(BuildContext context) {
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(12),
        child: Row(
          children: [
            const ZzSkeleton(width: 46, height: 46),
            const SizedBox(width: 12),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: const [
                  ZzSkeleton(width: 140, height: 14),
                  SizedBox(height: 8),
                  ZzSkeleton(width: 80, height: 12),
                ],
              ),
            ),
            const SizedBox(width: 12),
            const ZzSkeleton(width: 64, height: 38),
          ],
        ),
      ),
    );
  }
}

/// „Szkielety” kart zamówień — dla listy „Moje zamówienia”.
class OrderListSkeleton extends StatelessWidget {
  final int count;
  const OrderListSkeleton({super.key, this.count = 6});

  @override
  Widget build(BuildContext context) {
    return ZzShimmer(
      child: ListView.separated(
        padding: const EdgeInsets.all(16),
        itemCount: count,
        separatorBuilder: (_, _) => const SizedBox(height: 10),
        itemBuilder: (_, _) => const _OrderCardSkeleton(),
      ),
    );
  }
}

class _OrderCardSkeleton extends StatelessWidget {
  const _OrderCardSkeleton();

  @override
  Widget build(BuildContext context) {
    return Card(
      child: Padding(
        padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 16),
        child: Row(
          children: [
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: const [
                  ZzSkeleton(width: 90, height: 14),
                  SizedBox(height: 8),
                  ZzSkeleton(width: 160, height: 12),
                ],
              ),
            ),
            const SizedBox(width: 12),
            const ZzSkeleton(width: 76, height: 22),
          ],
        ),
      ),
    );
  }
}
