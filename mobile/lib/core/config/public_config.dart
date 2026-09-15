/// Publiczna konfiguracja platformy (z `/api/config/public`) — jawne wartości dla klienta.
class PublicConfig {
  final String? googleClientId;
  final String? captchaProvider;
  final String? captchaSiteKey;

  const PublicConfig({this.googleClientId, this.captchaProvider, this.captchaSiteKey});

  bool get captchaEnabled =>
      (captchaProvider ?? '').isNotEmpty && (captchaSiteKey ?? '').isNotEmpty;

  factory PublicConfig.fromJson(Map<String, dynamic> j) => PublicConfig(
        googleClientId: j['googleClientId'] as String?,
        captchaProvider: j['captchaProvider'] as String?,
        captchaSiteKey: j['captchaSiteKey'] as String?,
      );
}
