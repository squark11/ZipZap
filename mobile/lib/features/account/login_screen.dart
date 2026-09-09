import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/api/api_exception.dart';
import '../../core/config/app_config.dart';
import '../../core/providers.dart';
import '../../core/theme/zz_theme.dart';

class LoginScreen extends ConsumerStatefulWidget {
  final String? redirect;
  const LoginScreen({super.key, this.redirect});

  @override
  ConsumerState<LoginScreen> createState() => _LoginScreenState();
}

class _LoginScreenState extends ConsumerState<LoginScreen> {
  final _email = TextEditingController();
  final _password = TextEditingController();
  final _fullName = TextEditingController();
  bool _register = false;
  bool _busy = false;

  @override
  void dispose() {
    _email.dispose();
    _password.dispose();
    _fullName.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    if (_email.text.trim().isEmpty || _password.text.isEmpty) {
      _snack('Podaj e-mail i hasło.');
      return;
    }
    if (_register && _fullName.text.trim().isEmpty) {
      _snack('Podaj imię i nazwisko.');
      return;
    }
    setState(() => _busy = true);
    try {
      final ctrl = ref.read(authControllerProvider.notifier);
      if (_register) {
        await ctrl.register(_email.text.trim(), _password.text, _fullName.text.trim());
      } else {
        await ctrl.login(_email.text.trim(), _password.text);
      }
      if (mounted) context.go(widget.redirect ?? '/stores');
    } on ApiException catch (e) {
      _snack(e.message);
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  void _snack(String m) =>
      ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(m)));

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: Text(_register ? 'Rejestracja' : 'Logowanie')),
      body: ListView(
        padding: const EdgeInsets.all(24),
        children: [
          const SizedBox(height: 8),
          Text(_register ? 'Załóż konto' : 'Zaloguj się',
              style: Theme.of(context).textTheme.headlineSmall),
          const SizedBox(height: 4),
          const Text('Konto potrzebne jest przy składaniu zamówienia.',
              style: TextStyle(color: ZzColors.textMuted)),
          const SizedBox(height: 24),
          if (_register) ...[
            TextField(
              controller: _fullName,
              decoration: const InputDecoration(labelText: 'Imię i nazwisko'),
              textInputAction: TextInputAction.next,
            ),
            const SizedBox(height: 14),
          ],
          TextField(
            controller: _email,
            keyboardType: TextInputType.emailAddress,
            decoration: const InputDecoration(labelText: 'E-mail'),
            textInputAction: TextInputAction.next,
          ),
          const SizedBox(height: 14),
          TextField(
            controller: _password,
            obscureText: true,
            decoration: const InputDecoration(labelText: 'Hasło'),
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
                : Text(_register ? 'Zarejestruj się' : 'Zaloguj się'),
          ),
          const SizedBox(height: 12),
          OutlinedButton.icon(
            onPressed: AppConfig.googleSignInEnabled
                ? null
                : () => _snack('Logowanie Google będzie dostępne po konfiguracji.'),
            icon: const Icon(Icons.g_mobiledata, size: 28),
            label: const Text('Kontynuuj z Google'),
          ),
          const SizedBox(height: 20),
          Center(
            child: TextButton(
              onPressed: () => setState(() => _register = !_register),
              child: Text(_register
                  ? 'Masz już konto? Zaloguj się'
                  : 'Nie masz konta? Zarejestruj się'),
            ),
          ),
        ],
      ),
    );
  }
}
