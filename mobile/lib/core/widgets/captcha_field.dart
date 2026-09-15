import 'package:cloudflare_turnstile/cloudflare_turnstile.dart';
import 'package:flutter/material.dart';

/// Widget captchy Cloudflare Turnstile. Renderowany tylko, gdy captcha jest włączona
/// (site key niepusty). Zwraca token przez [onToken] (null gdy wygasł/błąd).
class CaptchaField extends StatelessWidget {
  final String? siteKey;
  final ValueChanged<String?> onToken;
  const CaptchaField({super.key, required this.siteKey, required this.onToken});

  @override
  Widget build(BuildContext context) {
    final key = siteKey;
    if (key == null || key.isEmpty) return const SizedBox.shrink();
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 12),
      child: CloudflareTurnstile(
        siteKey: key,
        onTokenReceived: (token) => onToken(token),
        onTokenExpired: () => onToken(null),
        onError: (_) => onToken(null),
      ),
    );
  }
}
