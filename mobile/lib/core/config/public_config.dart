/// Publiczna konfiguracja platformy (z `/api/config/public`) — jawne wartości dla klienta.
class PublicConfig {
  final String? googleClientId;
  final String? captchaProvider;
  final String? captchaSiteKey;

  /// `online` (bramka) albo `test` (pilotaż: zamówienia testowe bez opłaty, tylko dla testerów).
  final String paymentMode;

  /// `live` albo `mock` — atrapa poczty: e-maile (np. link resetu hasła) NIE są wysyłane.
  final String emailDelivery;

  const PublicConfig({
    this.googleClientId,
    this.captchaProvider,
    this.captchaSiteKey,
    this.paymentMode = 'online',
    this.emailDelivery = 'live',
  });

  bool get captchaEnabled =>
      (captchaProvider ?? '').isNotEmpty && (captchaSiteKey ?? '').isNotEmpty;

  bool get isTestOrdering => paymentMode == 'test';

  bool get mailIsMock => emailDelivery == 'mock';

  factory PublicConfig.fromJson(Map<String, dynamic> j) => PublicConfig(
        googleClientId: j['googleClientId'] as String?,
        captchaProvider: j['captchaProvider'] as String?,
        captchaSiteKey: j['captchaSiteKey'] as String?,
        paymentMode: (j['paymentMode'] ?? 'online').toString(),
        emailDelivery: (j['emailDelivery'] ?? 'live').toString(),
      );
}
