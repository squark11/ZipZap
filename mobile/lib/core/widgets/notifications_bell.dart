import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../providers.dart';
import '../theme/zz_theme.dart';
import 'zz_icon.dart';

/// Liczba nieprzeczytanych powiadomień zalogowanego użytkownika.
final unreadCountProvider = FutureProvider.autoDispose<int>((ref) async {
  final list = await ref.read(notificationsRepositoryProvider).listMine(take: 50);
  return list.where((n) => !n.isRead).length;
});

/// Ikona dzwonka z licznikiem nieprzeczytanych (dla AppBar zalogowanego).
class NotificationsBell extends ConsumerWidget {
  const NotificationsBell({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final count = ref.watch(unreadCountProvider).valueOrNull ?? 0;
    return Stack(
      alignment: Alignment.center,
      children: [
        IconButton(
          icon: const ZzIcon('bell', size: 24),
          tooltip: 'Powiadomienia',
          onPressed: () => context.push('/notifications'),
        ),
        if (count > 0)
          Positioned(
            right: 6,
            top: 8,
            child: Container(
              padding: const EdgeInsets.symmetric(horizontal: 5, vertical: 1),
              decoration: BoxDecoration(
                color: ZzColors.orange,
                borderRadius: BorderRadius.circular(10),
              ),
              constraints: const BoxConstraints(minWidth: 16),
              child: Text(count > 9 ? '9+' : '$count',
                  textAlign: TextAlign.center,
                  style: const TextStyle(
                      color: Colors.white, fontSize: 11, fontWeight: FontWeight.w700)),
            ),
          ),
      ],
    );
  }
}
