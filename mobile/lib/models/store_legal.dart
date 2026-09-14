/// Dokumenty prawne sklepu (adresy URL) + wymóg akceptacji przez klienta przed zakupem.
class StoreLegal {
  final String? termsUrl;
  final String? privacyUrl;
  final String? gdprUrl;
  final bool requiresAcceptance;

  const StoreLegal({
    this.termsUrl,
    this.privacyUrl,
    this.gdprUrl,
    this.requiresAcceptance = false,
  });

  bool get hasAnyDoc =>
      (termsUrl != null && termsUrl!.isNotEmpty) ||
      (privacyUrl != null && privacyUrl!.isNotEmpty) ||
      (gdprUrl != null && gdprUrl!.isNotEmpty);

  factory StoreLegal.fromJson(Map<String, dynamic> j) => StoreLegal(
        termsUrl: j['termsUrl'] as String?,
        privacyUrl: j['privacyUrl'] as String?,
        gdprUrl: j['gdprUrl'] as String?,
        requiresAcceptance: (j['requiresAcceptance'] as bool?) ?? false,
      );
}
