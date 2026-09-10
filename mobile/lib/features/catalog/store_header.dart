import 'package:flutter/material.dart';

import '../../core/theme/zz_theme.dart';
import '../../core/util/format.dart';
import '../../core/widgets/zz_icon.dart';
import '../../models/store.dart';

/// Nagłówek („hero") ekranu sklepu: nazwa, lokalizacja, status i kluczowe
/// informacje (min. zamówienie, kontakt) + notka o dostawie.
/// Tu wyląduje sposób i harmonogram dostawy po wdrożeniu Fazy H.
class StoreHeader extends StatelessWidget {
  final Store store;
  const StoreHeader({super.key, required this.store});

  @override
  Widget build(BuildContext context) {
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Container(
                  width: 56,
                  height: 56,
                  alignment: Alignment.center,
                  decoration: BoxDecoration(
                    color: context.zz.orangeTint,
                    borderRadius: BorderRadius.circular(ZzRadius.md),
                  ),
                  child: const ZzIcon('store', size: 28, color: ZzColors.orange),
                ),
                const SizedBox(width: 14),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(store.name,
                          style: Theme.of(context).textTheme.titleLarge),
                      const SizedBox(height: 4),
                      Row(
                        children: [
                          ZzIcon('location', size: 14, color: context.zz.textMuted),
                          const SizedBox(width: 4),
                          Flexible(
                            child: Text(store.city,
                                style: TextStyle(color: context.zz.textMuted, fontSize: 13)),
                          ),
                        ],
                      ),
                    ],
                  ),
                ),
                const SizedBox(width: 8),
                _StatusPill(status: store.status, open: store.isAcceptingOrders),
              ],
            ),
            if ((store.description ?? '').trim().isNotEmpty) ...[
              const SizedBox(height: 12),
              Text(store.description!.trim(),
                  style: TextStyle(color: context.zz.textMuted, height: 1.35)),
            ],
            const SizedBox(height: 14),
            Wrap(
              spacing: 8,
              runSpacing: 8,
              children: [
                _InfoChip(icon: 'tag', text: 'Min. ${zl(store.minimumOrderValue)}'),
                if ((store.phone ?? '').trim().isNotEmpty)
                  _InfoChip(icon: 'phone', text: store.phone!.trim()),
                if ((store.address ?? '').trim().isNotEmpty)
                  _InfoChip(icon: 'location', text: store.address!.trim()),
              ],
            ),
            const SizedBox(height: 12),
            Container(
              width: double.infinity,
              padding: const EdgeInsets.all(12),
              decoration: BoxDecoration(
                color: context.zz.orangeTint,
                borderRadius: BorderRadius.circular(ZzRadius.md),
              ),
              child: Row(
                children: [
                  const ZzIcon('delivery', size: 18, color: ZzColors.orange600),
                  const SizedBox(width: 8),
                  const Expanded(
                    child: Text('Dostawa — opłatę i termin dostawy wybierzesz przy zamówieniu.',
                        style: TextStyle(color: ZzColors.orange600, fontSize: 13)),
                  ),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _StatusPill extends StatelessWidget {
  final String status;
  final bool open;
  const _StatusPill({required this.status, required this.open});

  @override
  Widget build(BuildContext context) {
    final label = open
        ? 'Otwarte'
        : (status == 'TemporarilyUnavailable' ? 'Niedostępne' : 'Zamknięte');
    final bg = open ? ZzColors.green50 : const Color(0xFFFEECEC);
    final fg = open ? const Color(0xFF128040) : ZzColors.danger;
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
      decoration: BoxDecoration(color: bg, borderRadius: BorderRadius.circular(ZzRadius.sm)),
      child: Text(label,
          style: TextStyle(color: fg, fontSize: 12, fontWeight: FontWeight.w600)),
    );
  }
}

class _InfoChip extends StatelessWidget {
  final String icon;
  final String text;
  const _InfoChip({required this.icon, required this.text});

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 6),
      decoration: BoxDecoration(
        color: context.zz.surface,
        borderRadius: BorderRadius.circular(ZzRadius.sm),
        border: Border.all(color: context.zz.border),
      ),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          ZzIcon(icon, size: 15, color: context.zz.textMuted),
          const SizedBox(width: 6),
          Text(text, style: TextStyle(color: context.zz.text, fontSize: 13)),
        ],
      ),
    );
  }
}
