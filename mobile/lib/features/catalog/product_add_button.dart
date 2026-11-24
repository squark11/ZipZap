import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/theme/zz_theme.dart';
import '../../core/ui/ui.dart';
import '../../core/util/format.dart';
import '../../models/product.dart';
import '../cart/cart_controller.dart';

/// Akcja produktu na ekranie sklepu: „Dodaj" → licznik ilości. Zmiana jest animowana (skala + przenikanie,
/// [ZzMotion.base]); przy „ogranicz ruch" bez animacji. Logika koszyka bez zmian względem Etapu 2: koszyk powstaje
/// dopiero przy pierwszym dodaniu, inny sklep w koszyku → pytanie o opróżnienie, kilka jednostek → wybór jednostki.
class ProductAddButton extends ConsumerWidget {
  final Product product;
  final String storeId;
  final String storeName;
  final bool canOrder;
  const ProductAddButton({
    super.key,
    required this.product,
    required this.storeId,
    required this.storeName,
    this.canOrder = true,
  });

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    if (!product.isAvailable) return const SizedBox.shrink();
    final qty = ref.watch(cartControllerProvider).cart == null
        ? 0
        : ref.read(cartControllerProvider.notifier).quantityOf(product.id);
    final controller = ref.read(cartControllerProvider.notifier);
    final child = qty == 0
        ? OutlinedButton.icon(
            key: const ValueKey('add'),
            onPressed: canOrder ? () => _add(context, ref) : null,
            style: OutlinedButton.styleFrom(
              minimumSize: const Size(zzMinTouch, zzMinTouch),
              padding: const EdgeInsets.symmetric(horizontal: 12),
            ),
            icon: const Icon(Icons.add, size: 18),
            label: const Text('Dodaj'),
          )
        : ZzQtyStepper(
            key: const ValueKey('stepper'),
            itemLabel: product.name,
            quantity: qty,
            onMinus: () => controller.setQuantity(product.id, qty - 1),
            onPlus: () => controller.setQuantity(product.id, qty + 1),
          );
    return AnimatedSwitcher(
      duration: ZzMotion.of(context, ZzMotion.base),
      switchInCurve: ZzMotion.enter,
      switchOutCurve: ZzMotion.exit,
      transitionBuilder: (c, anim) => FadeTransition(
        opacity: anim,
        child: ScaleTransition(scale: Tween(begin: 0.85, end: 1.0).animate(anim), child: c),
      ),
      layoutBuilder: (current, previous) => Stack(
        alignment: Alignment.centerRight,
        children: [...previous, ?current],
      ),
      child: child,
    );
  }

  /// Dodaje produkt do koszyka. Jeśli koszyk zawiera produkty z innego sklepu,
  /// prosi o potwierdzenie opróżnienia (koszyk jest jednosklepowy).
  Future<void> _add(BuildContext context, WidgetRef ref) async {
    final controller = ref.read(cartControllerProvider.notifier);
    if (controller.hasItemsFromOtherStore(storeId)) {
      final ok = await showDialog<bool>(
        context: context,
        builder: (ctx) => AlertDialog(
          title: const Text('Zacząć nowy koszyk?'),
          content: Text(
              'Twój koszyk zawiera produkty z innego sklepu. Dodanie „${product.name}" '
              'opróżni obecny koszyk i zacznie zakupy w „$storeName".'),
          actions: [
            TextButton(onPressed: () => Navigator.pop(ctx, false), child: const Text('Anuluj')),
            ElevatedButton(onPressed: () => Navigator.pop(ctx, true), child: const Text('Opróżnij i dodaj')),
          ],
        ),
      );
      if (ok != true) return;
    }

    // Produkt z kilkoma jednostkami — klient wybiera którą kupuje.
    String? unit;
    if (product.hasMultipleUnits) {
      if (!context.mounted) return;
      unit = await _pickUnit(context);
      if (unit == null) return; // anulowano
    }
    await controller.addFromStore(storeId, product.id, unit: unit);
  }

  /// Arkusz z wyborem jednostki (np. „1 kg — 4,99 zł" / „1 szt — 1,20 zł").
  Future<String?> _pickUnit(BuildContext context) => showModalBottomSheet<String>(
        context: context,
        builder: (ctx) => SafeArea(
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              Padding(
                padding: const EdgeInsets.fromLTRB(20, 18, 20, 6),
                child: Text('Wybierz jednostkę — ${product.name}',
                    style: const TextStyle(fontWeight: FontWeight.w700, fontSize: 16)),
              ),
              ...product.unitOptions.map((o) => ListTile(
                    title: Text('1 ${o.unit}'),
                    trailing: Text(zl(o.price),
                        style: TextStyle(fontWeight: FontWeight.w700, color: context.zz.brandStrong)),
                    onTap: () => Navigator.pop(ctx, o.unit),
                  )),
              const SizedBox(height: 8),
            ],
          ),
        ),
      );
}
