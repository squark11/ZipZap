import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/api/api_exception.dart';
import '../../core/providers.dart';
import '../../core/theme/zz_theme.dart';

class ChangePasswordScreen extends ConsumerStatefulWidget {
  const ChangePasswordScreen({super.key});

  @override
  ConsumerState<ChangePasswordScreen> createState() => _ChangePasswordScreenState();
}

class _ChangePasswordScreenState extends ConsumerState<ChangePasswordScreen> {
  final _current = TextEditingController();
  final _next = TextEditingController();
  final _confirm = TextEditingController();
  bool _busy = false;
  bool _obscure = true;

  @override
  void dispose() {
    _current.dispose();
    _next.dispose();
    _confirm.dispose();
    super.dispose();
  }

  void _snack(String m) =>
      ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(m)));

  Future<void> _submit() async {
    if (_current.text.isEmpty || _next.text.isEmpty) {
      _snack('Podaj obecne i nowe hasło.');
      return;
    }
    if (_next.text.length < 6) {
      _snack('Nowe hasło musi mieć co najmniej 6 znaków.');
      return;
    }
    if (_next.text != _confirm.text) {
      _snack('Hasła nie są takie same.');
      return;
    }
    setState(() => _busy = true);
    try {
      await ref.read(authRepositoryProvider).changePassword(_current.text, _next.text);
      if (mounted) {
        _snack('Hasło zmienione.');
        context.pop();
      }
    } on ApiException catch (e) {
      _snack(e.message);
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('Zmiana hasła'),
        actions: [
          IconButton(
            tooltip: _obscure ? 'Pokaż hasła' : 'Ukryj hasła',
            icon: Icon(_obscure ? Icons.visibility_outlined : Icons.visibility_off_outlined),
            onPressed: () => setState(() => _obscure = !_obscure),
          ),
        ],
      ),
      body: ListView(
        padding: const EdgeInsets.all(24),
        children: [
          const SizedBox(height: 8),
          Text('Zmień hasło', style: Theme.of(context).textTheme.headlineSmall),
          const SizedBox(height: 4),
          Text('Po zmianie zostaniesz wylogowany na pozostałych urządzeniach.',
              style: TextStyle(color: context.zz.textMuted)),
          const SizedBox(height: 24),
          TextField(
            controller: _current,
            obscureText: _obscure,
            decoration: const InputDecoration(labelText: 'Obecne hasło'),
          ),
          const SizedBox(height: 14),
          TextField(
            controller: _next,
            obscureText: _obscure,
            decoration: const InputDecoration(labelText: 'Nowe hasło', helperText: 'Min. 6 znaków'),
          ),
          const SizedBox(height: 14),
          TextField(
            controller: _confirm,
            obscureText: _obscure,
            decoration: const InputDecoration(labelText: 'Powtórz nowe hasło'),
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
                : const Text('Zapisz nowe hasło'),
          ),
        ],
      ),
    );
  }
}
