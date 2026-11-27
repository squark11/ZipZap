import 'package:flutter/material.dart';

import '../theme/zz_theme.dart';
import '../util/format.dart';
import '../widgets/zz_icon.dart';
import 'zz_qty_stepper.dart';

/// Postęp do minimalnej wartości zamówienia (E3-3): pasek + „Brakuje 5,02 zł do minimum 15,00 zł" albo
/// „Minimum zamówienia osiągnięte". Pasek wydłuża się z animacją [ZzMotion.slow] (0 ms przy „ogranicz ruch").
class ZzMinOrderProgress extends StatelessWidget {
  final double subtotal;
  final double minimum;
  const ZzMinOrderProgress({super.key, required this.subtotal, required this.minimum});

  bool get reached => subtotal >= minimum;

  @override
  Widget build(BuildContext context) {
    final p = context.zz;
    final ratio = minimum <= 0 ? 1.0 : (subtotal / minimum).clamp(0.0, 1.0);
    final text = reached
        ? 'Minimum zamówienia osiągnięte (${zl(minimum)})'
        : 'Brakuje ${zl(minimum - subtotal)} do minimum ${zl(minimum)}';
    final color = reached ? p.success : p.brandStrong;
    return Semantics(
      label: text,
      value: '${(ratio * 100).round()}%',
      excludeSemantics: true,
      child: Container(
        padding: const EdgeInsets.all(ZzSpace.md),
        decoration: BoxDecoration(
          color: reached ? p.successTint : p.brandTint,
          borderRadius: BorderRadius.circular(ZzRadius.field),
        ),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                Icon(reached ? Icons.check_circle : Icons.shopping_basket_outlined, size: 18, color: color),
                const SizedBox(width: ZzSpace.sm),
                Expanded(
                  child: Text(
                    text,
                    style: TextStyle(color: color, fontWeight: FontWeight.w600, fontSize: 13.5),
                  ),
                ),
              ],
            ),
            const SizedBox(height: ZzSpace.sm),
            ClipRRect(
              borderRadius: BorderRadius.circular(ZzRadius.pill),
              child: SizedBox(
                height: 8,
                child: Stack(
                  children: [
                    Positioned.fill(child: ColoredBox(color: p.card)),
                    TweenAnimationBuilder<double>(
                      tween: Tween(end: ratio),
                      duration: ZzMotion.of(context, ZzMotion.slow),
                      curve: ZzMotion.enter,
                      builder: (context, v, _) => FractionallySizedBox(
                        widthFactor: v,
                        heightFactor: 1,
                        child: ColoredBox(color: color),
                      ),
                    ),
                  ],
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }
}

/// Pozycja koszyka: nazwa, „2 × 4,99 zł / kg", wartość, licznik (z wpisaniem ilości) i usuń (pole ≥ 48 px).
class ZzCartItem extends StatelessWidget {
  final String name;
  final int quantity;
  final String unit;
  final double unitPrice;
  final double lineTotal;
  final VoidCallback onMinus;
  final VoidCallback onPlus;
  final VoidCallback onRemove;
  final VoidCallback? onValueTap;

  const ZzCartItem({
    super.key,
    required this.name,
    required this.quantity,
    required this.unit,
    required this.unitPrice,
    required this.lineTotal,
    required this.onMinus,
    required this.onPlus,
    required this.onRemove,
    this.onValueTap,
  });

  @override
  Widget build(BuildContext context) {
    final p = context.zz;
    return Container(
      padding: const EdgeInsets.fromLTRB(ZzSpace.md, ZzSpace.md, ZzSpace.xs, ZzSpace.md),
      decoration: BoxDecoration(
        color: p.card,
        borderRadius: BorderRadius.circular(ZzRadius.card),
        boxShadow: p.shadowSm,
      ),
      // Dwa rzędy (nazwa + wartość, potem licznik + usuń): przy 360 px i tekście 130 % licznik nie konkuruje
      // o szerokość z kwotą (w jednym rzędzie przelewało się o 65 px).
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Padding(
            padding: const EdgeInsets.only(right: ZzSpace.md),
            child: Row(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        name,
                        style: TextStyle(fontWeight: FontWeight.w600, fontSize: 15, color: p.text),
                      ),
                      const SizedBox(height: 2),
                      Text('$quantity × ${zl(unitPrice)} / $unit', style: TextStyle(color: p.textMuted, fontSize: 13)),
                    ],
                  ),
                ),
                const SizedBox(width: ZzSpace.sm),
                Text(
                  zl(lineTotal),
                  style: TextStyle(fontWeight: FontWeight.w700, color: p.text),
                ),
              ],
            ),
          ),
          const SizedBox(height: ZzSpace.sm),
          Row(
            children: [
              ZzQtyStepper(
                itemLabel: name,
                quantity: quantity,
                onMinus: onMinus,
                onPlus: onPlus,
                onValueTap: onValueTap,
              ),
              const Spacer(),
              IconButton(
                tooltip: 'Usuń: $name',
                constraints: const BoxConstraints(minWidth: zzMinTouch, minHeight: zzMinTouch),
                onPressed: onRemove,
                icon: ZzIcon('trash', size: 20, color: p.textMuted),
              ),
            ],
          ),
        ],
      ),
    );
  }
}

/// Wiersz podsumowania: etykieta po lewej, wartość po prawej (pogrubiona dla sumy).
class ZzSummaryRow extends StatelessWidget {
  final String label;
  final String value;
  final bool strong;
  final bool mutedValue;
  const ZzSummaryRow({
    super.key,
    required this.label,
    required this.value,
    this.strong = false,
    this.mutedValue = false,
  });

  @override
  Widget build(BuildContext context) {
    final p = context.zz;
    final style = TextStyle(
      color: strong ? p.text : p.textMuted,
      fontWeight: strong ? FontWeight.w700 : FontWeight.w400,
      fontSize: strong ? 16 : 14,
    );
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 2),
      // Dwa luźne Flexible + spaceBetween: wartość zawsze przy prawej krawędzi, długie teksty zawijają się
      // (Expanded + Flexible dzieliły rząd na pół i wartość wisiała pośrodku).
      child: Row(
        mainAxisAlignment: MainAxisAlignment.spaceBetween,
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Flexible(child: Text(label, style: style)),
          const SizedBox(width: ZzSpace.md),
          Flexible(
            child: Text(
              value,
              textAlign: TextAlign.right,
              style: mutedValue
                  ? TextStyle(color: p.textMuted, fontSize: 13)
                  : style.copyWith(color: p.text, fontWeight: strong ? FontWeight.w700 : FontWeight.w600),
            ),
          ),
        ],
      ),
    );
  }
}
