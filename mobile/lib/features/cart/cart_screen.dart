import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/navigation/last_store.dart';
import '../../core/theme/zz_theme.dart';
import '../../core/ui/ui.dart';
import '../../core/util/format.dart';
import '../../core/widgets/states.dart';
import '../../models/cart.dart';
import '../../models/purchasing_round.dart';
import '../../models/store.dart';
import '../catalog/store_detail_screen.dart';
import '../home/home_providers.dart';
import 'cart_controller.dart';

/// Koszyk (E3-3): sklep, pozycje, termin najbliższej rundy, pasek postępu do minimum zamówienia i podsumowanie.
/// `cart_controller` bez zmian — ekran tylko inaczej pokazuje te same dane i wywołuje te same akcje.
class CartScreen extends ConsumerWidget {
  const CartScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final cart = ref.watch(cartControllerProvider);
    final controller = ref.read(cartControllerProvider.notifier);
    final c = cart.cart;

    if (c == null && cart.restoring) {
      return Scaffold(
        appBar: AppBar(title: const Text('Koszyk')),
        body: const LoadingView(label: 'Wczytywanie koszyka…'),
      );
    }
    if (c == null || c.items.isEmpty) {
      final last = ref.watch(lastStoreProvider);
      return Scaffold(
        appBar: AppBar(title: const Text('Koszyk')),
        body: EmptyView(
          svgAsset: 'assets/svg/illustrations/state_empty_cart.svg',
          icon: Icons.shopping_cart_outlined,
          title: 'Koszyk jest pusty',
          subtitle: 'Dodaj produkty ze sklepu, aby złożyć zamówienie.',
          action: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              if (last != null) ...[
                FilledButton(
                  onPressed: () => context.push(last.path),
                  child: Text('Wróć do: ${last.name}'),
                ),
                const SizedBox(height: ZzSpace.sm),
              ],
              OutlinedButton(onPressed: () => context.go('/stores'), child: const Text('Przeglądaj sklepy')),
            ],
          ),
        ),
      );
    }

    final store = ref.watch(storeDetailProvider(c.storeId)).valueOrNull;
    final round = ref.watch(storeRoundProvider(c.storeId)).valueOrNull;
    return Scaffold(
      appBar: AppBar(title: const Text('Twój koszyk')),
      body: Column(
        children: [
          Expanded(
            child: ListView(
              padding: const EdgeInsets.all(ZzSpace.screen),
              children: [
                if (store != null) ...[
                  _storeLine(context, store),
                  const SizedBox(height: ZzSpace.md),
                ],
                if (cart.error != null) ...[
                  ZzNotice(kind: ZzNoticeKind.danger, message: cart.error!),
                  const SizedBox(height: ZzSpace.md),
                ],
                for (final it in c.items) ...[
                  ZzCartItem(
                    name: it.productName,
                    quantity: it.quantity,
                    unit: it.unit,
                    unitPrice: it.unitPrice,
                    lineTotal: it.lineTotal,
                    onMinus: () => controller.setQuantity(it.productId, it.quantity - 1),
                    onPlus: () => controller.setQuantity(it.productId, it.quantity + 1),
                    onRemove: () => controller.remove(it.productId),
                    onValueTap: () =>
                        _editQuantity(context, it.quantity, (q) => controller.setQuantity(it.productId, q)),
                  ),
                  const SizedBox(height: ZzSpace.sm),
                ],
                if (round != null && round.available && round.round != null) ...[
                  const SizedBox(height: ZzSpace.sm),
                  ZzNotice(kind: ZzNoticeKind.info, title: 'Najbliższa runda zakupów', message: roundText(round)),
                ],
              ],
            ),
          ),
          _footer(context, c, store),
        ],
      ),
    );
  }

  /// Sklep, z którego jest koszyk, i powrót do jego oferty.
  Widget _storeLine(BuildContext context, Store store) => Row(
        children: [
          Expanded(
            child: Text(store.name,
                style: TextStyle(color: context.zz.textMuted, fontWeight: FontWeight.w600)),
          ),
          TextButton.icon(
            onPressed: () => context.push('/stores/${store.id}'),
            icon: const Icon(Icons.add, size: 18),
            label: const Text('Dodaj więcej'),
          ),
        ],
      );

  /// Podsumowanie i przejście dalej. Minimum zamówienia z danych sklepu; dopóki nie jest znane — 0 (jak dotąd).
  Widget _footer(BuildContext context, Cart c, Store? store) {
    final p = context.zz;
    final minOrder = store?.minimumOrderValue ?? 0;
    final belowMin = c.subtotal < minOrder;
    return Container(
      decoration: BoxDecoration(
        color: p.card,
        boxShadow: p.shadowMd,
        borderRadius: const BorderRadius.vertical(top: Radius.circular(ZzRadius.card)),
      ),
      child: SafeArea(
        minimum: const EdgeInsets.all(ZzSpace.screen),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            if (minOrder > 0) ...[
              ZzMinOrderProgress(subtotal: c.subtotal, minimum: minOrder),
              const SizedBox(height: ZzSpace.md),
            ],
            // Bez osobnego wiersza „Produkty" — przed wyborem adresu ma tę samą kwotę co suma (niższa stopka na 375 px).
            const ZzSummaryRow(label: 'Dostawa', value: 'po wyborze adresu', mutedValue: true),
            ZzSummaryRow(label: 'Razem bez dostawy', value: zl(c.subtotal), strong: true),
            const SizedBox(height: ZzSpace.md),
            ElevatedButton(
              onPressed: belowMin ? null : () => context.push('/checkout'),
              child: const Text('Przejdź do dostawy i płatności'),
            ),
          ],
        ),
      ),
    );
  }
}

/// „Zakupy w sklepie: Dziś, 16:00. Zamów do 15:30. Godzinę dostawy wybierzesz w następnym kroku." — tylko dane
/// z serwera (runda, termin graniczny), bez obietnic godziny dostawy.
String roundText(RoundPreview p) {
  final r = p.round!;
  return 'Zakupy w sklepie: ${r.label}. ${cutoffPhrase(r)}. Godzinę dostawy wybierzesz w następnym kroku.';
}

/// Wpisanie ilości z klawiatury (dotknięcie liczby w liczniku).
Future<void> _editQuantity(BuildContext context, int quantity, ValueChanged<int> onSet) async {
  final ctrl = TextEditingController(text: '$quantity');
  ctrl.selection = TextSelection(baseOffset: 0, extentOffset: ctrl.text.length);
  final result = await showDialog<int>(
    context: context,
    builder: (ctx) => AlertDialog(
      title: const Text('Podaj ilość'),
      content: TextField(
        controller: ctrl,
        autofocus: true,
        keyboardType: TextInputType.number,
        inputFormatters: [FilteringTextInputFormatter.digitsOnly],
        decoration: const InputDecoration(hintText: 'np. 3', counterText: ''),
        maxLength: 4,
        onSubmitted: (v) => Navigator.pop(ctx, int.tryParse(v)),
      ),
      actions: [
        TextButton(onPressed: () => Navigator.pop(ctx), child: const Text('Anuluj')),
        FilledButton(
          onPressed: () => Navigator.pop(ctx, int.tryParse(ctrl.text)),
          child: const Text('OK'),
        ),
      ],
    ),
  );
  if (result != null && result > 0) onSet(result);
}
