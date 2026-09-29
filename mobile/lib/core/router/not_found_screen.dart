import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import '../widgets/states.dart';

/// Nieznany adres (np. przepisany ręcznie lub nieaktualny link) — zamiast technicznego błędu routera.
class NotFoundScreen extends StatelessWidget {
  const NotFoundScreen({super.key});

  @override
  Widget build(BuildContext context) => Scaffold(
        appBar: AppBar(title: const Text('Dowózka.pl')),
        body: Column(
          mainAxisAlignment: MainAxisAlignment.center,
          children: [
            const EmptyView(
              icon: Icons.link_off,
              title: 'Nie ma takiej strony',
              subtitle: 'Link może być nieaktualny. Sprawdź dostępne sklepy.',
            ),
            ElevatedButton(
              onPressed: () => context.go('/stores'),
              child: const Text('Zobacz sklepy'),
            ),
          ],
        ),
      );
}
