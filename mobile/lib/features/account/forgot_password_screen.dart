import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/api/api_exception.dart';
import '../../core/providers.dart';
import '../../core/theme/zz_theme.dart';

/// Prośba o link resetu hasła. Odpowiedź jest zawsze taka sama (nie zdradzamy, czy konto istnieje).
/// Link z e-maila otwiera wspólną stronę resetu Dowózka.pl w przeglądarce (ta sama dla kont klientów, sklepów
/// i kierowców) — po ustawieniu nowego hasła klient wraca do aplikacji i loguje się.
class ForgotPasswordScreen extends ConsumerStatefulWidget {
  final String? initialEmail;
  const ForgotPasswordScreen({super.key, this.initialEmail});

  @override
  ConsumerState<ForgotPasswordScreen> createState() => _ForgotPasswordScreenState();
}

class _ForgotPasswordScreenState extends ConsumerState<ForgotPasswordScreen> {
  static const resendAfter = 60;

  late final _email = TextEditingController(text: widget.initialEmail ?? '');
  bool _busy = false;
  String? _sentTo;
  String? _error;
  int _cooldown = 0;
  Timer? _timer;

  @override
  void dispose() {
    _timer?.cancel();
    _email.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    final email = _email.text.trim();
    if (email.isEmpty || !email.contains('@')) {
      setState(() => _error = 'Podaj poprawny adres e-mail.');
      return;
    }
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      await ref.read(authRepositoryProvider).forgotPassword(email);
      if (!mounted) return;
      setState(() => _sentTo = email);
      _startCooldown();
    } on ApiException catch (e) {
      if (mounted) setState(() => _error = _message(e));
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  String _message(ApiException e) {
    if (e.isNetwork) return 'Nie udało się połączyć z serwerem — sprawdź internet i spróbuj ponownie.';
    if (e.statusCode == 429) return 'Zbyt wiele prób. Spróbuj ponownie za minutę.';
    return 'Nie udało się wysłać prośby — spróbuj ponownie.';
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
    final muted = TextStyle(color: context.zz.textMuted, height: 1.45);
    final sentTo = _sentTo;
    return Scaffold(
      appBar: AppBar(title: const Text('Odzyskiwanie hasła')),
      body: ListView(
        padding: const EdgeInsets.all(24),
        children: [
          if (sentTo != null) ...[
            const SizedBox(height: 8),
            const Icon(Icons.mark_email_read_outlined, size: 56, color: ZzColors.orange),
            const SizedBox(height: 12),
            Text('Sprawdź skrzynkę', style: Theme.of(context).textTheme.headlineSmall),
            const SizedBox(height: 8),
            Text(
              'Jeśli istnieje konto z adresem $sentTo, wysłaliśmy na nie link do zmiany hasła. '
              'Link jest ważny 1 godzinę.',
              style: muted,
            ),
            const SizedBox(height: 16),
            if (mailIsMock) const _MockMailNotice(),
            _InfoBox(
              title: 'Co dalej?',
              lines: const [
                'Otwórz link z wiadomości — strona Dowózka.pl otworzy się w przeglądarce.',
                'Ustaw nowe hasło, wróć do aplikacji i zaloguj się.',
                'Nie widzisz wiadomości? Zajrzyj do folderu Spam lub Oferty.',
              ],
            ),
            const SizedBox(height: 20),
            OutlinedButton(
              onPressed: _busy || _cooldown > 0 ? null : _submit,
              child: Text(_cooldown > 0 ? 'Wyślij ponownie za $_cooldown s' : 'Wyślij ponownie'),
            ),
            if (_error != null) _ErrorText(_error!),
            const SizedBox(height: 8),
            Center(
              child: TextButton(
                onPressed: () => context.canPop() ? context.pop() : context.go('/login'),
                child: const Text('Wróć do logowania'),
              ),
            ),
          ] else ...[
            const SizedBox(height: 8),
            Text('Nie pamiętasz hasła?', style: Theme.of(context).textTheme.headlineSmall),
            const SizedBox(height: 6),
            Text(
              'Podaj e-mail konta — wyślemy link do ustawienia nowego hasła. '
              'Link jest ważny 1 godzinę i działa jeden raz.',
              style: muted,
            ),
            const SizedBox(height: 24),
            TextField(
              controller: _email,
              keyboardType: TextInputType.emailAddress,
              autofillHints: const [AutofillHints.email],
              decoration: const InputDecoration(labelText: 'E-mail'),
              textInputAction: TextInputAction.send,
              onSubmitted: (_) => _submit(),
            ),
            const SizedBox(height: 16),
            if (mailIsMock) const _MockMailNotice(),
            ElevatedButton(
              onPressed: _busy ? null : _submit,
              child: _busy
                  ? const SizedBox(
                      height: 22,
                      width: 22,
                      child: CircularProgressIndicator(strokeWidth: 2, color: Colors.white))
                  : const Text('Wyślij link'),
            ),
            if (_error != null) _ErrorText(_error!),
          ],
        ],
      ),
    );
  }
}

class _MockMailNotice extends StatelessWidget {
  const _MockMailNotice();

  @override
  Widget build(BuildContext context) {
    return Container(
      key: const Key('mock-mail-notice'),
      margin: const EdgeInsets.only(bottom: 16),
      padding: const EdgeInsets.all(12),
      decoration: BoxDecoration(
        color: const Color(0xFFFFF8EB),
        borderRadius: BorderRadius.circular(ZzRadius.md),
      ),
      child: const Text.rich(
        TextSpan(children: [
          TextSpan(
            text: 'Poczta w tym środowisku to atrapa\n',
            style: TextStyle(fontWeight: FontWeight.w600, color: Color(0xFFB45309)),
          ),
          TextSpan(
            text: 'Wiadomości nie są wysyłane, więc link nie dotrze. Skontaktuj się z obsługą Dowózka.pl.',
            style: TextStyle(color: Color(0xFF6B3E09), height: 1.4),
          ),
        ]),
      ),
    );
  }
}

class _InfoBox extends StatelessWidget {
  final String title;
  final List<String> lines;
  const _InfoBox({required this.title, required this.lines});

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(14),
      decoration: BoxDecoration(
        border: Border.all(color: context.zz.border),
        borderRadius: BorderRadius.circular(ZzRadius.md),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(title, style: const TextStyle(fontWeight: FontWeight.w600)),
          const SizedBox(height: 6),
          for (final l in lines)
            Padding(
              padding: const EdgeInsets.only(top: 4),
              child: Text('•  $l', style: TextStyle(color: context.zz.textMuted, height: 1.4)),
            ),
        ],
      ),
    );
  }
}

class _ErrorText extends StatelessWidget {
  final String text;
  const _ErrorText(this.text);

  @override
  Widget build(BuildContext context) => Padding(
        padding: const EdgeInsets.only(top: 12),
        child: Semantics(
          liveRegion: true,
          child: Text(text, style: const TextStyle(color: ZzColors.danger)),
        ),
      );
}
