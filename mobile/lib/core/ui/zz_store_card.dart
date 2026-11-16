import 'package:flutter/material.dart';

import '../../models/store.dart';
import '../theme/zz_theme.dart';
import '../util/format.dart';
import '../widgets/store_logo.dart';

/// Karta sklepu (wariant kompaktowy z logo): nazwa, status, miasto · odległość · minimum zamówienia.
/// Okładka sklepu (decyzja F-5) dojdzie w Etapie 3 — wtedy ten komponent dostanie wariant z okładką.
class ZzStoreCard extends StatelessWidget {
  final Store store;
  final VoidCallback onTap;
  const ZzStoreCard({super.key, required this.store, required this.onTap});

  @override
  Widget build(BuildContext context) {
    final p = context.zz;
    final meta = [store.city, if (store.distanceLabel != null) store.distanceLabel!, 'min. ${zl(store.minimumOrderValue)}'];
    return Semantics(
      button: true,
      label: '${store.name}, ${store.isAcceptingOrders ? 'otwarte' : 'zamknięte'}, ${meta.join(', ')}',
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
              child: Row(
                children: [
                  StoreLogo(logoUrl: store.logoUrl, size: 52),
                  const SizedBox(width: 14),
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(store.name, style: Theme.of(context).textTheme.titleMedium),
                        const SizedBox(height: 2),
                        Text(meta.join(' · '), style: Theme.of(context).textTheme.bodySmall),
                      ],
                    ),
                  ),
                  const SizedBox(width: ZzSpace.sm),
                  ZzStatusChip(open: store.isAcceptingOrders),
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
