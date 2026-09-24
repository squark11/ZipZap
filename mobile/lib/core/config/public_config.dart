/// Publiczna konfiguracja platformy (z `/api/config/public`) — jawne wartości dla klienta.
class PublicConfig {
  final String? googleClientId;
  final String? captchaProvider;
  final String? captchaSiteKey;

  /// `online` (bramka) albo `test` (pilotaż: zamówienia testowe bez opłaty, tylko dla testerów).
  final String paymentMode;

  const PublicConfig({
    this.googleClientId,
    this.captchaProvider,
    this.captchaSiteKey,
    this.paymentMode = 'online',
  });

  bool get captchaEnabled =>
      (captchaProvider ?? '').isNotEmpty && (captchaSiteKey ?? '').isNotEmpty;

  bool get isTestOrdering => paymentMode == 'test';

  factory PublicConfig.fromJson(Map<String, dynamic> j) => PublicConfig(
        googleClientId: j['googleClientId'] as String?,
        captchaProvider: j['captchaProvider'] as String?,
        captchaSiteKey: j['captchaSiteKey'] as String?,
        paymentMode: (j['paymentMode'] ?? 'online').toString(),
      );
}
