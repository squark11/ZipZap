import 'package:flutter/material.dart';

import '../theme/zz_theme.dart';

/// Prostokąt „skeleton” z delikatnym efektem shimmer (ładowanie treści).
class ZzSkeleton extends StatefulWidget {
  final double? width;
  final double height;
  final BorderRadiusGeometry? borderRadius;

  const ZzSkeleton({super.key, this.width, this.height = 14, this.borderRadius});

  @override
  State<ZzSkeleton> createState() => _ZzSkeletonState();
}

class _ZzSkeletonState extends State<ZzSkeleton>
    with SingleTickerProviderStateMixin {
  late final AnimationController _c =
      AnimationController(vsync: this, duration: const Duration(milliseconds: 1200))
        ..repeat();

  @override
  void dispose() {
    _c.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final dark = Theme.of(context).brightness == Brightness.dark;
    final base = context.zz.border;
    final hi = Color.lerp(base, Colors.white, dark ? 0.12 : 0.7)!;
    return AnimatedBuilder(
      animation: _c,
      builder: (_, _) {
        final v = _c.value;
        return Container(
          width: widget.width,
          height: widget.height,
          decoration: BoxDecoration(
            borderRadius: widget.borderRadius ?? BorderRadius.circular(ZzRadius.sm),
            gradient: LinearGradient(
              begin: Alignment.centerLeft,
              end: Alignment.centerRight,
              colors: [base, hi, base],
              stops: [
                (v - 0.3).clamp(0.0, 1.0),
                v.clamp(0.0, 1.0),
                (v + 0.3).clamp(0.0, 1.0),
              ],
            ),
          ),
        );
      },
    );
  }
}

/// Lista „szkieletów” kart sklepu — zastępuje spinner podczas ładowania.
class StoreListSkeleton extends StatelessWidget {
  final int count;
  const StoreListSkeleton({super.key, this.count = 6});

  @override
  Widget build(BuildContext context) {
    return ListView.separated(
      padding: const EdgeInsets.all(16),
      itemCount: count,
      separatorBuilder: (_, _) => const SizedBox(height: 12),
      itemBuilder: (_, _) => const _StoreCardSkeleton(),
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
    return ListView.separated(
      padding: const EdgeInsets.fromLTRB(16, 16, 16, 100),
      itemCount: count,
      separatorBuilder: (_, _) => const SizedBox(height: 10),
      itemBuilder: (_, _) => const _ProductRowSkeleton(),
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
    return ListView.separated(
      padding: const EdgeInsets.all(16),
      itemCount: count,
      separatorBuilder: (_, _) => const SizedBox(height: 10),
      itemBuilder: (_, _) => const _OrderCardSkeleton(),
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
