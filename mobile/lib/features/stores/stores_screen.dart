import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/providers.dart';
import '../../core/theme/zz_theme.dart';
import '../../core/util/format.dart';
import '../../core/widgets/notifications_bell.dart';
import '../../core/widgets/states.dart';
import '../../models/store.dart';

final storesProvider = FutureProvider.autoDispose<List<Store>>(
    (ref) => ref.read(catalogRepositoryProvider).listStores());

class StoresScreen extends ConsumerWidget {
  const StoresScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final stores = ref.watch(storesProvider);
    final auth = ref.watch(authControllerProvider);

    return Scaffold(
      appBar: AppBar(
        title: const Text('ZipZap'),
        actions: [
          if (auth.isAuthenticated) const NotificationsBell(),
        ],
      ),
      body: stores.when(
        loading: () => const LoadingView(label: 'Ładowanie sklepów…'),
        error: (e, _) => ErrorView(
          message: e.toString(),
          onRetry: () => ref.invalidate(storesProvider),
        ),
        data: (list) {
          if (list.isEmpty) {
            return const EmptyView(
              svgAsset: 'assets/svg/empty_box.svg',
              icon: Icons.storefront_outlined,
              title: 'Brak sklepów',
              subtitle: 'W Twojej okolicy nie ma jeszcze aktywnych sklepów.',
            );
          }
          return RefreshIndicator(
            color: ZzColors.orange,
            onRefresh: () async => ref.invalidate(storesProvider),
            child: ListView.separated(
              padding: const EdgeInsets.all(16),
              itemCount: list.length,
              separatorBuilder: (_, _) => const SizedBox(height: 12),
              itemBuilder: (_, i) => _StoreCard(store: list[i]),
            ),
          );
        },
      ),
    );
  }
}

class _StoreCard extends StatelessWidget {
  final Store store;
  const _StoreCard({required this.store});

  @override
  Widget build(BuildContext context) {
    final open = store.isAcceptingOrders;
    return Card(
      child: InkWell(
        borderRadius: BorderRadius.circular(ZzRadius.lg),
        onTap: () => context.push('/stores/${store.id}'),
        child: Padding(
          padding: const EdgeInsets.all(16),
          child: Row(
            children: [
              Container(
                width: 48,
                height: 48,
                decoration: BoxDecoration(
                  color: ZzColors.orange50,
                  borderRadius: BorderRadius.circular(ZzRadius.md),
                ),
                child: const Icon(Icons.storefront, color: ZzColors.orange),
              ),
              const SizedBox(width: 14),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(store.name,
                        style: Theme.of(context).textTheme.titleMedium),
                    const SizedBox(height: 2),
                    Text('${store.city} · min. ${zl(store.minimumOrderValue)}',
                        style: const TextStyle(color: ZzColors.textMuted, fontSize: 13)),
                  ],
                ),
              ),
              Container(
                padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
                decoration: BoxDecoration(
                  color: open ? ZzColors.green50 : const Color(0xFFFEECEC),
                  borderRadius: BorderRadius.circular(ZzRadius.sm),
                ),
                child: Text(open ? 'Otwarte' : 'Zamknięte',
                    style: TextStyle(
                        color: open ? const Color(0xFF128040) : ZzColors.danger,
                        fontSize: 12,
                        fontWeight: FontWeight.w600)),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
