import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/api/api_exception.dart';
import '../../core/providers.dart';
import '../../core/theme/zz_theme.dart';
import '../../core/util/format.dart';
import '../../core/widgets/states.dart';
import '../../models/order.dart';

/// Kolejność statusów w osi czasu zamówienia.
const _statusFlow = [
  'Placed',
  'Confirmed',
  'Picking',
  'ReadyForPickup',
  'InDelivery',
  'Delivered',
];

const _statusLabels = {
  'Placed': 'Złożone',
  'Confirmed': 'Potwierdzone',
  'Picking': 'Kompletowane',
  'ReadyForPickup': 'Gotowe do odbioru',
  'InDelivery': 'W dostawie',
  'Delivered': 'Dostarczone',
};

class OrderTrackScreen extends ConsumerStatefulWidget {
  final String orderId;
  const OrderTrackScreen({super.key, required this.orderId});

  @override
  ConsumerState<OrderTrackScreen> createState() => _OrderTrackScreenState();
}

class _OrderTrackScreenState extends ConsumerState<OrderTrackScreen> {
  Order? _order;
  String? _error;
  bool _loading = true;
  Timer? _timer;

  @override
  void initState() {
    super.initState();
    _load();
    _timer = Timer.periodic(const Duration(seconds: 5), (_) => _load(silent: true));
  }

  @override
  void dispose() {
    _timer?.cancel();
    super.dispose();
  }

  Future<void> _load({bool silent = false}) async {
    try {
      final o = await ref.read(orderingRepositoryProvider).getOrder(widget.orderId);
      if (!mounted) return;
      setState(() {
        _order = o;
        _loading = false;
        _error = null;
      });
      if (o.status == 'Delivered' || o.status == 'Completed' || o.status == 'Cancelled') {
        _timer?.cancel();
      }
    } on ApiException catch (e) {
      if (!mounted || silent) return;
      setState(() {
        _error = e.message;
        _loading = false;
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('Zamówienie'),
        leading: IconButton(
          icon: const Icon(Icons.close),
          onPressed: () => context.go('/stores'),
        ),
      ),
      body: _loading
          ? const LoadingView()
          : _error != null
              ? ErrorView(message: _error!, onRetry: _load)
              : _body(_order!),
    );
  }

  Widget _body(Order o) {
    final cancelled = o.status == 'Cancelled';
    return RefreshIndicator(
      color: ZzColors.orange,
      onRefresh: () => _load(),
      child: ListView(
        padding: const EdgeInsets.all(16),
        children: [
          Row(
            children: [
              Text('Nr ${o.id.substring(0, 8)}',
                  style: Theme.of(context).textTheme.titleMedium),
              const Spacer(),
              StatusPill(o.status),
            ],
          ),
          const SizedBox(height: 4),
          Text('Złożono ${shortDateTime(o.placedAtUtc)}',
              style: TextStyle(color: context.zz.textMuted, fontSize: 13)),
          const SizedBox(height: 20),
          if (cancelled)
            const _Banner(
              color: Color(0xFFFEECEC),
              textColor: ZzColors.danger,
              text: 'To zamówienie zostało anulowane.',
            )
          else
            _Timeline(currentStatus: o.status),
          const SizedBox(height: 24),
          Text('Produkty', style: Theme.of(context).textTheme.titleMedium),
          const SizedBox(height: 8),
          ...o.items.map((it) => Padding(
                padding: const EdgeInsets.symmetric(vertical: 4),
                child: Row(
                  children: [
                    Expanded(child: Text('${it.quantity} × ${it.productName}')),
                    Text(zl(it.lineTotal)),
                  ],
                ),
              )),
          const Divider(height: 24),
          _row('Wartość produktów', zl(o.subtotal)),
          _row('Opłata za dostawę', zl(o.deliveryFee)),
          const SizedBox(height: 4),
          _row('Razem', zl(o.total), bold: true),
          const SizedBox(height: 20),
          Text('Dostawa', style: Theme.of(context).textTheme.titleMedium),
          const SizedBox(height: 6),
          Text(o.deliveryAddress),
          Text('tel. ${o.contactPhone}',
              style: TextStyle(color: context.zz.textMuted)),
        ],
      ),
    );
  }

  Widget _row(String label, String value, {bool bold = false}) => Padding(
        padding: const EdgeInsets.symmetric(vertical: 3),
        child: Row(
          mainAxisAlignment: MainAxisAlignment.spaceBetween,
          children: [
            Text(label,
                style: TextStyle(
                    color: bold ? context.zz.text : context.zz.textMuted,
                    fontWeight: bold ? FontWeight.w700 : FontWeight.w400,
                    fontSize: bold ? 17 : 14)),
            Text(value,
                style: TextStyle(fontWeight: FontWeight.w700, fontSize: bold ? 17 : 14)),
          ],
        ),
      );
}

class _Timeline extends StatelessWidget {
  final String currentStatus;
  const _Timeline({required this.currentStatus});

  @override
  Widget build(BuildContext context) {
    var currentIndex = _statusFlow.indexOf(currentStatus);
    if (currentStatus == 'Completed') currentIndex = _statusFlow.length - 1;
    return Column(
      children: List.generate(_statusFlow.length, (i) {
        final done = i <= currentIndex;
        final isLast = i == _statusFlow.length - 1;
        return IntrinsicHeight(
          child: Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Column(
                children: [
                  Container(
                    width: 22,
                    height: 22,
                    decoration: BoxDecoration(
                      color: done ? ZzColors.green : context.zz.surface,
                      shape: BoxShape.circle,
                      border: Border.all(
                          color: done ? ZzColors.green : context.zz.border, width: 2),
                    ),
                    child: done
                        ? const Icon(Icons.check, size: 14, color: Colors.white)
                        : null,
                  ),
                  if (!isLast)
                    Expanded(
                      child: Container(
                        width: 2,
                        color: i < currentIndex ? ZzColors.green : context.zz.border,
                      ),
                    ),
                ],
              ),
              const SizedBox(width: 12),
              Padding(
                padding: const EdgeInsets.only(bottom: 18, top: 1),
                child: Text(
                  _statusLabels[_statusFlow[i]]!,
                  style: TextStyle(
                    fontWeight: done ? FontWeight.w600 : FontWeight.w400,
                    color: done ? ZzColors.text : context.zz.textMuted,
                  ),
                ),
              ),
            ],
          ),
        );
      }),
    );
  }
}

class _Banner extends StatelessWidget {
  final Color color;
  final Color textColor;
  final String text;
  const _Banner({required this.color, required this.textColor, required this.text});

  @override
  Widget build(BuildContext context) => Container(
        width: double.infinity,
        padding: const EdgeInsets.all(12),
        decoration: BoxDecoration(color: color, borderRadius: BorderRadius.circular(ZzRadius.md)),
        child: Text(text, style: TextStyle(color: textColor, fontWeight: FontWeight.w600)),
      );
}
