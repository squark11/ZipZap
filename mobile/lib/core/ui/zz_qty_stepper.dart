import 'package:flutter/material.dart';

import '../theme/zz_theme.dart';

/// Licznik ilości `− n +` (pigułka na tle marki jasnej). Pola dotyku 48 × 48 px, etykiety dla czytnika ekranu,
/// cyfra zmienia się z krótkim przenikaniem (wyłączonym przy „ogranicz ruch").
class ZzQtyStepper extends StatelessWidget {
  final int quantity;
  final VoidCallback? onMinus;
  final VoidCallback? onPlus;

  /// Nazwa produktu do etykiet dostępności („Mniej: Jabłka").
  final String itemLabel;

  /// Tekst ilości (np. „2 kg"); domyślnie sama liczba.
  final String? valueText;

  const ZzQtyStepper({
    super.key,
    required this.quantity,
    required this.onMinus,
    required this.onPlus,
    required this.itemLabel,
    this.valueText,
  });

  @override
  Widget build(BuildContext context) {
    final p = context.zz;
    Widget btn(IconData icon, VoidCallback? onTap, String label) => Semantics(
          button: true,
          label: label,
          excludeSemantics: true,
          child: InkResponse(
            onTap: onTap,
            radius: 24,
            child: SizedBox(
              width: zzMinTouch,
              height: zzMinTouch,
              child: Icon(icon, size: 20, color: onTap == null ? p.textMuted : p.brandStrong),
            ),
          ),
        );
    return Container(
      decoration: BoxDecoration(color: p.brandTint, borderRadius: BorderRadius.circular(ZzRadius.pill)),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          btn(Icons.remove, onMinus, 'Mniej: $itemLabel'),
          ConstrainedBox(
            constraints: const BoxConstraints(minWidth: 24),
            child: AnimatedSwitcher(
              duration: ZzMotion.of(context, ZzMotion.fast),
              child: Text(
                valueText ?? '$quantity',
                key: ValueKey(valueText ?? quantity),
                textAlign: TextAlign.center,
                semanticsLabel: 'Ilość: ${valueText ?? quantity}',
                style: TextStyle(fontWeight: FontWeight.w700, fontSize: 15, color: p.text),
              ),
            ),
          ),
          btn(Icons.add, onPlus, 'Więcej: $itemLabel'),
        ],
      ),
    );
  }
}
