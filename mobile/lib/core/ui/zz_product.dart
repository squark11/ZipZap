import 'package:flutter/material.dart';

import '../../models/product.dart';
import '../theme/zz_theme.dart';
import '../util/format.dart';
import '../widgets/zz_icon.dart';

/// Cena produktu w jednej linii: „4,99 zł / kg" albo przy kilku jednostkach „4,99 zł/kg · 1,20 zł/szt".
String productPriceLine(Product p) => p.hasMultipleUnits
    ? p.unitOptions.map((o) => '${zl(o.price)}/${o.unit}').join('  ·  ')
    : '${zl(p.price)} / ${p.unit}';

/// Zdjęcie produktu z zapasową ikoną (brak zdjęcia, ładowanie, błąd).
class ZzProductImage extends StatelessWidget {
  final String? url;
  final double? size; // null = wypełnij rodzica
  const ZzProductImage({super.key, required this.url, this.size});

  @override
  Widget build(BuildContext context) {
    final p = context.zz;
    final placeholder = Container(
      width: size,
      height: size,
      color: p.surface,
      alignment: Alignment.center,
      child: ZzIcon('package', size: (size ?? 56) * 0.45, color: p.textMuted),
    );
    final u = url;
    return ClipRRect(
      borderRadius: BorderRadius.circular(ZzRadius.field),
      child: u == null || u.isEmpty
          ? placeholder
          : Image.network(
              u,
              width: size,
              height: size,
              fit: BoxFit.cover,
              excludeFromSemantics: true,
              errorBuilder: (_, _, _) => placeholder,
              loadingBuilder: (context, child, progress) => progress == null ? child : placeholder,
            ),
    );
  }
}

/// Wiersz produktu (lista na telefonie): zdjęcie, nazwa, cena, dostępność i akcja po prawej (np. „Dodaj" / licznik).
class ZzProductRow extends StatelessWidget {
  final Product product;
  final Widget? action;
  const ZzProductRow({super.key, required this.product, this.action});

  @override
  Widget build(BuildContext context) {
    final p = context.zz;
    return Container(
      padding: const EdgeInsets.all(ZzSpace.md),
      decoration: BoxDecoration(
        color: p.card,
        borderRadius: BorderRadius.circular(ZzRadius.card),
        boxShadow: p.shadowSm,
      ),
      child: Row(
        children: [
          ZzProductImage(url: product.imageUrl, size: 56),
          const SizedBox(width: ZzSpace.md),
          Expanded(child: _ProductText(product: product)),
          if (action != null) ...[const SizedBox(width: ZzSpace.sm), action!],
        ],
      ),
    );
  }
}

/// Kafelek produktu (siatka od 600 px): zdjęcie 4:3 na górze, nazwa, cena, akcja na dole.
class ZzProductTile extends StatelessWidget {
  final Product product;
  final Widget? action;
  const ZzProductTile({super.key, required this.product, this.action});

  @override
  Widget build(BuildContext context) {
    final p = context.zz;
    return Container(
      padding: const EdgeInsets.all(ZzSpace.md),
      decoration: BoxDecoration(
        color: p.card,
        borderRadius: BorderRadius.circular(ZzRadius.card),
        boxShadow: p.shadowSm,
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          AspectRatio(aspectRatio: 4 / 3, child: ZzProductImage(url: product.imageUrl)),
          const SizedBox(height: ZzSpace.sm),
          Expanded(child: _ProductText(product: product, maxNameLines: 2)),
          if (action != null) Align(alignment: Alignment.centerRight, child: action!),
        ],
      ),
    );
  }
}

class _ProductText extends StatelessWidget {
  final Product product;
  final int? maxNameLines;
  const _ProductText({required this.product, this.maxNameLines});

  @override
  Widget build(BuildContext context) {
    final p = context.zz;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      mainAxisSize: MainAxisSize.min,
      children: [
        Text(product.name,
            maxLines: maxNameLines,
            overflow: maxNameLines == null ? null : TextOverflow.ellipsis,
            style: TextStyle(fontWeight: FontWeight.w600, fontSize: 15, color: p.text)),
        const SizedBox(height: 2),
        Text(productPriceLine(product), style: TextStyle(color: p.textMuted, fontSize: 13)),
        if (!product.isAvailable)
          Text('Niedostępny', style: TextStyle(color: p.danger, fontSize: 12, fontWeight: FontWeight.w600)),
      ],
    );
  }
}

/// Pigułka filtra (kategorie sklepu): tekst, zaznaczenie wypełnieniem; pole dotyku ≥ 48 px.
class ZzFilterPill extends StatelessWidget {
  final String label;
  final bool selected;
  final VoidCallback onTap;
  const ZzFilterPill({super.key, required this.label, required this.selected, required this.onTap});

  @override
  Widget build(BuildContext context) {
    final p = context.zz;
    return Semantics(
      button: true,
      selected: selected,
      label: label,
      excludeSemantics: true,
      child: InkWell(
        onTap: onTap,
        borderRadius: BorderRadius.circular(ZzRadius.pill),
        child: ConstrainedBox(
          constraints: const BoxConstraints(minHeight: zzMinTouch),
          child: Center(
            widthFactor: 1,
            child: AnimatedContainer(
              duration: ZzMotion.of(context, ZzMotion.fast),
              padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 8),
              decoration: BoxDecoration(
                color: selected ? p.brandStrong : p.card,
                borderRadius: BorderRadius.circular(ZzRadius.pill),
                border: Border.all(color: selected ? p.brandStrong : p.border),
              ),
              child: Text(label,
                  style: TextStyle(
                    color: selected ? p.onBrand : p.text,
                    fontSize: 14,
                    fontWeight: selected ? FontWeight.w700 : FontWeight.w500,
                  )),
            ),
          ),
        ),
      ),
    );
  }
}

/// Chip informacji z ikoną (min. zamówienie, telefon, adres) — obramowana pigułka.
class ZzInfoChip extends StatelessWidget {
  /// Nazwa ikony z `assets/svg/icons/`.
  final String icon;
  final String text;
  const ZzInfoChip({super.key, required this.icon, required this.text});

  @override
  Widget build(BuildContext context) {
    final p = context.zz;
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 6),
      decoration: BoxDecoration(
        color: p.surface,
        borderRadius: BorderRadius.circular(ZzRadius.pill),
        border: Border.all(color: p.border),
      ),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          ZzIcon(icon, size: 15, color: p.textMuted),
          const SizedBox(width: 6),
          Flexible(child: Text(text, style: TextStyle(color: p.text, fontSize: 13))),
        ],
      ),
    );
  }
}
