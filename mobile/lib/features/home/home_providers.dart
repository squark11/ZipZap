import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/location/location_service.dart';
import '../../core/providers.dart';
import '../../core/util/format.dart';
import '../../models/purchasing_round.dart';
import '../../models/store.dart';

/// Lokalizacja klienta wybrana do sortowania sklepów wg odległości (null = nieustalona).
final myLocationProvider = StateProvider<LatLng?>((ref) => null);

/// Kod pocztowy filtrujący sklepy do tych, które pod niego dowożą (null = wszystkie).
final postalFilterProvider = StateProvider<String?>((ref) => null);

/// Nazwa zapisanego adresu, z którego wzięto kod pocztowy („Dom") — tylko do wyświetlenia w nagłówku.
final postalLabelProvider = StateProvider<String?>((ref) => null);

/// Czy kod pocztowy został już raz auto-uzupełniony z domyślnego adresu (blokada powtórki).
final postalAutoFilledProvider = StateProvider<bool>((ref) => false);

final storesProvider = FutureProvider.autoDispose<List<Store>>((ref) {
  final loc = ref.watch(myLocationProvider);
  final postal = ref.watch(postalFilterProvider);
  return ref.read(catalogRepositoryProvider).listStores(lat: loc?.lat, lng: loc?.lng, postalCode: postal);
});

/// Najbliższa runda zakupowa sklepu (publiczny endpoint). Karta sklepu i baner na Starcie czytają ten sam
/// wpis, więc na sklep idzie jedno zapytanie.
final storeRoundProvider = FutureProvider.autoDispose.family<RoundPreview, String>(
  (ref, storeId) => ref.read(orderingRepositoryProvider).getRoundPreview(storeId),
);

/// „31042" / „31-042" → „31-042"; inaczej null.
String? normalizePostal(String raw) {
  final d = raw.replaceAll(RegExp(r'\D'), '');
  return d.length == 5 ? '${d.substring(0, 2)}-${d.substring(2)}' : null;
}

/// Krótka linia terminu na karcie sklepu: „Zamów do 11:30 · dostawa od dziś, 17:00" (null gdy brak rundy).
String? roundLine(RoundPreview p) {
  final r = p.round;
  if (!p.available || r == null) return null;
  final delivery = p.earliestDeliveryLabel;
  final cutoff = cutoffPhrase(r);
  return delivery == null ? cutoff : '$cutoff · dostawa od ${_lowerFirst(delivery)}';
}

/// Termin graniczny zawsze z dniem (na Starcie nie ma obok daty rundy): „Zamów do 11:30" (dziś),
/// „Zamów jutro do 11:30", „Zamów do 27.11, 11:30".
String cutoffPhrase(PurchasingRound r) {
  final t = r.cutoffLocalTime.length >= 5 ? r.cutoffLocalTime.substring(0, 5) : r.cutoffLocalTime;
  return switch (friendlyDate(r.cutoffLocalDate)) {
    'Dziś' => 'Zamów do $t',
    'Jutro' => 'Zamów jutro do $t',
    final d => 'Zamów do $d, $t',
  };
}

String _lowerFirst(String s) => s.isEmpty ? s : s[0].toLowerCase() + s.substring(1);
