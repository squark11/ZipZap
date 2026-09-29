import 'dart:async';

import '../../core/api/api_client.dart';

/// Pomiar wejść na kartę sklepu z kodu QR / kampanii (`?src=…` w stałym linku).
/// Tylko licznik: źródło NIE daje uprawnień, nie zmienia cen ani ścieżki zamówienia. Wysyłane raz na sesję
/// aplikacji dla pary sklep + źródło; błąd sieci nie przeszkadza klientowi (pomiar jest „najlepszej próby").
class StoreEntryRecorder {
  static const maxSourceLength = 40;

  final ApiClient _api;
  final Set<String> _sent = {};

  StoreEntryRecorder(this._api);

  /// Zwraca true, jeśli wejście zostało wysłane (pierwszy raz w tej sesji).
  bool record(String idOrSlug, String? source) {
    final src = normalize(source);
    if (src == null || idOrSlug.isEmpty) return false;
    if (!_sent.add('$idOrSlug|$src')) return false;
    unawaited(_api
        .post('/catalog/stores/${Uri.encodeComponent(idOrSlug)}/entries', body: {'source': src})
        .then((_) {}, onError: (_) {}));
    return true;
  }

  /// Wstępne przycięcie etykiety (serwer i tak ją normalizuje): bez białych znaków, maks. 40 znaków.
  static String? normalize(String? raw) {
    final v = raw?.trim().toLowerCase() ?? '';
    if (v.isEmpty) return null;
    return v.length > maxSourceLength ? v.substring(0, maxSourceLength) : v;
  }
}
