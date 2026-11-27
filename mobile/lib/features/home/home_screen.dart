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
import '../../models/purchasing_round.dart';
import '../../models/store.dart';
import '../account/addresses_screen.dart';
import 'delivery_place_sheet.dart';
import 'home_providers.dart';

final _locationService = LocationService();

/// Start (E3-1): nagłówek „Dostawa do" → powitanie → szukajka → najbliższa runda → sklepy.
/// Kategorie platformy (F-6), okładki (F-5) i „Zamów ponownie" (F-8) wymagają danych z serwera — dojdą osobno.
class HomeScreen extends ConsumerWidget {
  const HomeScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final stores = ref.watch(storesProvider);
    final auth = ref.watch(authControllerProvider);

    // Auto-uzupełnij kod pocztowy z domyślnego adresu klienta (jednorazowo, gdy zalogowany) — bez zmian względem
    // dawnego baneru.
    if (auth.isAuthenticated) {
      ref.listen(addressesProvider, (_, next) {
        next.whenData((list) {
          if (ref.read(postalAutoFilledProvider)) return;
          if (ref.read(postalFilterProvider) != null) return;
          String? picked;
          String? label;
          for (final a in list) {
            final code = normalizePostal(a.postalCode);
            if (code == null) continue;
            picked = code;
            label = a.label.isEmpty ? null : a.label;
            if (a.isDefault) break; // domyślny ma priorytet
          }
          if (picked != null) {
            ref.read(postalAutoFilledProvider.notifier).state = true;
            ref.read(postalFilterProvider.notifier).state = picked;
            ref.read(postalLabelProvider.notifier).state = label;
          }
        });
      });
    }

    final firstName = auth.user?.fullName.trim().split(RegExp(r'\s+')).first ?? '';

    // Kategorie platformy (F-6): chipy tylko dla kategorii obecnych w widocznych sklepach. Wybrany chip, którego już
    // nie ma (np. po zmianie kodu pocztowego), przestaje filtrować.
    final all = stores.valueOrNull ?? const <Store>[];
    final chips = chipsFor(ref.watch(platformCategoriesProvider).valueOrNull ?? const [], all);
    final picked = ref.watch(selectedCategoryProvider);
    final category = chips.any((c) => c.key == picked) ? picked : null;
    final categoryName = category == null ? null : chips.firstWhere((c) => c.key == category).name;

    return Scaffold(
      appBar: AppBar(
        toolbarHeight: 72,
        titleSpacing: ZzSpace.screen,
        title: const _PlaceHeader(),
        actions: [if (auth.isAuthenticated) const NotificationsBell()],
      ),
      body: RefreshIndicator(
        color: context.zz.brandStrong,
        onRefresh: () async => ref.invalidate(storesProvider),
        child: CustomScrollView(
          physics: const AlwaysScrollableScrollPhysics(),
          slivers: [
            SliverPadding(
              padding: const EdgeInsets.fromLTRB(ZzSpace.screen, ZzSpace.xs, ZzSpace.screen, 0),
              sliver: SliverList.list(
                children: [
                  Semantics(
                    header: true,
                    child: Text(
                      firstName.isEmpty ? 'Co dziś kupujemy?' : 'Co dziś kupujemy, $firstName?',
                      style: Theme.of(context).textTheme.headlineSmall,
                    ),
                  ),
                  const SizedBox(height: ZzSpace.md),
                  ZzSearchField(hint: 'Szukaj sklepu albo miejscowości', onTap: () => context.go('/search')),
                  const SizedBox(height: ZzSpace.md),
                  if (stores.valueOrNull case final list?) _NextRoundNotice(stores: list),
                ],
              ),
            ),
            if (chips.isNotEmpty)
              SliverToBoxAdapter(
                child: SizedBox(
                  height: 96,
                  child: ListView(
                    scrollDirection: Axis.horizontal,
                    padding: const EdgeInsets.symmetric(horizontal: ZzSpace.screen - 4),
                    children: [
                      ZzCategoryChip(
                        label: 'Wszystko',
                        icon: 'wszystko',
                        selected: category == null,
                        onTap: () => ref.read(selectedCategoryProvider.notifier).state = null,
                      ),
                      for (final c in chips)
                        ZzCategoryChip(
                          label: c.name,
                          icon: c.icon,
                          selected: c.key == category,
                          onTap: () =>
                              ref.read(selectedCategoryProvider.notifier).state = c.key == category ? null : c.key,
                        ),
                    ],
                  ),
                ),
              ),
            SliverPadding(
              padding: const EdgeInsets.fromLTRB(ZzSpace.screen, ZzSpace.sm, ZzSpace.screen, ZzSpace.sm),
              sliver: SliverToBoxAdapter(
                child: ZzSectionHeader(
                  title: categoryName != null
                      ? 'Sklepy: $categoryName'
                      : ref.watch(postalFilterProvider) == null
                      ? 'Sklepy'
                      : 'Sklepy, które tu dowożą',
                ),
              ),
            ),
            ...stores.when(
              loading: () => [const SliverToBoxAdapter(child: SizedBox(height: 420, child: StoreListSkeleton()))],
              error: (e, _) => [
                SliverFillRemaining(
                  hasScrollBody: false,
                  child: ErrorView(message: e.toString(), onRetry: () => ref.invalidate(storesProvider)),
                ),
              ],
              data: (list) => list.isEmpty
                  ? [
                      SliverFillRemaining(
                        hasScrollBody: false,
                        child: _noStores(ref.watch(postalFilterProvider) != null),
                      ),
                    ]
                  : [
                      SliverPadding(
                        padding: const EdgeInsets.fromLTRB(ZzSpace.screen, 0, ZzSpace.screen, ZzSpace.screen),
                        sliver: SliverToBoxAdapter(child: _StoreGrid(stores: storesInCategory(list, category))),
                      ),
                    ],
            ),
          ],
        ),
      ),
    );
  }
}

/// Nagłówek z miejscem dostawy; dotknięcie → arkusz wyboru (adres = 2 dotknięcia łącznie).
class _PlaceHeader extends ConsumerWidget {
  const _PlaceHeader();

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final postal = ref.watch(postalFilterProvider);
    final label = ref.watch(postalLabelProvider);
    final located = ref.watch(myLocationProvider) != null;
    final place = postal == null ? 'Wybierz kod pocztowy' : (label == null ? postal : '$label · $postal');
    return ZzPlaceHeader(
      place: place,
      note: located ? 'najbliższe sklepy na górze' : null,
      onTap: () => _choose(context, ref),
    );
  }

  Future<void> _choose(BuildContext context, WidgetRef ref) async {
    final pick = await DeliveryPlaceSheet.show(context);
    if (pick == null) return;
    if (pick.location != null) {
      if (!pick.location!) {
        ref.read(myLocationProvider.notifier).state = null;
        return;
      }
      final loc = await _locationService.current();
      if (loc == null) {
        if (context.mounted) {
          ScaffoldMessenger.of(context).showSnackBar(
            const SnackBar(content: Text('Nie udało się ustalić lokalizacji. Sprawdź zgodę na lokalizację.')),
          );
        }
        return;
      }
      ref.read(myLocationProvider.notifier).state = loc;
      return;
    }
    ref.read(postalFilterProvider.notifier).state = pick.postal;
    ref.read(postalLabelProvider.notifier).state = pick.postal == null ? null : pick.label;
  }
}

/// Najbliższa runda spośród widocznych sklepów przyjmujących zamówienia (te same wpisy co na kartach).
class _NextRoundNotice extends ConsumerWidget {
  final List<Store> stores;
  const _NextRoundNotice({required this.stores});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    (Store, RoundPreview)? best;
    for (final s in stores.where((s) => s.isAcceptingOrders).take(_roundLookups)) {
      final p = ref.watch(storeRoundProvider(s.id)).valueOrNull;
      final r = p?.round;
      if (p == null || !p.available || r == null) continue;
      if (best == null || r.cutoffAtUtc.isBefore(best.$2.round!.cutoffAtUtc)) best = (s, p);
    }
    if (best == null) return const SizedBox.shrink();
    final (store, preview) = best;
    final r = preview.round!;
    final delivery = preview.earliestDeliveryLabel;
    return Padding(
      padding: const EdgeInsets.only(bottom: ZzSpace.sm),
      child: ZzNotice(
        kind: ZzNoticeKind.info,
        title: stores.length > 1 ? 'Najbliższa runda · ${store.name}' : 'Najbliższa runda zakupów',
        message: '${cutoffPhrase(r)}${delivery == null ? '' : ', dostawa od ${_lowerFirst(delivery)}'}.',
      ),
    );
  }
}

/// Ile sklepów pytamy o rundę (karta + baner). Pilot ma kilka sklepów; przy dużej liście karty poniżej tej liczby
/// pokazują się bez linii terminu, zamiast wysyłać kilkadziesiąt zapytań przy każdym wejściu na Start.
const _roundLookups = 12;

class _StoreGrid extends ConsumerWidget {
  final List<Store> stores;
  const _StoreGrid({required this.stores});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    return LayoutBuilder(
      builder: (context, c) {
        // Telefon: lista; szerzej: kolumny kart (maks. 520 px) o naturalnej wysokości.
        final cols = c.maxWidth >= 700 - 2 * ZzSpace.screen ? (c.maxWidth / 520).ceil().clamp(2, 3) : 1;
        final cardWidth = (c.maxWidth - (cols - 1) * ZzSpace.md) / cols;
        final lookups = stores.where((s) => s.isAcceptingOrders).take(_roundLookups).map((s) => s.id).toSet();
        return Wrap(
          spacing: ZzSpace.md,
          runSpacing: ZzSpace.md,
          children: [
            for (final store in stores)
              SizedBox(
                width: cardWidth,
                child: ZzStoreCard(
                  store: store,
                  roundLine: lookups.contains(store.id) ? _line(ref.watch(storeRoundProvider(store.id))) : null,
                  onTap: () => context.push('/stores/${store.id}'),
                ),
              ),
          ],
        );
      },
    );
  }
}

String? _line(AsyncValue<RoundPreview> v) {
  final p = v.valueOrNull;
  return p == null ? null : roundLine(p);
}

String _lowerFirst(String s) => s.isEmpty ? s : s[0].toLowerCase() + s.substring(1);

Widget _noStores(bool filtered) => EmptyView(
  svgAsset: 'assets/svg/illustrations/state_no_stores.svg',
  icon: Icons.storefront_outlined,
  title: filtered ? 'Brak sklepów dowożących' : 'Brak sklepów',
  subtitle: filtered
      ? 'Żaden sklep nie dowozi jeszcze pod ten kod pocztowy. Zmień kod w nagłówku albo spróbuj później.'
      : 'W Twojej okolicy nie ma jeszcze aktywnych sklepów.',
);
