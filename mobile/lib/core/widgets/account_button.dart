import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../providers.dart';
import 'zz_icon.dart';

/// Konto / logowanie z ekranu bez dolnej nawigacji (np. karta sklepu otwarta z kodu QR).
/// Logowanie wraca do bieżącego ekranu — klient zostaje przy ofercie wybranego sklepu.
class AccountButton extends ConsumerWidget {
  const AccountButton({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final signedIn = ref.watch(authControllerProvider).isAuthenticated;
    return IconButton(
      tooltip: signedIn ? 'Konto' : 'Zaloguj się',
      icon: const ZzIcon('account'),
      onPressed: () {
        if (signedIn) {
          context.push('/account');
        } else {
          final here = GoRouterState.of(context).uri.toString();
          context.push('/login?redirect=${Uri.encodeComponent(here)}');
        }
      },
    );
  }
}
