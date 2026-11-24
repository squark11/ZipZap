import 'package:flutter/material.dart';

import '../../models/store.dart';
import '../theme/zz_theme.dart';
import '../util/format.dart';
import '../widgets/store_logo.dart';

/// Karta sklepu: nazwa, status, miasto · odległość · minimum zamówienia, sposób dostawy z panelu (F-5) i — gdy
/// znana — linia terminu z najbliższej rundy („Zamów do 11:30 · dostawa od dziś, 17:00").
/// Z okładką (F-5): zdjęcie 16:9 nad treścią; bez okładki albo gdy zdjęcie się nie wczyta — wariant z samym logo.
class ZzStoreCard extends StatelessWidget {
  final Store store;
  final VoidCallback onTap;

  /// Termin z najbliższej rundy zakupowej (dane z API); null = nie pokazuj.
  final String? roundLine;
  const ZzStoreCard({super.key, required this.store, required this.onTap, this.roundLine});

  @override
  Widget build(BuildContext context) {
    final p = context.zz;
    final meta = [
      store.city,
      if (store.distanceLabel != null) store.distanceLabel!,
      'min. ${zl(store.minimumOrderValue)}',
    ];
    return Semantics(
      button: true,
      label:
          '${store.name}, ${store.isAcceptingOrders ? 'otwarte' : 'zamknięte'}, ${meta.join(', ')}'
          '${store.deliveryNote == null ? '' : ', ${store.deliveryNote}'}'
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
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                if (store.coverUrl != null) _Cover(url: store.coverUrl!),
                Padding(
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
                            Text(
                              store.name,
                              maxLines: 2,
                              overflow: TextOverflow.ellipsis,
                              style: Theme.of(context).textTheme.titleMedium,
                            ),
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
                            if (store.deliveryNote != null) ...[
                              const SizedBox(height: ZzSpace.xs),
                              _InfoLine(
                                icon: Icons.local_shipping_outlined,
                                text: store.deliveryNote!,
                                color: p.textMuted,
                              ),
                            ],
                            if (roundLine != null) ...[
                              const SizedBox(height: ZzSpace.xs),
                              _InfoLine(
                                icon: Icons.schedule,
                                text: roundLine!,
                                color: p.brandStrong,
                                weight: FontWeight.w600,
                              ),
                            ],
                          ],
                        ),
                      ),
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

/// Okładka nad treścią karty: 16:9, najwyżej [maxHeight] — w szerokiej kolumnie (komputer) zdjęcie nie robi karty
/// dwa razy wyższej od sąsiedniej. Nie wczytała się → znika (zostaje wariant z logo), bez pustej ramki.
class _Cover extends StatelessWidget {
  final String url;
  const _Cover({required this.url});

  static const maxHeight = 140.0;

  @override
  Widget build(BuildContext context) => LayoutBuilder(
        builder: (context, c) => ClipRRect(
          key: const ValueKey('store-cover'),
          borderRadius: const BorderRadius.vertical(top: Radius.circular(ZzRadius.card)),
          child: Image.network(
            url,
            width: c.maxWidth,
            height: (c.maxWidth * 9 / 16).clamp(0, maxHeight).toDouble(),
            fit: BoxFit.cover,
            excludeFromSemantics: true,
            errorBuilder: (_, _, _) => const SizedBox.shrink(),
            frameBuilder: (context, child, frame, sync) => frame == null && !sync
                ? SizedBox(
                    width: c.maxWidth,
                    height: (c.maxWidth * 9 / 16).clamp(0, maxHeight).toDouble(),
                    child: ColoredBox(color: context.zz.skeleton),
                  )
                : child,
          ),
        ),
      );
}

/// Linia informacji z ikoną (sposób dostawy, termin rundy).
class _InfoLine extends StatelessWidget {
  final IconData icon;
  final String text;
  final Color color;
  final FontWeight weight;
  const _InfoLine({required this.icon, required this.text, required this.color, this.weight = FontWeight.w500});

  @override
  Widget build(BuildContext context) => Row(
    crossAxisAlignment: CrossAxisAlignment.start,
    children: [
      Padding(
        padding: const EdgeInsets.only(top: 1),
        child: Icon(icon, size: 14, color: color),
      ),
      const SizedBox(width: ZzSpace.xs),
      Expanded(
        child: Text(
          text,
          style: TextStyle(color: color, fontSize: 12, fontWeight: weight),
        ),
      ),
    ],
  );
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
      child: Text(
        open ? 'Otwarte' : 'Zamknięte',
        style: TextStyle(color: open ? p.success : p.danger, fontSize: 12, fontWeight: FontWeight.w600),
      ),
    );
  }
}
