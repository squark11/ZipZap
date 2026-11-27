import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/providers.dart';
import '../../core/theme/zz_theme.dart';
import '../../core/widgets/zz_icon.dart';
import '../account/addresses_screen.dart';
import 'home_providers.dart';

/// Wybór z arkusza „Dokąd dowieźć?".
class PlacePick {
  /// Kod „31-042" albo null = pokaż wszystkie sklepy.
  final String? postal;

  /// Nazwa zapisanego adresu (np. „Dom"), gdy wybrano adres.
  final String? label;

  /// Ustawione = włącz (true) / wyłącz (false) sortowanie wg lokalizacji; kod bez zmian.
  final bool? location;
  const PlacePick({this.postal, this.label, this.location});
}

/// Arkusz wyboru miejsca dostawy: zapisane adresy (1 dotknięcie), inny kod pocztowy, sortowanie wg odległości.
/// Logika filtrowania jest ta sama co w dawnych banerach — zmienia się tylko sposób wyboru.
class DeliveryPlaceSheet extends ConsumerStatefulWidget {
  const DeliveryPlaceSheet({super.key});

  static Future<PlacePick?> show(BuildContext context) => showModalBottomSheet<PlacePick>(
    context: context,
    isScrollControlled: true,
    showDragHandle: true,
    builder: (_) => const DeliveryPlaceSheet(),
  );

  @override
  ConsumerState<DeliveryPlaceSheet> createState() => _DeliveryPlaceSheetState();
}

class _DeliveryPlaceSheetState extends ConsumerState<DeliveryPlaceSheet> {
  late final _code = TextEditingController(text: ref.read(postalFilterProvider) ?? '');
  String? _error;

  @override
  void dispose() {
    _code.dispose();
    super.dispose();
  }

  void _submitCode() {
    final code = normalizePostal(_code.text);
    if (code == null) {
      setState(() => _error = 'Kod pocztowy ma 5 cyfr, np. 31-042.');
      return;
    }
    Navigator.pop(context, PlacePick(postal: code));
  }

  @override
  Widget build(BuildContext context) {
    final p = context.zz;
    final current = ref.watch(postalFilterProvider);
    final located = ref.watch(myLocationProvider) != null;
    final signedIn = ref.watch(authControllerProvider).isAuthenticated;
    final addresses = signedIn ? ref.watch(addressesProvider).valueOrNull : null;
    final usable = [
      for (final a in addresses ?? const [])
        if (normalizePostal(a.postalCode) != null) a,
    ];

    return SafeArea(
      child: Padding(
        padding: EdgeInsets.only(bottom: MediaQuery.viewInsetsOf(context).bottom),
        child: SingleChildScrollView(
          padding: const EdgeInsets.fromLTRB(ZzSpace.screen, 0, ZzSpace.screen, ZzSpace.lg),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            mainAxisSize: MainAxisSize.min,
            children: [
              Semantics(
                header: true,
                child: Text('Dokąd dowieźć zakupy?', style: Theme.of(context).textTheme.titleLarge),
              ),
              const SizedBox(height: ZzSpace.xs),
              Text(
                'Pokażemy tylko sklepy, które dowożą pod ten kod pocztowy.',
                style: TextStyle(color: p.textMuted, fontSize: 13),
              ),
              const SizedBox(height: ZzSpace.md),
              for (final a in usable)
                ListTile(
                  contentPadding: EdgeInsets.zero,
                  minTileHeight: zzMinTouch,
                  leading: ZzIcon('home', size: 22, color: p.brandStrong),
                  title: Text(a.label.isEmpty ? 'Adres' : a.label, style: const TextStyle(fontWeight: FontWeight.w600)),
                  subtitle: Text(a.oneLine, maxLines: 2, overflow: TextOverflow.ellipsis),
                  trailing: normalizePostal(a.postalCode) == current
                      ? Icon(Icons.check_circle, color: p.brandStrong)
                      : null,
                  onTap: () => Navigator.pop(
                    context,
                    PlacePick(postal: normalizePostal(a.postalCode), label: a.label.isEmpty ? null : a.label),
                  ),
                ),
              if (usable.isNotEmpty) const Divider(height: ZzSpace.lg),
              Text(
                usable.isEmpty ? 'Kod pocztowy' : 'Inny kod pocztowy',
                style: const TextStyle(fontWeight: FontWeight.w600),
              ),
              const SizedBox(height: ZzSpace.sm),
              Row(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Expanded(
                    child: TextField(
                      controller: _code,
                      keyboardType: TextInputType.number,
                      maxLength: 6,
                      textInputAction: TextInputAction.done,
                      onSubmitted: (_) => _submitCode(),
                      decoration: InputDecoration(hintText: '31-042', counterText: '', errorText: _error),
                    ),
                  ),
                  const SizedBox(width: ZzSpace.sm),
                  SizedBox(
                    height: 52,
                    child: FilledButton(onPressed: _submitCode, child: const Text('Pokaż')),
                  ),
                ],
              ),
              const SizedBox(height: ZzSpace.md),
              ListTile(
                contentPadding: EdgeInsets.zero,
                minTileHeight: zzMinTouch,
                leading: ZzIcon('location', size: 22, color: p.brandStrong),
                title: Text(located ? 'Wyłącz sortowanie wg odległości' : 'Najbliższe sklepy na górze'),
                subtitle: located ? null : const Text('Użyje lokalizacji z przeglądarki'),
                onTap: () => Navigator.pop(context, PlacePick(postal: current, location: !located)),
              ),
              if (current != null)
                ListTile(
                  contentPadding: EdgeInsets.zero,
                  minTileHeight: zzMinTouch,
                  leading: ZzIcon('store', size: 22, color: p.brandStrong),
                  title: const Text('Pokaż wszystkie sklepy'),
                  onTap: () => Navigator.pop(context, const PlacePick()),
                ),
            ],
          ),
        ),
      ),
    );
  }
}
