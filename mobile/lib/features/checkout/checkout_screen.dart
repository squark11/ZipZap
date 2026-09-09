import 'dart:math';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/api/api_exception.dart';
import '../../core/providers.dart';
import '../../core/theme/zz_theme.dart';
import '../../core/util/format.dart';
import '../../core/widgets/states.dart';
import '../../models/delivery.dart';
import '../cart/cart_controller.dart';

final zonesProvider = FutureProvider.autoDispose.family<List<DeliveryZone>, String>(
    (ref, storeId) => ref.read(orderingRepositoryProvider).listZones(storeId));

final slotsProvider =
    FutureProvider.autoDispose.family<List<TimeSlot>, ({String storeId, String zoneId})>(
        (ref, k) =>
            ref.read(orderingRepositoryProvider).listSlots(k.storeId, zoneId: k.zoneId));

class CheckoutScreen extends ConsumerStatefulWidget {
  const CheckoutScreen({super.key});

  @override
  ConsumerState<CheckoutScreen> createState() => _CheckoutScreenState();
}

class _CheckoutScreenState extends ConsumerState<CheckoutScreen> {
  final _address = TextEditingController();
  final _phone = TextEditingController();
  String? _zoneId;
  String? _slotId;
  bool _submitting = false;
  late final String _idempotencyKey;

  @override
  void initState() {
    super.initState();
    _idempotencyKey =
        '${DateTime.now().microsecondsSinceEpoch}-${Random().nextInt(1 << 30)}';
  }

  @override
  void dispose() {
    _address.dispose();
    _phone.dispose();
    super.dispose();
  }

  Future<void> _submit(double fee) async {
    final cart = ref.read(cartControllerProvider).cart;
    if (cart == null) return;
    if (_address.text.trim().isEmpty || _phone.text.trim().isEmpty) {
      _snack('Podaj adres i telefon kontaktowy.');
      return;
    }
    if (_zoneId == null || _slotId == null) {
      _snack('Wybierz strefę i termin dostawy.');
      return;
    }
    setState(() => _submitting = true);
    try {
      final order = await ref.read(orderingRepositoryProvider).checkout(
            cartId: cart.id,
            cartToken: cart.cartToken,
            deliveryZoneId: _zoneId!,
            timeSlotId: _slotId!,
            deliveryAddress: _address.text.trim(),
            contactPhone: _phone.text.trim(),
            idempotencyKey: _idempotencyKey,
          );
      ref.read(cartControllerProvider.notifier).clearAfterCheckout();
      if (mounted) context.go('/pay/${order.id}');
    } on ApiException catch (e) {
      _snack(e.message);
    } finally {
      if (mounted) setState(() => _submitting = false);
    }
  }

  void _snack(String m) =>
      ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(m)));

  @override
  Widget build(BuildContext context) {
    final cart = ref.watch(cartControllerProvider);
    final c = cart.cart;
    if (c == null || c.items.isEmpty) {
      return Scaffold(
        appBar: AppBar(title: const Text('Dostawa i płatność')),
        body: const EmptyView(
          icon: Icons.shopping_cart_outlined,
          title: 'Koszyk jest pusty',
        ),
      );
    }

    final zones = ref.watch(zonesProvider(c.storeId));

    return Scaffold(
      appBar: AppBar(title: const Text('Dostawa i płatność')),
      body: zones.when(
        loading: () => const LoadingView(),
        error: (e, _) => ErrorView(
          message: e.toString(),
          onRetry: () => ref.invalidate(zonesProvider(c.storeId)),
        ),
        data: (zoneList) {
          double fee = 0;
          for (final z in zoneList) {
            if (z.id == _zoneId) {
              fee = z.deliveryFee;
              break;
            }
          }
          return ListView(
            padding: const EdgeInsets.all(16),
            children: [
              const _Label('Adres dostawy'),
              TextField(
                controller: _address,
                decoration: const InputDecoration(hintText: 'ul. Przykładowa 12/3'),
              ),
              const SizedBox(height: 16),
              const _Label('Telefon kontaktowy'),
              TextField(
                controller: _phone,
                keyboardType: TextInputType.phone,
                decoration: const InputDecoration(hintText: '600 100 200'),
              ),
              const SizedBox(height: 16),
              const _Label('Strefa dostawy'),
              _ZoneDropdown(
                zones: zoneList,
                value: _zoneId,
                onChanged: (v) => setState(() {
                  _zoneId = v;
                  _slotId = null;
                }),
              ),
              const SizedBox(height: 16),
              if (_zoneId != null) ...[
                const _Label('Termin dostawy'),
                _SlotPicker(
                  storeId: c.storeId,
                  zoneId: _zoneId!,
                  value: _slotId,
                  onChanged: (v) => setState(() => _slotId = v),
                ),
                const SizedBox(height: 16),
              ],
              const Divider(height: 24),
              _SummaryRow('Wartość produktów', zl(cart.subtotal)),
              _SummaryRow('Opłata za dostawę', zl(fee)),
              const SizedBox(height: 4),
              _SummaryRow('Razem', zl(cart.subtotal + fee), bold: true),
              const SizedBox(height: 20),
              ElevatedButton(
                onPressed: _submitting ? null : () => _submit(fee),
                child: _submitting
                    ? const SizedBox(
                        height: 22,
                        width: 22,
                        child: CircularProgressIndicator(strokeWidth: 2, color: Colors.white))
                    : const Text('Złóż zamówienie i przejdź do płatności'),
              ),
              const SizedBox(height: 8),
              const Text(
                'Płatność potwierdza dostawca — status zaktualizuje się automatycznie.',
                textAlign: TextAlign.center,
                style: TextStyle(color: ZzColors.textMuted, fontSize: 12),
              ),
            ],
          );
        },
      ),
    );
  }
}

class _ZoneDropdown extends StatelessWidget {
  final List<DeliveryZone> zones;
  final String? value;
  final ValueChanged<String?> onChanged;
  const _ZoneDropdown({required this.zones, required this.value, required this.onChanged});

  @override
  Widget build(BuildContext context) {
    return DropdownButtonFormField<String>(
      initialValue: value,
      isExpanded: true,
      hint: const Text('Wybierz strefę'),
      items: zones
          .map((z) => DropdownMenuItem(
                value: z.id,
                child: Text('${z.name} · dostawa ${zl(z.deliveryFee)}'),
              ))
          .toList(),
      onChanged: onChanged,
    );
  }
}

class _SlotPicker extends ConsumerWidget {
  final String storeId;
  final String zoneId;
  final String? value;
  final ValueChanged<String?> onChanged;
  const _SlotPicker(
      {required this.storeId,
      required this.zoneId,
      required this.value,
      required this.onChanged});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final slots = ref.watch(slotsProvider((storeId: storeId, zoneId: zoneId)));
    return slots.when(
      loading: () => const Padding(
        padding: EdgeInsets.symmetric(vertical: 8),
        child: LinearProgressIndicator(color: ZzColors.orange),
      ),
      error: (e, _) => Text(e.toString(), style: const TextStyle(color: ZzColors.danger)),
      data: (list) {
        if (list.isEmpty) {
          return const Text('Brak dostępnych terminów w tej strefie.',
              style: TextStyle(color: ZzColors.textMuted));
        }
        return DropdownButtonFormField<String>(
          initialValue: value,
          isExpanded: true,
          hint: const Text('Wybierz termin'),
          items: list
              .map((s) => DropdownMenuItem(
                    value: s.id,
                    child: Text('${s.label}  (wolne: ${s.remainingCapacity})'),
                  ))
              .toList(),
          onChanged: onChanged,
        );
      },
    );
  }
}

class _Label extends StatelessWidget {
  final String text;
  const _Label(this.text);
  @override
  Widget build(BuildContext context) => Padding(
        padding: const EdgeInsets.only(bottom: 6),
        child: Text(text, style: const TextStyle(fontWeight: FontWeight.w600)),
      );
}

class _SummaryRow extends StatelessWidget {
  final String label;
  final String value;
  final bool bold;
  const _SummaryRow(this.label, this.value, {this.bold = false});
  @override
  Widget build(BuildContext context) => Padding(
        padding: const EdgeInsets.symmetric(vertical: 3),
        child: Row(
          mainAxisAlignment: MainAxisAlignment.spaceBetween,
          children: [
            Text(label,
                style: TextStyle(
                    color: bold ? ZzColors.text : ZzColors.textMuted,
                    fontWeight: bold ? FontWeight.w700 : FontWeight.w400,
                    fontSize: bold ? 17 : 14)),
            Text(value,
                style: TextStyle(
                    fontWeight: FontWeight.w700, fontSize: bold ? 17 : 14)),
          ],
        ),
      );
}
