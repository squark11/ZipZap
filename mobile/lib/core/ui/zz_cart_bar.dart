import 'package:flutter/material.dart';

import '../theme/zz_theme.dart';
import '../util/format.dart';

/// Pływający pasek koszyka: liczba produktów i kwota + przycisk w kolorze akcentu.
/// Gdy do minimum zamówienia brakuje kwoty — pokazuje ją pod sumą.
class ZzCartBar extends StatelessWidget {
  final int count;
  final double subtotal;
  final double? missingToMinimum;
  final VoidCallback onTap;

  const ZzCartBar({super.key, required this.count, required this.subtotal, required this.onTap, this.missingToMinimum});

  static String productsLabel(int n) {
    if (n == 1) return '1 produkt';
    final last = n % 10, lastTwo = n % 100;
    final few = last >= 2 && last <= 4 && (lastTwo < 12 || lastTwo > 14);
    return '$n ${few ? 'produkty' : 'produktów'}';
  }

  @override
  Widget build(BuildContext context) {
    final missing = missingToMinimum;
    return SafeArea(
      minimum: const EdgeInsets.all(ZzSpace.lg),
      child: Semantics(
        button: true,
        label: 'Koszyk: ${productsLabel(count)}, ${zl(subtotal)}',
        excludeSemantics: true,
        child: Material(
          color: ZzColors.graphite,
          shape: const StadiumBorder(),
          elevation: 0,
          shadowColor: Colors.transparent,
          child: InkWell(
            customBorder: const StadiumBorder(),
            onTap: onTap,
            child: Container(
              constraints: const BoxConstraints(minHeight: 60),
              padding: const EdgeInsets.fromLTRB(20, 8, 8, 8),
              decoration: BoxDecoration(
                borderRadius: BorderRadius.circular(ZzRadius.pill),
                boxShadow: context.zz.shadowFloat,
              ),
              child: Row(
                children: [
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      mainAxisSize: MainAxisSize.min,
                      children: [
                        Text(
                          '${productsLabel(count)} · ${zl(subtotal)}',
                          style: const TextStyle(color: Colors.white, fontWeight: FontWeight.w700, fontSize: 15),
                        ),
                        if (missing != null && missing > 0)
                          Text(
                            'Brakuje ${zl(missing)} do minimum',
                            style: const TextStyle(
                              color: ZzColors.onGraphiteMuted,
                              fontSize: 12,
                              fontWeight: FontWeight.w500,
                            ),
                          ),
                      ],
                    ),
                  ),
                  Container(
                    padding: const EdgeInsets.symmetric(horizontal: 18, vertical: 12),
                    decoration: BoxDecoration(
                      color: ZzColors.accent,
                      borderRadius: BorderRadius.circular(ZzRadius.pill),
                    ),
                    // Strzałka jako ikona, nie znak „→" — tego znaku nie ma w Poppins, a brak glifu
                    // kazałby przeglądarce pobrać czcionkę zapasową z Google (check_glyphs.py).
                    child: const Row(
                      mainAxisSize: MainAxisSize.min,
                      children: [
                        Text(
                          'Koszyk',
                          style: TextStyle(color: ZzColors.onAccent, fontWeight: FontWeight.w700, fontSize: 14),
                        ),
                        SizedBox(width: 4),
                        Icon(Icons.arrow_forward, size: 16, color: ZzColors.onAccent),
                      ],
                    ),
                  ),
                ],
              ),
            ),
          ),
        ),
      ),
    );
  }
}
