import 'package:flutter/foundation.dart' show kIsWeb;

import '../../core/api/api_client.dart';
import '../../core/config/app_config.dart';

class FeedbackRepository {
  final ApiClient _api;
  FeedbackRepository(this._api);

  /// Wysyła uwagę użytkownika. Dołącza kontekst (ekran, wersja, platforma) dla zespołu.
  Future<void> submit({
    required String type,
    required String message,
    String? contactEmail,
    String? storeId,
    String? screen,
    String? captchaToken,
  }) async {
    final body = <String, dynamic>{
      'type': type,
      'message': message,
      'appVersion': AppConfig.appVersion,
      'platform': kIsWeb ? 'web' : 'mobile',
    };
    final email = contactEmail?.trim();
    if (email != null && email.isNotEmpty) body['contactEmail'] = email;
    if (storeId != null) body['storeId'] = storeId;
    if (screen != null) body['screen'] = screen;
    if (captchaToken != null && captchaToken.isNotEmpty) body['captchaToken'] = captchaToken;
    await _api.post('/feedback', body: body);
  }
}
