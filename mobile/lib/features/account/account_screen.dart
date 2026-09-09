import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/providers.dart';
import '../../core/theme/zz_theme.dart';
import '../../core/widgets/zz_icon.dart';

class AccountScreen extends ConsumerWidget {
  const AccountScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final user = ref.watch(authControllerProvider).user;

    return Scaffold(
      appBar: AppBar(title: const Text('Konto')),
      body: ListView(
        padding: const EdgeInsets.all(16),
        children: [
          Card(
            child: Padding(
              padding: const EdgeInsets.all(16),
              child: Row(
                children: [
                  const CircleAvatar(
                    radius: 26,
                    backgroundColor: ZzColors.orange50,
                    child: ZzIcon('account', size: 26, color: ZzColors.orange),
                  ),
                  const SizedBox(width: 14),
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(user?.fullName ?? 'Klient',
                            style: Theme.of(context).textTheme.titleMedium),
                        Text(user?.email ?? '',
                            style: const TextStyle(color: ZzColors.textMuted)),
                      ],
                    ),
                  ),
                ],
              ),
            ),
          ),
          const SizedBox(height: 16),
          Card(
            child: Column(
              children: [
                ListTile(
                  leading: const ZzIcon('orders', size: 22, color: ZzColors.graphite),
                  title: const Text('Moje zamówienia'),
                  trailing: const ZzIcon('chevron_right', size: 20, color: ZzColors.textMuted),
                  onTap: () => context.push('/orders'),
                ),
                const Divider(height: 1),
                ListTile(
                  leading: const ZzIcon('bell', size: 22, color: ZzColors.graphite),
                  title: const Text('Powiadomienia'),
                  trailing: const ZzIcon('chevron_right', size: 20, color: ZzColors.textMuted),
                  onTap: () => context.push('/notifications'),
                ),
              ],
            ),
          ),
          const SizedBox(height: 16),
          OutlinedButton.icon(
            style: OutlinedButton.styleFrom(foregroundColor: ZzColors.danger),
            icon: const Icon(Icons.logout),
            label: const Text('Wyloguj się'),
            onPressed: () async {
              await ref.read(authControllerProvider.notifier).logout();
              if (context.mounted) context.go('/stores');
            },
          ),
        ],
      ),
    );
  }
}
