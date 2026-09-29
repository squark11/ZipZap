import 'dart:async';
import 'dart:convert';

import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../models/store.dart';
import '../providers.dart';
import '../router/redirects.dart';

/// Ostatnio oglądany sklep (np. otwarty z kodu QR). Po zalogowaniu bez wskazanego celu klient wraca do jego oferty,
/// a nie na listę wszystkich sklepów. Trwały — przeżywa odświeżenie strony i przejście przez logowanie.
class LastStore {
  final String id;
  final String slug;
  final String name;
  const LastStore({required this.id, required this.slug, required this.name});

  String get path => storePath(slug.isNotEmpty ? slug : id);

  Map<String, dynamic> toJson() => {'id': id, 'slug': slug, 'name': name};

  static LastStore? fromJson(Object? j) {
    if (j is! Map) return null;
    final id = j['id'], slug = j['slug'], name = j['name'];
    if (id is! String || id.isEmpty) return null;
    return LastStore(id: id, slug: slug is String ? slug : '', name: name is String ? name : '');
  }
}

class LastStoreController extends Notifier<LastStore?> {
  static const storageKey = 'zz_last_store';

  @override
  LastStore? build() {
    unawaited(_load());
    return null;
  }

  Future<void> _load() async {
    final raw = await ref.read(appStorageProvider).read(storageKey);
    if (raw == null || state != null) return;
    try {
      state = LastStore.fromJson(jsonDecode(raw));
    } catch (_) {}
  }

  /// Zapamiętuje sklep, którego ofertę klient właśnie ogląda.
  Future<void> remember(Store s) async {
    final next = LastStore(id: s.id, slug: s.slug, name: s.name);
    final cur = state;
    if (cur != null && cur.id == next.id && cur.slug == next.slug && cur.name == next.name) return;
    state = next;
    await ref.read(appStorageProvider).write(storageKey, jsonEncode(next.toJson()));
  }
}

final lastStoreProvider = NotifierProvider<LastStoreController, LastStore?>(LastStoreController.new);
