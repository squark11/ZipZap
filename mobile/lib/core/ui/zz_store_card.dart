import 'package:flutter/material.dart';

import '../../models/store.dart';
import '../theme/zz_theme.dart';
import '../util/format.dart';
import '../widgets/store_logo.dart';

/// Karta sklepu (wariant z logo): nazwa, status, miasto · odległość · minimum zamówienia i — gdy znana —
/// linia terminu z najbliższej rundy („Zamów do 11:30 · dostawa od dziś, 17:00").
/// Okładka sklepu (decyzja F-5) dojdzie razem z polem w API — wtedy ten komponent dostanie wariant z okładką.
class ZzStoreCard extends StatelessWidget {
  final Store store;
  final VoidCallback onTap;

  /// Termin z najbliższej rundy zakupowej (dane z API); null = nie pokazuj.
  final String? roundLine;
  const ZzStoreCard({super.key, required this.store, required this.onTap, this.roundLine});

  @override
  Widget build(BuildContext context) {
    final p = context.zz;
    final meta = [store.city, if (store.distanceLabel != null) store.distanceLabel!, 'min. ${zl(store.minimumOrderValue)}'];
    return Semantics(
      button: true,
      label: '${store.name}, ${store.isAcceptingOrders ? 'otwarte' : 'zamknięte'}, ${meta.join(', ')}'
          '${roundLine == null ? '' : ', $roundLine'}',
      excludeSemantics: true,
      child: Container(
        decoration: BoxDecoration(
          color: p.card,
          borderRadius: BorderRadius.circular(ZzRadius.card),
          boxShadow: p.shadowMd,
        ),
        child: Material(
          type: MaterialType.transparency,
          child: InkWell(
            borderRadius: BorderRadius.circular(ZzRadius.card),
            onTap: onTap,
            child: Padding(
              padding: const EdgeInsets.all(ZzSpace.lg),
              // Status pod nazwą, nie z prawej — przy 360 px nazwa i terminy mają całą szerokość (mniej łamania).
              child: Row(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  StoreLogo(logoUrl: store.logoUrl, size: 52),
                  const SizedBox(width: 14),
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(store.name,
                            maxLines: 2, overflow: TextOverflow.ellipsis, style: Theme.of(context).textTheme.titleMedium),
                        const SizedBox(height: ZzSpace.xs),
                        Wrap(
                          spacing: ZzSpace.sm,
                          runSpacing: ZzSpace.xs,
                          crossAxisAlignment: WrapCrossAlignment.center,
                          children: [
                            ZzStatusChip(open: store.isAcceptingOrders),
                            Text(meta.join(' · '), style: Theme.of(context).textTheme.bodySmall),
                          ],
                        ),
                        if (roundLine != null) ...[
                          const SizedBox(height: ZzSpace.xs),
                          Row(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              Padding(
                                padding: const EdgeInsets.only(top: 1),
                                child: Icon(Icons.schedule, size: 14, color: p.brandStrong),
                              ),
                              const SizedBox(width: ZzSpace.xs),
                              Expanded(
                                child: Text(roundLine!,
                                    style: TextStyle(
                                        color: p.brandStrong, fontSize: 12, fontWeight: FontWeight.w600)),
                              ),
                            ],
                          ),
                        ],
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

/// Pigułka „Otwarte / Zamknięte".
class ZzStatusChip extends StatelessWidget {
  final bool open;
  const ZzStatusChip({super.key, required this.open});

  @override
  Widget build(BuildContext context) {
    final p = context.zz;
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
      decoration: BoxDecoration(
        color: open ? p.successTint : p.dangerTint,
        borderRadius: BorderRadius.circular(ZzRadius.pill),
      ),
      child: Text(open ? 'Otwarte' : 'Zamknięte',
          style: TextStyle(color: open ? p.success : p.danger, fontSize: 12, fontWeight: FontWeight.w600)),
    );
  }
}
