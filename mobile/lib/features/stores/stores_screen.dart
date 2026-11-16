import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/location/location_service.dart';
import '../../core/providers.dart';
import '../../core/theme/zz_theme.dart';
import '../../core/ui/ui.dart';
import '../../core/widgets/notifications_bell.dart';
import '../../core/widgets/skeleton.dart';
import '../../core/widgets/states.dart';
import '../../models/store.dart';
import '../account/addresses_screen.dart';

final _locationService = LocationService();

/// Lokalizacja klienta wybrana do sortowania sklepów wg odległości (null = nieustalona).
final myLocationProvider = StateProvider<LatLng?>((ref) => null);

/// Kod pocztowy filtrujący sklepy do tych, które pod niego dowożą (null = wszystkie).
final postalFilterProvider = StateProvider<String?>((ref) => null);

/// Czy kod pocztowy został już raz auto-uzupełniony z domyślnego adresu (blokada powtórki).
final _postalAutoFilledProvider = StateProvider<bool>((ref) => false);

final storesProvider = FutureProvider.autoDispose<List<Store>>((ref) {
  final loc = ref.watch(myLocationProvider);
  final postal = ref.watch(postalFilterProvider);
  return ref.read(catalogRepositoryProvider).listStores(lat: loc?.lat, lng: loc?.lng, postalCode: postal);
});

class StoresScreen extends ConsumerWidget {
  const StoresScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final stores = ref.watch(storesProvider);
    final auth = ref.watch(authControllerProvider);

    // Auto-uzupełnij kod pocztowy z domyślnego adresu klienta (jednorazowo, gdy zalogowany).
    if (auth.isAuthenticated) {
      ref.listen(addressesProvider, (_, next) {
        next.whenData((list) {
          if (ref.read(_postalAutoFilledProvider)) return;
          if (ref.read(postalFilterProvider) != null) return;
          String? picked;
          for (final a in list) {
            if (a.postalCode.replaceAll(RegExp(r'\D'), '').length != 5) continue;
            picked = a.postalCode;
            if (a.isDefault) break; // domyślny ma priorytet
          }
          if (picked != null) {
            ref.read(_postalAutoFilledProvider.notifier).state = true;
            ref.read(postalFilterProvider.notifier).state = picked;
          }
        });
      });
    }

    return Scaffold(
      appBar: AppBar(
        title: const Text('Dowózka.pl'),
        actions: [
          if (auth.isAuthenticated) const NotificationsBell(),
        ],
      ),
      body: Column(
        // Rozciągnięcie: karty sklepów zaczynają się równo z banerami (bez tego lista zwężała się i środkowała).
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          const _PostalBanner(),
          const _LocationBanner(),
          Expanded(
            child: stores.when(
              loading: () => const StoreListSkeleton(),
              error: (e, _) => ErrorView(
                message: e.toString(),
                onRetry: () => ref.invalidate(storesProvider),
              ),
              data: (list) {
                if (list.isEmpty) {
                  final filtered = ref.watch(postalFilterProvider) != null;
                  return EmptyView(
                    svgAsset: 'assets/svg/empty_box.svg',
                    icon: Icons.storefront_outlined,
                    title: filtered ? 'Brak sklepów dowożących' : 'Brak sklepów',
                    subtitle: filtered
                        ? 'Żaden sklep nie dowozi jeszcze pod ten kod pocztowy. Zmień kod lub spróbuj później.'
                        : 'W Twojej okolicy nie ma jeszcze aktywnych sklepów.',
                  );
                }
                return RefreshIndicator(
                  color: context.zz.brandStrong,
                  onRefresh: () async => ref.invalidate(storesProvider),
                  // Telefon: lista; szerzej: kolumny kart (maks. 520 px) o naturalnej wysokości — długa nazwa sklepu
                  // albo powiększony tekst nie przelewa karty (stała wysokość wiersza siatki by to robiła).
                  child: LayoutBuilder(
                    builder: (context, c) {
                      final inner = c.maxWidth - 2 * ZzSpace.screen;
                      final cols = c.maxWidth >= 700 ? (inner / 520).ceil().clamp(2, 3) : 1;
                      final cardWidth = (inner - (cols - 1) * ZzSpace.md) / cols;
                      return SingleChildScrollView(
                        physics: const AlwaysScrollableScrollPhysics(),
                        padding: const EdgeInsets.all(ZzSpace.screen),
                        child: Wrap(
                          spacing: ZzSpace.md,
                          runSpacing: ZzSpace.md,
                          children: [
                            for (final store in list)
                              SizedBox(
                                width: cardWidth,
                                child: ZzStoreCard(store: store, onTap: () => context.push('/stores/${store.id}')),
                              ),
                          ],
                        ),
                      );
                    },
                  ),
                );
              },
            ),
          ),
        ],
      ),
    );
  }
}

/// Baner kodu pocztowego: filtruje listę do sklepów dowożących pod wskazany kod.
class _PostalBanner extends ConsumerWidget {
  const _PostalBanner();

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final postal = ref.watch(postalFilterProvider);
    if (postal != null && postal.isNotEmpty) {
      return Container(
        width: double.infinity,
        padding: const EdgeInsets.fromLTRB(16, 10, 8, 10),
        color: context.zz.successTint,
        child: Row(
          children: [
            Icon(Icons.local_shipping_outlined, size: 18, color: context.zz.success),
            const SizedBox(width: 8),
            Expanded(
              child: Text('Sklepy dowożące pod ${_fmt(postal)}',
                  style: TextStyle(
                      color: context.zz.success, fontSize: 13, fontWeight: FontWeight.w600)),
            ),
            TextButton(onPressed: () => _edit(context, ref, postal), child: const Text('Zmień')),
          ],
        ),
      );
    }
    return InkWell(
      onTap: () => _edit(context, ref, null),
      child: Container(
        width: double.infinity,
        padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 12),
        color: context.zz.brandTint,
        child: Row(
          children: [
            Icon(Icons.markunread_mailbox_outlined, size: 18, color: context.zz.brandStrong),
            SizedBox(width: 8),
            Expanded(
              child: Text('Podaj kod pocztowy — pokaż tylko sklepy, które dowożą',
                  style: TextStyle(color: context.zz.brandStrong, fontSize: 14, fontWeight: FontWeight.w600)),
            ),
            Icon(Icons.chevron_right, color: context.zz.brandStrong),
          ],
        ),
      ),
    );
  }

  static String _fmt(String code) {
    final d = code.replaceAll(RegExp(r'\D'), '');
    return d.length == 5 ? '${d.substring(0, 2)}-${d.substring(2)}' : code;
  }

  Future<void> _edit(BuildContext context, WidgetRef ref, String? current) async {
    final controller = TextEditingController(text: current ?? '');
    final result = await showDialog<String?>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: const Text('Kod pocztowy dostawy'),
        content: TextField(
          controller: controller,
          autofocus: true,
          keyboardType: TextInputType.number,
          maxLength: 6,
          decoration: const InputDecoration(hintText: '31-042', counterText: ''),
        ),
        actions: [
          if (current != null)
            TextButton(
              onPressed: () => Navigator.pop(ctx, ''), // pusty = wyczyść filtr
              child: const Text('Wyczyść'),
            ),
          TextButton(onPressed: () => Navigator.pop(ctx, null), child: const Text('Anuluj')),
          FilledButton(
            style: FilledButton.styleFrom(backgroundColor: context.zz.brandStrong),
            onPressed: () => Navigator.pop(ctx, controller.text),
            child: const Text('Pokaż'),
          ),
        ],
      ),
    );
    if (result == null) return; // anulowano
    final digits = result.replaceAll(RegExp(r'\D'), '');
    if (digits.isEmpty) {
      ref.read(postalFilterProvider.notifier).state = null; // wyczyść filtr
      return;
    }
    if (digits.length != 5) {
      if (context.mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
            const SnackBar(content: Text('Kod pocztowy musi mieć 5 cyfr (np. 31-042).')));
      }
      return;
    }
    ref.read(postalFilterProvider.notifier).state =
        '${digits.substring(0, 2)}-${digits.substring(2)}';
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
        color: context.zz.brandTint,
        child: Row(
          children: [
            Icon(Icons.location_on, size: 18, color: context.zz.brandStrong),
            const SizedBox(width: 8),
            Expanded(
              child: Text('Sklepy w Twojej okolicy — najbliższe na górze',
                  style: TextStyle(color: context.zz.brandStrong, fontSize: 13, fontWeight: FontWeight.w600)),
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
        color: context.zz.brandTint,
        child: Row(
          children: [
            Icon(Icons.location_on, size: 18, color: context.zz.brandStrong),
            SizedBox(width: 8),
            Expanded(
              child: Text('Pokaż sklepy najbliżej Ciebie',
                  style: TextStyle(color: context.zz.brandStrong, fontSize: 14, fontWeight: FontWeight.w600)),
            ),
            Icon(Icons.chevron_right, color: context.zz.brandStrong),
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
