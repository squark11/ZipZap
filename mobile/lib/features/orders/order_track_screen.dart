import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/api/api_exception.dart';
import '../../core/providers.dart';
import '../../core/theme/zz_theme.dart';
import '../../core/util/format.dart';
import '../../core/widgets/states.dart';
import '../../core/widgets/zz_icon.dart';
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

/// Ikona z zestawu marki dla każdego kroku osi statusów.
const _statusIcons = {
  'Placed': 'orders',
  'Confirmed': 'check',
  'Picking': 'cart',
  'ReadyForPickup': 'package',
  'InDelivery': 'delivery',
  'Delivered': 'check_circle',
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
    // Znaczniki czasu przejść: „Placed" = złożenie, reszta z historii statusów.
    final timestamps = <String, DateTime>{'Placed': o.placedAtUtc};
    for (final h in o.history) {
      timestamps[h.toStatus] = h.changedAtUtc;
    }
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
            OrderTimeline(currentStatus: o.status, timestamps: timestamps),
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

/// Oś statusów zamówienia (ilustrowana ikonami kroków; aktywny „pulsuje”).
class OrderTimeline extends StatelessWidget {
  final String currentStatus;

  /// Znaczniki czasu przejść (status → kiedy). Pokazywane przy zrobionych krokach.
  final Map<String, DateTime> timestamps;

  const OrderTimeline(
      {super.key, required this.currentStatus, this.timestamps = const {}});

  @override
  Widget build(BuildContext context) {
    // Delivered/Completed to stany końcowe — cała oś „zrobiona”, bez pulsu.
    final terminal = currentStatus == 'Delivered' || currentStatus == 'Completed';
    var currentIndex = _statusFlow.indexOf(currentStatus);
    if (currentStatus == 'Completed') currentIndex = _statusFlow.length - 1;
    return Column(
      children: List.generate(_statusFlow.length, (i) {
        final step = _statusFlow[i];
        final done = terminal ? i <= currentIndex : i < currentIndex;
        final current = terminal ? false : i == currentIndex;
        final reached = done || current;
        final isLast = i == _statusFlow.length - 1;

        final fill = current
            ? ZzColors.orange
            : (done ? ZzColors.green : context.zz.surface);
        final border = current
            ? ZzColors.orange
            : (done ? ZzColors.green : context.zz.border);
        final iconColor = reached ? Colors.white : context.zz.textMuted;
        final ts = timestamps[step];

        return IntrinsicHeight(
          child: Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Column(
                children: [
                  _StatusNode(
                    icon: _statusIcons[step]!,
                    fill: fill,
                    border: border,
                    iconColor: iconColor,
                    pulse: current,
                  ),
                  if (!isLast)
                    Expanded(
                      child: Container(
                        width: 2,
                        color: done ? ZzColors.green : context.zz.border,
                      ),
                    ),
                ],
              ),
              const SizedBox(width: 14),
              Padding(
                padding: const EdgeInsets.only(bottom: 22, top: 4),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      _statusLabels[step]!,
                      style: TextStyle(
                        fontWeight: reached ? FontWeight.w600 : FontWeight.w400,
                        color: current
                            ? ZzColors.orange600
                            : (done ? context.zz.text : context.zz.textMuted),
                      ),
                    ),
                    if (done && ts != null)
                      Padding(
                        padding: const EdgeInsets.only(top: 2),
                        child: Text(shortTime(ts),
                            style: TextStyle(color: context.zz.textMuted, fontSize: 12)),
                      ),
                    if (current)
                      const Padding(
                        padding: EdgeInsets.only(top: 2),
                        child: Text('W trakcie',
                            style: TextStyle(color: ZzColors.orange600, fontSize: 12)),
                      ),
                  ],
                ),
              ),
            ],
          ),
        );
      }),
    );
  }
}

/// Węzeł osi statusów: kółko z ikoną kroku; aktywny „pulsuje”.
class _StatusNode extends StatefulWidget {
  final String icon;
  final Color fill;
  final Color border;
  final Color iconColor;
  final bool pulse;
  const _StatusNode({
    required this.icon,
    required this.fill,
    required this.border,
    required this.iconColor,
    required this.pulse,
  });

  @override
  State<_StatusNode> createState() => _StatusNodeState();
}

class _StatusNodeState extends State<_StatusNode>
    with SingleTickerProviderStateMixin {
  AnimationController? _c;

  @override
  void initState() {
    super.initState();
    if (widget.pulse) {
      _c = AnimationController(
          vsync: this, duration: const Duration(milliseconds: 1500))
        ..repeat();
    }
  }

  @override
  void dispose() {
    _c?.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    const size = 30.0;
    final circle = Container(
      width: size,
      height: size,
      decoration: BoxDecoration(
        color: widget.fill,
        shape: BoxShape.circle,
        border: Border.all(color: widget.border, width: 2),
      ),
      child: Center(child: ZzIcon(widget.icon, size: 16, color: widget.iconColor)),
    );
    if (_c == null) return SizedBox(width: size, height: size, child: circle);
    return SizedBox(
      width: size,
      height: size,
      child: Stack(
        alignment: Alignment.center,
        clipBehavior: Clip.none,
        children: [
          AnimatedBuilder(
            animation: _c!,
            builder: (_, _) {
              final t = _c!.value;
              return Container(
                width: size + size * t,
                height: size + size * t,
                decoration: BoxDecoration(
                  shape: BoxShape.circle,
                  // Pomarańcz marki (249,115,22) z zanikającą przezroczystością.
                  color: Color.fromRGBO(249, 115, 22, (1 - t) * 0.30),
                ),
              );
            },
          ),
          circle,
        ],
      ),
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
