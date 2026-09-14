import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/api/api_exception.dart';
import '../../core/providers.dart';
import '../../core/theme/zz_theme.dart';

/// Ekran „Zgłoś uwagę" — użytkownik pisze, co poprawić. Dołączamy kontekst dla zespołu.
class FeedbackScreen extends ConsumerStatefulWidget {
  final String? screen;
  const FeedbackScreen({super.key, this.screen});

  @override
  ConsumerState<FeedbackScreen> createState() => _FeedbackScreenState();
}

class _FeedbackScreenState extends ConsumerState<FeedbackScreen> {
  final _message = TextEditingController();
  final _email = TextEditingController();
  String _type = 'Idea';
  bool _sending = false;

  @override
  void initState() {
    super.initState();
    _email.text = ref.read(authControllerProvider).user?.email ?? '';
  }

  @override
  void dispose() {
    _message.dispose();
    _email.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    if (_message.text.trim().isEmpty) {
      _snack('Napisz, co chcesz zgłosić.');
      return;
    }
    setState(() => _sending = true);
    try {
      await ref.read(feedbackRepositoryProvider).submit(
            type: _type,
            message: _message.text,
            contactEmail: _email.text,
            screen: widget.screen,
          );
      if (!mounted) return;
      _snack('Dziękujemy! Uwaga została wysłana.');
      context.pop();
    } on ApiException catch (e) {
      _snack(e.message);
    } finally {
      if (mounted) setState(() => _sending = false);
    }
  }

  void _snack(String m) =>
      ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(m)));

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Zgłoś uwagę')),
      body: ListView(
        padding: const EdgeInsets.all(16),
        children: [
          Text('Co możemy poprawić?',
              style: Theme.of(context).textTheme.titleMedium),
          const SizedBox(height: 4),
          Text('Twoja uwaga pomaga nam ulepszać aplikację.',
              style: TextStyle(color: context.zz.textMuted)),
          const SizedBox(height: 16),
          SizedBox(
            width: double.infinity,
            child: SegmentedButton<String>(
              showSelectedIcon: false,
              segments: const [
                ButtonSegment(value: 'Bug', label: Text('Błąd')),
                ButtonSegment(value: 'Idea', label: Text('Pomysł')),
                ButtonSegment(value: 'Other', label: Text('Inne')),
              ],
              selected: {_type},
              onSelectionChanged: (s) => setState(() => _type = s.first),
            ),
          ),
          const SizedBox(height: 16),
          TextField(
            controller: _message,
            minLines: 4,
            maxLines: 8,
            maxLength: 4000,
            decoration: const InputDecoration(
              labelText: 'Treść uwagi',
              hintText: 'Opisz problem lub pomysł…',
              alignLabelWithHint: true,
            ),
          ),
          const SizedBox(height: 8),
          TextField(
            controller: _email,
            keyboardType: TextInputType.emailAddress,
            decoration: const InputDecoration(
              labelText: 'E-mail kontaktowy (opcjonalnie)',
              hintText: 'gdybyśmy chcieli dopytać',
            ),
          ),
          const SizedBox(height: 20),
          ElevatedButton(
            onPressed: _sending ? null : _submit,
            child: _sending
                ? const SizedBox(
                    height: 22,
                    width: 22,
                    child: CircularProgressIndicator(strokeWidth: 2, color: Colors.white))
                : const Text('Wyślij uwagę'),
          ),
        ],
      ),
    );
  }
}
