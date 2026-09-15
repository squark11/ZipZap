import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/location/location_service.dart';
import '../../core/providers.dart';
import '../../core/theme/zz_theme.dart';
import '../../core/util/format.dart';
import '../../core/widgets/notifications_bell.dart';
import '../../core/widgets/skeleton.dart';
import '../../core/widgets/states.dart';
import '../../core/widgets/store_logo.dart';
import '../../models/store.dart';

final _locationService = LocationService();

/// Lokalizacja klienta wybrana do sortowania sklepów wg odległości (null = nieustalona).
final myLocationProvider = StateProvider<LatLng?>((ref) => null);

final storesProvider = FutureProvider.autoDispose<List<Store>>((ref) {
  final loc = ref.watch(myLocationProvider);
  return ref.read(catalogRepositoryProvider).listStores(lat: loc?.lat, lng: loc?.lng);
});

class StoresScreen extends ConsumerWidget {
  const StoresScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final stores = ref.watch(storesProvider);
    final auth = ref.watch(authControllerProvider);

    return Scaffold(
      appBar: AppBar(
        title: const Text('Dowózka.pl'),
        actions: [
          if (auth.isAuthenticated) const NotificationsBell(),
        ],
      ),
      body: stores.when(
        loading: () => const StoreListSkeleton(),
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
          return Column(
            children: [
              const _LocationBanner(),
              Expanded(
                child: RefreshIndicator(
                  color: ZzColors.orange,
                  onRefresh: () async => ref.invalidate(storesProvider),
                  child: ListView.separated(
                    padding: const EdgeInsets.all(16),
                    itemCount: list.length,
                    separatorBuilder: (_, _) => const SizedBox(height: 12),
                    itemBuilder: (_, i) => _StoreCard(store: list[i]),
                  ),
                ),
              ),
            ],
          );
        },
      ),
    );
  }
}

/// Baner lokalizacji: włącza sortowanie sklepów wg odległości (GPS/przeglądarka).
class _LocationBanner extends ConsumerWidget {
  const _LocationBanner();

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final loc = ref.watch(myLocationProvider);
    if (loc != null) {
      return Container(
        width: double.infinity,
        padding: const EdgeInsets.fromLTRB(16, 10, 8, 10),
        color: context.zz.orangeTint,
        child: Row(
          children: [
            const Icon(Icons.location_on, size: 18, color: ZzColors.orange600),
            const SizedBox(width: 8),
            const Expanded(
              child: Text('Sklepy w Twojej okolicy — najbliższe na górze',
                  style: TextStyle(color: ZzColors.orange600, fontSize: 13, fontWeight: FontWeight.w600)),
            ),
            TextButton(onPressed: () => ref.invalidate(myLocationProvider), child: const Text('Wyłącz')),
          ],
        ),
      );
    }
    return InkWell(
      onTap: () => _enable(context, ref),
      child: Container(
        width: double.infinity,
        padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 12),
        color: context.zz.orangeTint,
        child: const Row(
          children: [
            Icon(Icons.location_on, size: 18, color: ZzColors.orange),
            SizedBox(width: 8),
            Expanded(
              child: Text('Pokaż sklepy najbliżej Ciebie',
                  style: TextStyle(color: ZzColors.orange600, fontSize: 14, fontWeight: FontWeight.w600)),
            ),
            Icon(Icons.chevron_right, color: ZzColors.orange),
          ],
        ),
      ),
    );
  }

  Future<void> _enable(BuildContext context, WidgetRef ref) async {
    final loc = await _locationService.current();
    if (loc == null) {
      if (context.mounted) {
        ScaffoldMessenger.of(context).showSnackBar(const SnackBar(
            content: Text('Nie udało się ustalić lokalizacji. Sprawdź zgodę na lokalizację.')));
      }
      return;
    }
    ref.read(myLocationProvider.notifier).state = loc;
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
              StoreLogo(logoUrl: store.logoUrl, size: 48),
              const SizedBox(width: 14),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(store.name,
                        style: Theme.of(context).textTheme.titleMedium),
                    const SizedBox(height: 2),
                    Text(
                        store.distanceLabel != null
                            ? '${store.city} · ${store.distanceLabel} · min. ${zl(store.minimumOrderValue)}'
                            : '${store.city} · min. ${zl(store.minimumOrderValue)}',
                        style: TextStyle(color: context.zz.textMuted, fontSize: 13)),
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
