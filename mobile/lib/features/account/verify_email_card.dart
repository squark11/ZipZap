import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/api/api_exception.dart';
import '../../core/providers.dart';
import '../../core/theme/zz_theme.dart';

/// Stan potwierdzenia adresu e-mail zalogowanego konta (z API — aktualny także po potwierdzeniu na innym urządzeniu).
/// `null` = nie wiadomo (brak sieci, starsze API) — wtedy karta się nie pokazuje.
final emailVerifiedProvider = FutureProvider.autoDispose<bool?>((ref) async {
  if (!ref.watch(authControllerProvider).isAuthenticated) return null;
  try {
    return await ref.read(authRepositoryProvider).isEmailVerified();
  } catch (_) {
    return null;
  }
});

/// Karta na ekranie „Konto": przypomina o linku potwierdzającym z rejestracji i pozwala wysłać go ponownie
/// (istniejący endpoint; limit linków na konto pilnuje serwer, tu tylko krótka blokada przycisku).
class VerifyEmailCard extends ConsumerStatefulWidget {
  final String email;
  const VerifyEmailCard({super.key, required this.email});

  @override
  ConsumerState<VerifyEmailCard> createState() => _VerifyEmailCardState();
}

class _VerifyEmailCardState extends ConsumerState<VerifyEmailCard> {
  static const resendAfter = 60;

  bool _busy = false;
  bool _sent = false;
  String? _error;
  int _cooldown = 0;
  Timer? _timer;

  @override
  void dispose() {
    _timer?.cancel();
    super.dispose();
  }

  Future<void> _resend() async {
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      final sent = await ref.read(authRepositoryProvider).resendVerification();
      if (!mounted) return;
      if (!sent) {
        ref.invalidate(emailVerifiedProvider); // adres potwierdzony w międzyczasie — karta znika
        return;
      }
      setState(() => _sent = true);
      _startCooldown();
    } on ApiException catch (e) {
      if (mounted) setState(() => _error = _message(e));
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  String _message(ApiException e) {
    if (e.code == 'validation.verify_resend_limit') {
      return 'Wysłaliśmy już kilka linków w ciągu ostatniej godziny. Sprawdź skrzynkę (także Spam) albo spróbuj później.';
    }
    if (e.isNetwork) return 'Nie udało się połączyć z serwerem — sprawdź internet i spróbuj ponownie.';
    if (e.statusCode == 429) return 'Zbyt wiele prób. Spróbuj ponownie za minutę.';
    return 'Nie udało się wysłać linku — spróbuj ponownie.';
  }

  void _startCooldown() {
    _timer?.cancel();
    setState(() => _cooldown = resendAfter);
    _timer = Timer.periodic(const Duration(seconds: 1), (t) {
      if (!mounted) return t.cancel();
      setState(() => _cooldown = _cooldown > 0 ? _cooldown - 1 : 0);
      if (_cooldown == 0) t.cancel();
    });
  }

  @override
  Widget build(BuildContext context) {
    final mailIsMock = ref.watch(publicConfigProvider).valueOrNull?.mailIsMock ?? false;
    const accent = Color(0xFFB45309);
    const body = TextStyle(color: Color(0xFF6B3E09), height: 1.4);
    return Container(
      key: const Key('verify-email-card'),
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        color: const Color(0xFFFFF8EB),
        borderRadius: BorderRadius.circular(ZzRadius.lg),
        border: Border.all(color: const Color(0xFFF7D9B9)),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(_sent ? 'Link wysłany' : 'Potwierdź adres e-mail',
              style: const TextStyle(fontWeight: FontWeight.w600, fontSize: 15, color: accent)),
          const SizedBox(height: 6),
          Text(
            _sent
                ? 'Sprawdź skrzynkę ${widget.email} (także folder Spam) i otwórz link z wiadomości.'
                : 'Po rejestracji wysłaliśmy link na ${widget.email}. Otwórz go, aby potwierdzić adres.',
            style: body,
          ),
          if (mailIsMock) ...[
            const SizedBox(height: 6),
            const Text('Poczta w tym środowisku to atrapa — wiadomości nie są wysyłane.', style: body),
          ],
          if (_error != null) ...[
            const SizedBox(height: 8),
            Semantics(liveRegion: true, child: Text(_error!, style: const TextStyle(color: ZzColors.danger))),
          ],
          const SizedBox(height: 12),
          OutlinedButton(
            onPressed: _busy || _cooldown > 0 ? null : _resend,
            child: Text(_busy
                ? 'Wysyłanie…'
                : _cooldown > 0
                    ? 'Wyślij ponownie za $_cooldown s'
                    : 'Wyślij link ponownie'),
          ),
        ],
      ),
    );
  }
}
