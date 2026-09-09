import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/providers.dart';
import '../../core/theme/zz_theme.dart';
import '../../core/util/format.dart';
import '../../core/widgets/states.dart';
import '../../models/notification.dart';

final notificationsProvider = FutureProvider.autoDispose<List<AppNotification>>(
    (ref) => ref.read(notificationsRepositoryProvider).listMine());

class NotificationsScreen extends ConsumerWidget {
  const NotificationsScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final items = ref.watch(notificationsProvider);
    return Scaffold(
      appBar: AppBar(title: const Text('Powiadomienia')),
      body: items.when(
        loading: () => const LoadingView(),
        error: (e, _) => ErrorView(
            message: e.toString(), onRetry: () => ref.invalidate(notificationsProvider)),
        data: (list) {
          if (list.isEmpty) {
            return const EmptyView(
              icon: Icons.notifications_none,
              title: 'Brak powiadomień',
            );
          }
          return RefreshIndicator(
            color: ZzColors.orange,
            onRefresh: () async => ref.invalidate(notificationsProvider),
            child: ListView.separated(
              padding: const EdgeInsets.all(16),
              itemCount: list.length,
              separatorBuilder: (_, _) => const SizedBox(height: 8),
              itemBuilder: (_, i) {
                final n = list[i];
                return Card(
                  child: ListTile(
                    leading: Icon(
                      n.isRead ? Icons.notifications_none : Icons.notifications_active,
                      color: n.isRead ? ZzColors.textMuted : ZzColors.orange,
                    ),
                    title: Text(n.title,
                        style: TextStyle(
                            fontWeight: n.isRead ? FontWeight.w500 : FontWeight.w700)),
                    subtitle: Text(shortDateTime(n.createdAtUtc),
                        style: const TextStyle(color: ZzColors.textMuted, fontSize: 12)),
                    trailing: n.isRead
                        ? null
                        : Container(
                            width: 10,
                            height: 10,
                            decoration: const BoxDecoration(
                                color: ZzColors.orange, shape: BoxShape.circle),
                          ),
                    onTap: n.isRead
                        ? null
                        : () async {
                            await ref
                                .read(notificationsRepositoryProvider)
                                .markRead(n.id);
                            ref.invalidate(notificationsProvider);
                          },
                  ),
                );
              },
            ),
          );
        },
      ),
    );
  }
}
