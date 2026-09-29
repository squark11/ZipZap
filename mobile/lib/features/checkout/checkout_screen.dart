import 'dart:math';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:url_launcher/url_launcher.dart';

import '../../core/api/api_exception.dart';
import '../../core/providers.dart';
import '../../core/theme/zz_theme.dart';
import '../../core/util/format.dart';
import '../../core/widgets/states.dart';
import '../../core/widgets/test_order_banner.dart';
import '../../core/widgets/zz_icon.dart';
import '../../models/delivery.dart';
import '../../models/purchasing_round.dart';
import '../../models/store_legal.dart';
import '../cart/cart_controller.dart';

final zonesProvider = FutureProvider.autoDispose.family<List<DeliveryZone>, String>(
    (ref, storeId) => ref.read(orderingRepositoryProvider).listZones(storeId));

final storeLegalProvider = FutureProvider.autoDispose.family<StoreLegal, String>(
    (ref, storeId) => ref.read(orderingRepositoryProvider).getStoreLegal(storeId));

final slotsProvider =
    FutureProvider.autoDispose.family<List<TimeSlot>, ({String storeId, String zoneId})>(
        (ref, k) =>
            ref.read(orderingRepositoryProvider).listSlots(k.storeId, zoneId: k.zoneId));

/// Najbliższa runda zakupowa sklepu (+ termin graniczny i najwcześniejsza dostawa).
final roundPreviewProvider = FutureProvider.autoDispose.family<RoundPreview, String>(
    (ref, storeId) => ref.read(orderingRepositoryProvider).getRoundPreview(storeId));

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
  bool _consent = false;
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
    final round = ref.read(roundPreviewProvider(cart.storeId)).valueOrNull?.round;
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
            consentAccepted: _consent,
            expectedRoundStartsAtUtc: round?.startsAtUtc,
          );
      ref.read(cartControllerProvider.notifier).clearAfterCheckout();
      // Tryb decyduje zamówienie z serwera (nie lokalna konfiguracja): zamówienie testowe
      // (pilotaż) NIGDY nie trafia na ekran płatności.
      if (mounted) context.go(order.isTestOrder ? '/orders/${order.id}' : '/pay/${order.id}');
    } on ApiException catch (e) {
      // 409: m.in. minął termin graniczny pokazanej rundy — odświeżamy rundę i terminy,
      // klient potwierdza ponownie (bez cichego przesunięcia zamówienia).
      if (e.statusCode == 409) {
        ref.invalidate(roundPreviewProvider(cart.storeId));
        if (_zoneId != null) {
          ref.invalidate(slotsProvider((storeId: cart.storeId, zoneId: _zoneId!)));
        }
      }
      _snack(e.message);
    } finally {
      if (mounted) setState(() => _submitting = false);
    }
  }

  void _snack(String m) =>
      ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(m)));

  /// Pierwszy brakujący warunek złożenia zamówienia (null = można składać).
  String? _missingReason(StoreLegal? legal,
      {required bool testerBlocked, required AsyncValue<RoundPreview> round, TimeSlot? slot}) {
    if (testerBlocked) return 'Zamówienia w pilotażu są dostępne tylko dla zaproszonych testerów.';
    final preview = round.valueOrNull;
    if (preview == null) {
      return round.hasError
          ? 'Nie udało się sprawdzić rundy zakupowej — spróbuj ponownie.'
          : 'Sprawdzamy najbliższą rundę zakupową…';
    }
    if (!preview.available) return preview.message ?? 'Sklep nie przyjmuje teraz zamówień.';
    if (_address.text.trim().isEmpty) return 'Podaj adres dostawy.';
    if (_phone.text.trim().isEmpty) return 'Podaj telefon kontaktowy.';
    if (_zoneId == null) return 'Wybierz strefę dostawy.';
    if (_slotId == null) return 'Wybierz termin dostawy.';
    if (slot != null && !preview.allowsSlot(slot)) {
      return 'Wybrany termin jest przed zakupami w rundzie — wybierz późniejszy.';
    }
    if ((legal?.requiresAcceptance ?? false) && !_consent) {
      return 'Zaakceptuj regulamin i politykę prywatności.';
    }
    return null;
  }

  @override
  Widget build(BuildContext context) {
    final cart = ref.watch(cartControllerProvider);
    final c = cart.cart;
    // Pilotaż W1: zamówienia testowe bez opłaty, tylko dla testerów (serwer i tak to egzekwuje).
    final testMode = ref.watch(publicConfigProvider).valueOrNull?.isTestOrdering ?? false;
    final roles = ref.watch(authControllerProvider).user?.roles ?? const <String>[];
    final testerBlocked = testMode && !roles.contains('Tester') && !roles.contains('Admin');
    final title = testMode ? 'Dostawa — zamówienie testowe' : 'Dostawa i płatność';
    if (c == null || c.items.isEmpty) {
      return Scaffold(
        appBar: AppBar(title: Text(title)),
        // Po odświeżeniu strony koszyk jest jeszcze odtwarzany — nie pokazujemy wtedy „pustego koszyka".
        body: cart.restoring && c == null
            ? const LoadingView(label: 'Wczytywanie koszyka…')
            : const EmptyView(
                icon: Icons.shopping_cart_outlined,
                title: 'Koszyk jest pusty',
              ),
      );
    }

    final zones = ref.watch(zonesProvider(c.storeId));

    return Scaffold(
      appBar: AppBar(title: Text(title)),
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
          final legal = ref.watch(storeLegalProvider(c.storeId)).valueOrNull;
          final round = ref.watch(roundPreviewProvider(c.storeId));
          TimeSlot? selectedSlot;
          if (_zoneId != null && _slotId != null) {
            final slots = ref
                    .watch(slotsProvider((storeId: c.storeId, zoneId: _zoneId!)))
                    .valueOrNull ??
                const <TimeSlot>[];
            for (final s in slots) {
              if (s.id == _slotId) selectedSlot = s;
            }
          }
          final missing = _missingReason(legal,
              testerBlocked: testerBlocked, round: round, slot: selectedSlot);
          return ListView(
            padding: const EdgeInsets.all(16),
            children: [
              if (testMode) ...[
                TestOrderBanner(blocked: testerBlocked),
                const SizedBox(height: 16),
              ],
              _RoundCard(
                round: round,
                onRetry: () => ref.invalidate(roundPreviewProvider(c.storeId)),
              ),
              const SizedBox(height: 16),
              const _Label('Adres dostawy', icon: 'home'),
              TextField(
                controller: _address,
                onChanged: (_) => setState(() {}),
                decoration: const InputDecoration(hintText: 'ul. Przykładowa 12/3'),
              ),
              const SizedBox(height: 16),
              const _Label('Telefon kontaktowy', icon: 'phone'),
              TextField(
                controller: _phone,
                keyboardType: TextInputType.phone,
                onChanged: (_) => setState(() {}),
                decoration: const InputDecoration(hintText: '600 100 200'),
              ),
              const SizedBox(height: 16),
              const _Label('Strefa dostawy', icon: 'location'),
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
                const _Label('Termin dostawy', icon: 'clock'),
                _SlotPicker(
                  storeId: c.storeId,
                  zoneId: _zoneId!,
                  value: _slotId,
                  round: round.valueOrNull,
                  onChanged: (v) => setState(() => _slotId = v),
                ),
                const SizedBox(height: 16),
              ],
              if (legal != null && (legal.hasAnyDoc || legal.requiresAcceptance)) ...[
                const Divider(height: 24),
                const _Label('Dokumenty sklepu'),
                _LegalLinks(legal: legal),
                if (legal.requiresAcceptance)
                  CheckboxListTile(
                    value: _consent,
                    onChanged: (v) => setState(() => _consent = v ?? false),
                    controlAffinity: ListTileControlAffinity.leading,
                    contentPadding: EdgeInsets.zero,
                    dense: true,
                    activeColor: ZzColors.orange,
                    title: const Text(
                        'Akceptuję regulamin i politykę prywatności sklepu',
                        style: TextStyle(fontSize: 13)),
                  ),
              ],
              const Divider(height: 24),
              _SummaryRow('Wartość produktów', zl(cart.subtotal)),
              _SummaryRow('Opłata za dostawę', zl(fee)),
              const SizedBox(height: 4),
              if (testMode) ...[
                _SummaryRow('Wartość zamówienia', zl(cart.subtotal + fee)),
                _SummaryRow('Opłata', 'bez opłaty — zamówienie testowe', bold: true),
              ] else
                _SummaryRow('Razem', zl(cart.subtotal + fee), bold: true),
              const SizedBox(height: 20),
              ElevatedButton(
                onPressed: (_submitting || missing != null) ? null : () => _submit(fee),
                child: _submitting
                    ? const SizedBox(
                        height: 22,
                        width: 22,
                        child: CircularProgressIndicator(strokeWidth: 2, color: Colors.white))
                    : Text(testMode
                        ? 'Złóż zamówienie testowe'
                        : 'Złóż zamówienie i przejdź do płatności'),
              ),
              const SizedBox(height: 8),
              Text(
                missing == null || _submitting
                    ? (testMode
                        ? 'Nie pobieramy żadnej płatności — to zamówienie testowe pilotażu.'
                        : 'Płatność potwierdza dostawca — status zaktualizuje się automatycznie.')
                    : missing,
                textAlign: TextAlign.center,
                style: TextStyle(
                    color: missing == null || _submitting
                        ? context.zz.textMuted
                        : ZzColors.orange600,
                    fontSize: 12),
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
  final RoundPreview? round;
  final ValueChanged<String?> onChanged;
  const _SlotPicker(
      {required this.storeId,
      required this.zoneId,
      required this.value,
      required this.round,
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
          return Text('Brak dostępnych terminów w tej strefie.',
              style: TextStyle(color: context.zz.textMuted));
        }
        final r = round;
        return Column(
          children: [
            for (final s in list)
              Padding(
                padding: const EdgeInsets.only(bottom: 8),
                child: _SlotCard(
                  slot: s,
                  selected: s.id == value,
                  // Termin przed zakupami w rundzie (lub gdy runda nieznana) — nie do wybrania.
                  tooEarlyNote: r == null || r.allowsSlot(s)
                      ? null
                      : (r.earliestDeliveryLabel == null
                          ? 'Niedostępny'
                          : 'Za wcześnie — dostawy od ${r.earliestDeliveryLabel}'),
                  onTap: () => onChanged(s.id),
                ),
              ),
          ],
        );
      },
    );
  }
}

/// Karta terminu (fali) dostawy — pokazuje harmonogram wprost i pozwala wybrać.
class _SlotCard extends StatelessWidget {
  final TimeSlot slot;
  final bool selected;
  final String? tooEarlyNote;
  final VoidCallback onTap;
  const _SlotCard(
      {required this.slot, required this.selected, required this.onTap, this.tooEarlyNote});

  @override
  Widget build(BuildContext context) {
    final soldOut = slot.remainingCapacity <= 0;
    final disabled = soldOut || tooEarlyNote != null;
    return InkWell(
      onTap: disabled ? null : onTap,
      borderRadius: BorderRadius.circular(ZzRadius.md),
      child: Opacity(
        opacity: disabled ? 0.5 : 1,
        child: Container(
          padding: const EdgeInsets.all(12),
          decoration: BoxDecoration(
            color: selected ? context.zz.orangeTint : context.zz.surface,
            border: Border.all(
                color: selected ? ZzColors.orange : context.zz.border,
                width: selected ? 1.5 : 1),
            borderRadius: BorderRadius.circular(ZzRadius.md),
          ),
          child: Row(
            children: [
              ZzIcon('clock',
                  size: 18, color: selected ? ZzColors.orange : context.zz.textMuted),
              const SizedBox(width: 10),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(slot.label,
                        style: TextStyle(
                            fontWeight: FontWeight.w600,
                            color: selected ? ZzColors.orange600 : context.zz.text)),
                    const SizedBox(height: 2),
                    Text(
                        tooEarlyNote ??
                            (soldOut
                                ? 'Brak wolnych miejsc'
                                : 'Wolne miejsca: ${slot.remainingCapacity}'),
                        style: TextStyle(color: context.zz.textMuted, fontSize: 12)),
                  ],
                ),
              ),
              if (selected)
                const ZzIcon('check_circle', size: 20, color: ZzColors.orange),
            ],
          ),
        ),
      ),
    );
  }
}

/// Runda zakupowa PRZED złożeniem zamówienia: kiedy sklep robi zakupy, do kiedy zamówić
/// (termin graniczny) i od kiedy możliwa dostawa. Okno dostawy wybiera się osobno poniżej.
class _RoundCard extends StatelessWidget {
  final AsyncValue<RoundPreview> round;
  final VoidCallback onRetry;
  const _RoundCard({required this.round, required this.onRetry});

  @override
  Widget build(BuildContext context) {
    return round.when(
      loading: () => const Padding(
        padding: EdgeInsets.symmetric(vertical: 8),
        child: LinearProgressIndicator(color: ZzColors.orange),
      ),
      error: (e, _) => _box(
        context,
        warn: true,
        children: [
          Text('Nie udało się sprawdzić rundy zakupowej.',
              style: TextStyle(color: context.zz.text, fontWeight: FontWeight.w600)),
          TextButton(onPressed: onRetry, child: const Text('Spróbuj ponownie')),
        ],
      ),
      data: (p) {
        final r = p.round;
        if (!p.available || r == null) {
          return _box(
            context,
            warn: true,
            children: [
              Text(
                  p.reason == 'store_closed'
                      ? 'Sklep jest zamknięty'
                      : 'Brak dostępnej rundy zakupowej',
                  style: TextStyle(color: context.zz.text, fontWeight: FontWeight.w700)),
              const SizedBox(height: 4),
              Text(p.message ?? 'Sklep nie przyjmuje teraz zamówień.',
                  style: TextStyle(color: context.zz.textMuted, fontSize: 13)),
            ],
          );
        }
        return _box(
          context,
          children: [
            Text('Zakupy w sklepie: ${r.label}',
                style: TextStyle(color: context.zz.text, fontWeight: FontWeight.w700)),
            const SizedBox(height: 4),
            Text('Zamów do ${r.cutoffLabel} — później zamówienie trafi do kolejnej rundy.',
                style: TextStyle(color: context.zz.textMuted, fontSize: 13)),
            if (p.earliestDeliveryLabel != null) ...[
              const SizedBox(height: 4),
              Text('Dostawa najwcześniej: ${p.earliestDeliveryLabel} (termin wybierasz poniżej).',
                  style: TextStyle(color: context.zz.textMuted, fontSize: 13)),
            ],
            const SizedBox(height: 4),
            Text('Godziny według czasu polskiego.',
                style: TextStyle(color: context.zz.textMuted, fontSize: 11)),
          ],
        );
      },
    );
  }

  Widget _box(BuildContext context, {required List<Widget> children, bool warn = false}) =>
      Container(
        padding: const EdgeInsets.all(12),
        decoration: BoxDecoration(
          color: warn ? context.zz.surface : context.zz.orangeTint,
          border: Border.all(color: warn ? ZzColors.orange600 : ZzColors.orange),
          borderRadius: BorderRadius.circular(ZzRadius.md),
        ),
        child: Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            ZzIcon(warn ? 'info' : 'cart', size: 20, color: ZzColors.orange600),
            const SizedBox(width: 10),
            Expanded(
              child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: children),
            ),
          ],
        ),
      );
}

/// Klikalne linki do dokumentów prawnych sklepu (otwierane w przeglądarce).
class _LegalLinks extends StatelessWidget {
  final StoreLegal legal;
  const _LegalLinks({required this.legal});

  @override
  Widget build(BuildContext context) {
    final items = <(String, String)>[];
    if ((legal.termsUrl ?? '').isNotEmpty) items.add(('Regulamin', legal.termsUrl!));
    if ((legal.privacyUrl ?? '').isNotEmpty) items.add(('Polityka prywatności', legal.privacyUrl!));
    if ((legal.gdprUrl ?? '').isNotEmpty) items.add(('Informacja RODO', legal.gdprUrl!));
    if (items.isEmpty) {
      return Text('Sklep nie udostępnił dokumentów.',
          style: TextStyle(color: context.zz.textMuted, fontSize: 13));
    }
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        for (final it in items)
          InkWell(
            onTap: () => _open(context, it.$2),
            child: Padding(
              padding: const EdgeInsets.symmetric(vertical: 6),
              child: Row(children: [
                const Icon(Icons.open_in_new, size: 16, color: ZzColors.orange),
                const SizedBox(width: 8),
                Expanded(
                  child: Text(it.$1,
                      style: const TextStyle(
                          color: ZzColors.orange600,
                          fontWeight: FontWeight.w600,
                          decoration: TextDecoration.underline)),
                ),
              ]),
            ),
          ),
      ],
    );
  }

  Future<void> _open(BuildContext context, String url) async {
    final uri = Uri.tryParse(url);
    var ok = false;
    if (uri != null) {
      ok = await launchUrl(uri, mode: LaunchMode.externalApplication);
    }
    if (!ok && context.mounted) {
      ScaffoldMessenger.of(context)
          .showSnackBar(const SnackBar(content: Text('Nie udało się otworzyć dokumentu.')));
    }
  }
}

class _Label extends StatelessWidget {
  final String text;
  final String? icon;
  const _Label(this.text, {this.icon});
  @override
  Widget build(BuildContext context) => Padding(
        padding: const EdgeInsets.only(bottom: 8),
        child: Row(
          children: [
            if (icon != null) ...[
              ZzIcon(icon!, size: 18, color: context.zz.textMuted),
              const SizedBox(width: 6),
            ],
            Text(text, style: const TextStyle(fontWeight: FontWeight.w600)),
          ],
        ),
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
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(label,
                style: TextStyle(
                    color: bold ? context.zz.text : context.zz.textMuted,
                    fontWeight: bold ? FontWeight.w700 : FontWeight.w400,
                    fontSize: bold ? 17 : 14)),
            const SizedBox(width: 12),
            // Długa wartość (np. „bez opłaty — zamówienie testowe") zawija się zamiast wychodzić poza ekran.
            Expanded(
              child: Text(value,
                  textAlign: TextAlign.right,
                  style: TextStyle(
                      fontWeight: FontWeight.w700, fontSize: bold ? 17 : 14)),
            ),
          ],
        ),
      );
}
