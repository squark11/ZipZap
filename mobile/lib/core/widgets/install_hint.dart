import 'package:flutter/foundation.dart';
import 'package:flutter/material.dart';

import '../theme/zz_theme.dart';
import '../web/display_mode.dart';

/// Przycisk „Dodaj do ekranu głównego" — tylko w przeglądarce i tylko gdy strona nie jest już skrótem.
/// Nic nie instaluje i niczego nie wymaga: pokazuje krótką instrukcję, jeśli klient sam chce mieć skrót.
class InstallHintButton extends StatelessWidget {
  /// Testy: pokaż także poza przeglądarką.
  final bool forceShow;
  const InstallHintButton({super.key, this.forceShow = false});

  @override
  Widget build(BuildContext context) {
    if (!(kIsWeb || forceShow) || isRunningStandalone()) return const SizedBox.shrink();
    return IconButton(
      tooltip: 'Dodaj do ekranu głównego',
      icon: const Icon(Icons.add_to_home_screen),
      onPressed: () => showInstallHint(context),
    );
  }
}

Future<void> showInstallHint(BuildContext context) => showModalBottomSheet<void>(
      context: context,
      isScrollControlled: true,
      showDragHandle: true,
      builder: (_) => const InstallHintSheet(),
    );

class InstallHintSheet extends StatelessWidget {
  const InstallHintSheet({super.key});

  static const _iphone = _Guide('iPhone — Safari', [
    'Stuknij „Udostępnij" (kwadrat ze strzałką w górę) na dole ekranu.',
    'Przewiń i wybierz „Do ekranu początkowego".',
    'Stuknij „Dodaj". Skrót otworzy tę stronę jak aplikację.',
  ], 'W innej przeglądarce na iPhonie najpierw otwórz tę stronę w Safari.');

  static const _android = _Guide('Android — Chrome', [
    'Stuknij menu ⋮ w prawym górnym rogu.',
    'Wybierz „Dodaj do ekranu głównego" albo „Zainstaluj aplikację".',
    'Potwierdź „Dodaj".',
  ], null);

  @override
  Widget build(BuildContext context) {
    // Najpierw instrukcja dla telefonu, na którym klient właśnie jest.
    final guides = defaultTargetPlatform == TargetPlatform.iOS ? const [_iphone, _android] : const [_android, _iphone];
    return SafeArea(
      child: SingleChildScrollView(
        padding: const EdgeInsets.fromLTRB(20, 0, 20, 20),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            const Text('Dodaj Dowózkę do ekranu głównego',
                style: TextStyle(fontSize: 18, fontWeight: FontWeight.w700)),
            const SizedBox(height: 6),
            Text('Nie musisz niczego instalować ze sklepu z aplikacjami — to tylko skrót do tej strony. '
                'Zamawiać możesz też bez niego, prosto z przeglądarki.',
                style: TextStyle(color: context.zz.textMuted)),
            for (final g in guides) ...[
              const SizedBox(height: 18),
              Text(g.title, style: const TextStyle(fontWeight: FontWeight.w700, fontSize: 15)),
              const SizedBox(height: 6),
              for (var i = 0; i < g.steps.length; i++)
                Padding(
                  padding: const EdgeInsets.only(bottom: 4),
                  child: Row(crossAxisAlignment: CrossAxisAlignment.start, children: [
                    SizedBox(width: 22, child: Text('${i + 1}.', style: const TextStyle(fontWeight: FontWeight.w600))),
                    Expanded(child: Text(g.steps[i])),
                  ]),
                ),
              if (g.note != null)
                Text(g.note!, style: TextStyle(color: context.zz.textMuted, fontSize: 13)),
            ],
            const SizedBox(height: 12),
            Align(
              alignment: Alignment.centerRight,
              child: TextButton(onPressed: () => Navigator.of(context).pop(), child: const Text('Zamknij')),
            ),
          ],
        ),
      ),
    );
  }
}

class _Guide {
  final String title;
  final List<String> steps;
  final String? note;
  const _Guide(this.title, this.steps, this.note);
}
