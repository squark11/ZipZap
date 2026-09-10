import 'package:connectivity_plus/connectivity_plus.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../theme/zz_theme.dart';

bool _isOnline(List<ConnectivityResult> results) =>
    results.any((r) => r != ConnectivityResult.none);

/// `true` = jest połączenie, `false` = offline. Zaczyna od bieżącego stanu,
/// potem śledzi zmiany interfejsów sieciowych.
final connectivityProvider = StreamProvider<bool>((ref) async* {
  final c = Connectivity();
  yield _isOnline(await c.checkConnectivity());
  yield* c.onConnectivityChanged.map(_isOnline);
});

/// Pasek „Brak połączenia" — montowany globalnie nad wszystkimi ekranami
/// (przez `builder` w [MaterialApp]). Gdy online — zwija się do zera.
class OfflineBar extends ConsumerWidget {
  const OfflineBar({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    // Przy braku danych zakładamy online (nie strasz użytkownika na starcie).
    final online = ref.watch(connectivityProvider).valueOrNull ?? true;
    return AnimatedSize(
      duration: const Duration(milliseconds: 220),
      curve: Curves.easeOut,
      alignment: Alignment.topCenter,
      child: online
          ? const SizedBox(width: double.infinity)
          : const _OfflineContent(),
    );
  }
}

class _OfflineContent extends StatelessWidget {
  const _OfflineContent();

  @override
  Widget build(BuildContext context) {
    return Material(
      color: ZzColors.danger,
      child: SafeArea(
        bottom: false,
        child: Padding(
          padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
          child: Row(
            mainAxisAlignment: MainAxisAlignment.center,
            children: const [
              Icon(Icons.cloud_off, color: Colors.white, size: 16),
              SizedBox(width: 8),
              Text('Brak połączenia z internetem',
                  style: TextStyle(
                      color: Colors.white, fontWeight: FontWeight.w600, fontSize: 13)),
            ],
          ),
        ),
      ),
    );
  }
}
