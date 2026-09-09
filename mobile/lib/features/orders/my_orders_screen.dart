import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/providers.dart';
import '../../core/theme/zz_theme.dart';
import '../../core/util/format.dart';
import '../../core/widgets/states.dart';
import '../../models/order.dart';

final myOrdersProvider = FutureProvider.autoDispose<List<Order>>(
    (ref) => ref.read(orderingRepositoryProvider).listMyOrders());

class MyOrdersScreen extends ConsumerWidget {
  const MyOrdersScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final orders = ref.watch(myOrdersProvider);
    return Scaffold(
      appBar: AppBar(title: const Text('Moje zamówienia')),
      body: orders.when(
        loading: () => const LoadingView(),
        error: (e, _) =>
            ErrorView(message: e.toString(), onRetry: () => ref.invalidate(myOrdersProvider)),
        data: (list) {
          if (list.isEmpty) {
            return const EmptyView(
              svgAsset: 'assets/svg/empty_box.svg',
              icon: Icons.receipt_long_outlined,
              title: 'Brak zamówień',
              subtitle: 'Twoje zamówienia pojawią się tutaj.',
            );
          }
          return RefreshIndicator(
            color: ZzColors.orange,
            onRefresh: () async => ref.invalidate(myOrdersProvider),
            child: ListView.separated(
              padding: const EdgeInsets.all(16),
              itemCount: list.length,
              separatorBuilder: (_, _) => const SizedBox(height: 10),
              itemBuilder: (_, i) {
                final o = list[i];
                return Card(
                  child: ListTile(
                    contentPadding: const EdgeInsets.symmetric(horizontal: 16, vertical: 6),
                    title: Text('Nr ${o.id.substring(0, 8)}',
                        style: const TextStyle(fontWeight: FontWeight.w600)),
                    subtitle: Text(
                        '${shortDateTime(o.placedAtUtc)} · ${zl(o.total)}',
                        style: const TextStyle(color: ZzColors.textMuted)),
                    trailing: StatusPill(o.status),
                    onTap: () => context.push('/orders/${o.id}'),
                  ),
                );
              },
            ),
          );
        },
      ),
    );
  }
}
