import 'package:flutter/material.dart';

import '../../core/theme/zz_theme.dart';
import '../../core/ui/ui.dart';
import '../../core/util/format.dart';
import '../../core/widgets/store_logo.dart';
import '../../core/widgets/zz_icon.dart';
import '../../models/store.dart';

/// Nagłówek ekranu sklepu (E3-2): logo, nazwa, miejscowość, status, opis, chipy informacji i termin dostawy.
/// [roundLine] = termin z najbliższej rundy („Zamów jutro do 11:30 · dostawa od jutro, 13:00"); bez niego —
/// ogólna informacja, że termin wybiera się przy zamówieniu.
class StoreHeader extends StatelessWidget {
  final Store store;
  final String? roundLine;
  const StoreHeader({super.key, required this.store, this.roundLine});

  @override
  Widget build(BuildContext context) {
    final p = context.zz;
    return Container(
      padding: const EdgeInsets.all(ZzSpace.lg),
      decoration: BoxDecoration(
        color: p.card,
        borderRadius: BorderRadius.circular(ZzRadius.card),
        boxShadow: p.shadowMd,
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              StoreLogo(logoUrl: store.logoUrl, size: 56),
              const SizedBox(width: 14),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Semantics(header: true, child: Text(store.name, style: Theme.of(context).textTheme.titleLarge)),
                    const SizedBox(height: 4),
                    Wrap(
                      spacing: ZzSpace.sm,
                      runSpacing: ZzSpace.xs,
                      crossAxisAlignment: WrapCrossAlignment.center,
                      children: [
                        ZzStatusChip(open: store.isAcceptingOrders, status: store.status),
                        Row(
                          mainAxisSize: MainAxisSize.min,
                          children: [
                            ZzIcon('location', size: 14, color: p.textMuted),
                            const SizedBox(width: 4),
                            Text(store.city, style: TextStyle(color: p.textMuted, fontSize: 13)),
                          ],
                        ),
                      ],
                    ),
                  ],
                ),
              ),
            ],
          ),
          if ((store.description ?? '').trim().isNotEmpty) ...[
            const SizedBox(height: ZzSpace.md),
            Text(store.description!.trim(), style: TextStyle(color: p.textMuted, height: 1.35)),
          ],
          const SizedBox(height: ZzSpace.md),
          Wrap(
            spacing: ZzSpace.sm,
            runSpacing: ZzSpace.sm,
            children: [
              ZzInfoChip(icon: 'tag', text: 'Min. ${zl(store.minimumOrderValue)}'),
              if ((store.phone ?? '').trim().isNotEmpty) ZzInfoChip(icon: 'phone', text: store.phone!.trim()),
              if ((store.address ?? '').trim().isNotEmpty) ZzInfoChip(icon: 'location', text: store.address!.trim()),
            ],
          ),
          const SizedBox(height: ZzSpace.md),
          ZzNotice(
            kind: ZzNoticeKind.info,
            message: roundLine ?? 'Dostawa — opłatę i termin dostawy wybierzesz przy zamówieniu.',
          ),
        ],
      ),
    );
  }
}
