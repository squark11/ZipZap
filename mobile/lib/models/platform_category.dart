/// Wspólna kategoria platformy (F-6), np. „Owoce". Klucz = nazwa ikony w `assets/svg/categories/`.
class PlatformCategory {
  final String key;
  final String name;

  const PlatformCategory({required this.key, required this.name});

  /// Ikony dołączone do aplikacji. Kategoria z nieznanym kluczem (nowa po stronie serwera, starsza aplikacja)
  /// dostaje ikonę ogólną zamiast pustego miejsca.
  static const knownIcons = {
    'owoce', 'warzywa', 'pieczywo', 'nabial', 'mieso', 'ryby', 'mrozonki', 'napoje', 'slodycze', 'chemia', 'dziecko',
  };

  String get icon => knownIcons.contains(key) ? key : 'wszystko';

  factory PlatformCategory.fromJson(Map<String, dynamic> j) =>
      PlatformCategory(key: (j['key'] ?? '').toString(), name: (j['name'] ?? '').toString());
}
