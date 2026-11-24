import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/theme/zz_theme.dart';
import '../../core/ui/ui.dart';
import '../../core/widgets/skeleton.dart';
import '../../core/widgets/states.dart';
import '../../models/store.dart';
import '../home/home_providers.dart';

/// Zakładka „Szukaj" (E2-6). Na razie szuka sklepów po nazwie i miejscowości (te same dane co „Start", bez nowego
/// zapytania do API). Wyszukiwarka produktów we wszystkich sklepach wymaga nowego endpointu — decyzja F-7 (E3-10).
class SearchScreen extends ConsumerStatefulWidget {
  const SearchScreen({super.key});

  @override
  ConsumerState<SearchScreen> createState() => _SearchScreenState();
}

class _SearchScreenState extends ConsumerState<SearchScreen> {
  final _query = TextEditingController();

  @override
  void dispose() {
    _query.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final stores = ref.watch(storesProvider);
    return Scaffold(
      appBar: AppBar(title: const Text('Szukaj')),
      body: Column(
        children: [
          Padding(
            padding: const EdgeInsets.fromLTRB(ZzSpace.screen, ZzSpace.sm, ZzSpace.screen, ZzSpace.md),
            child: ZzSearchField(
              controller: _query,
              hint: 'Nazwa sklepu albo miejscowość',
              onChanged: (_) => setState(() {}),
            ),
          ),
          Expanded(
            child: stores.when(
              loading: () => const StoreListSkeleton(),
              error: (e, _) => ErrorView(message: e.toString(), onRetry: () => ref.invalidate(storesProvider)),
              data: (list) {
                final found = searchStores(list, _query.text);
                if (found.isEmpty) {
                  return EmptyView(
                    svgAsset: 'assets/svg/illustrations/state_no_stores.svg',
                    icon: Icons.search_off,
                    title: _query.text.isEmpty ? 'Brak sklepów' : 'Nic nie znaleźliśmy',
                    subtitle: _query.text.isEmpty
                        ? 'W Twojej okolicy nie ma jeszcze aktywnych sklepów.'
                        : 'Sprawdź pisownię albo wpisz samą miejscowość.',
                  );
                }
                return ListView.separated(
                  padding: const EdgeInsets.fromLTRB(ZzSpace.screen, 0, ZzSpace.screen, ZzSpace.screen),
                  itemCount: found.length,
                  separatorBuilder: (_, _) => const SizedBox(height: ZzSpace.md),
                  itemBuilder: (_, i) => ZzStoreCard(
                    store: found[i],
                    onTap: () => context.push('/stores/${found[i].id}'),
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

String _norm(String s) {
  const from = 'ąćęłńóśźż', to = 'acelnoszz';
  final b = StringBuffer();
  for (final ch in s.toLowerCase().split('')) {
    final i = from.indexOf(ch);
    b.write(i >= 0 ? to[i] : ch);
  }
  return b.toString().trim();
}

/// Dopasowanie sklepów bez wielkości liter i polskich znaków („rabka" → „Rabka-Zdrój").
List<Store> searchStores(List<Store> stores, String query) {
  final q = _norm(query);
  if (q.isEmpty) return stores;
  return stores.where((s) => _norm('${s.name} ${s.city} ${s.address ?? ''}').contains(q)).toList();
}
