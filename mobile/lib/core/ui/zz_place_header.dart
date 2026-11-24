import 'package:flutter/material.dart';

import '../theme/zz_theme.dart';
import '../widgets/zz_icon.dart';

/// Nagłówek „Dostawa do" z wybranym miejscem i strzałką — jedno dotknięcie otwiera wybór kodu pocztowego
/// albo zapisanego adresu (E3-1). Zastępuje dwa banery kodu pocztowego i lokalizacji.
class ZzPlaceHeader extends StatelessWidget {
  /// Np. „Dom · 31-042" albo „Wybierz kod pocztowy".
  final String place;

  /// Dodatkowa informacja pod miejscem, np. „najbliższe na górze" (sortowanie wg lokalizacji).
  final String? note;
  final VoidCallback onTap;
  const ZzPlaceHeader({super.key, required this.place, required this.onTap, this.note});

  @override
  Widget build(BuildContext context) {
    final p = context.zz;
    return Semantics(
      button: true,
      label: 'Dostawa do: $place${note == null ? '' : ', $note'}. Zmień miejsce dostawy',
      excludeSemantics: true,
      child: InkWell(
        onTap: onTap,
        borderRadius: BorderRadius.circular(ZzRadius.field),
        child: ConstrainedBox(
          constraints: const BoxConstraints(minHeight: zzMinTouch),
          child: Padding(
            padding: const EdgeInsets.symmetric(vertical: ZzSpace.xs),
            child: Row(
              children: [
                Container(
                  width: 40,
                  height: 40,
                  decoration: BoxDecoration(color: p.brandTint, shape: BoxShape.circle),
                  alignment: Alignment.center,
                  child: ZzIcon('location', size: 20, color: p.brandStrong),
                ),
                const SizedBox(width: ZzSpace.md),
                Flexible(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    mainAxisSize: MainAxisSize.min,
                    children: [
                      Text('Dostawa do', style: TextStyle(color: p.textMuted, fontSize: 12)),
                      Row(
                        mainAxisSize: MainAxisSize.min,
                        children: [
                          Flexible(
                            child: Text(place,
                                maxLines: 1,
                                overflow: TextOverflow.ellipsis,
                                style: TextStyle(color: p.heading, fontSize: 15, fontWeight: FontWeight.w700)),
                          ),
                          Icon(Icons.keyboard_arrow_down, size: 20, color: p.brandStrong),
                        ],
                      ),
                      if (note != null)
                        Text(note!, style: TextStyle(color: p.textMuted, fontSize: 12)),
                    ],
                  ),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}
