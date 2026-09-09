import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/api/api_exception.dart';
import '../../core/providers.dart';
import '../../core/theme/zz_theme.dart';

class ForgotPasswordScreen extends ConsumerStatefulWidget {
  const ForgotPasswordScreen({super.key});

  @override
  ConsumerState<ForgotPasswordScreen> createState() => _ForgotPasswordScreenState();
}

class _ForgotPasswordScreenState extends ConsumerState<ForgotPasswordScreen> {
  final _email = TextEditingController();
  bool _busy = false;
  bool _sent = false;

  @override
  void dispose() {
    _email.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    if (_email.text.trim().isEmpty) {
      ScaffoldMessenger.of(context)
          .showSnackBar(const SnackBar(content: Text('Podaj adres e-mail.')));
      return;
    }
    setState(() => _busy = true);
    try {
      await ref.read(authRepositoryProvider).forgotPassword(_email.text.trim());
      if (mounted) setState(() => _sent = true);
    } on ApiException catch (e) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(e.message)));
      }
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Odzyskiwanie hasła')),
      body: ListView(
        padding: const EdgeInsets.all(24),
        children: [
          if (_sent) ...[
            const SizedBox(height: 12),
            const Icon(Icons.mark_email_read_outlined, size: 56, color: ZzColors.green),
            const SizedBox(height: 12),
            Text('Sprawdź skrzynkę', style: Theme.of(context).textTheme.headlineSmall),
            const SizedBox(height: 8),
            const Text(
              'Jeśli konto o tym adresie istnieje, wysłaliśmy link do zresetowania hasła.',
              style: TextStyle(color: ZzColors.textMuted),
            ),
          ] else ...[
            const SizedBox(height: 8),
            Text('Zresetuj hasło', style: Theme.of(context).textTheme.headlineSmall),
            const SizedBox(height: 4),
            const Text('Podaj e-mail konta — wyślemy link do zmiany hasła.',
                style: TextStyle(color: ZzColors.textMuted)),
            const SizedBox(height: 24),
            TextField(
              controller: _email,
              keyboardType: TextInputType.emailAddress,
              decoration: const InputDecoration(labelText: 'E-mail'),
              onSubmitted: (_) => _submit(),
            ),
            const SizedBox(height: 24),
            ElevatedButton(
              onPressed: _busy ? null : _submit,
              child: _busy
                  ? const SizedBox(
                      height: 22,
                      width: 22,
                      child: CircularProgressIndicator(strokeWidth: 2, color: Colors.white))
                  : const Text('Wyślij link'),
            ),
          ],
        ],
      ),
    );
  }
}
