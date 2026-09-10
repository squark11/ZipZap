import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/providers.dart';
import '../../core/theme/theme_mode_controller.dart';
import '../../core/theme/zz_theme.dart';
import '../../core/widgets/zz_icon.dart';

class AccountScreen extends ConsumerWidget {
  const AccountScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final user = ref.watch(authControllerProvider).user;
    final mode = ref.watch(themeModeProvider);

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
                  CircleAvatar(
                    radius: 26,
                    backgroundColor: context.zz.orangeTint,
                    child: const ZzIcon('account', size: 26, color: ZzColors.orange),
                  ),
                  const SizedBox(width: 14),
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(user?.fullName ?? 'Klient',
                            style: Theme.of(context).textTheme.titleMedium),
                        Text(user?.email ?? '',
                            style: TextStyle(color: context.zz.textMuted)),
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
                  leading: ZzIcon('orders', size: 22, color: context.zz.heading),
                  title: const Text('Moje zamówienia'),
                  trailing: ZzIcon('chevron_right', size: 20, color: context.zz.textMuted),
                  onTap: () => context.push('/orders'),
                ),
                const Divider(height: 1),
                ListTile(
                  leading: ZzIcon('bell', size: 22, color: context.zz.heading),
                  title: const Text('Powiadomienia'),
                  trailing: ZzIcon('chevron_right', size: 20, color: context.zz.textMuted),
                  onTap: () => context.push('/notifications'),
                ),
              ],
            ),
          ),
          const SizedBox(height: 16),
          Card(
            child: Padding(
              padding: const EdgeInsets.all(16),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  const Text('Wygląd',
                      style: TextStyle(fontWeight: FontWeight.w600, fontSize: 15)),
                  const SizedBox(height: 12),
                  SizedBox(
                    width: double.infinity,
                    child: SegmentedButton<ThemeMode>(
                      showSelectedIcon: false,
                      segments: const [
                        ButtonSegment(value: ThemeMode.system, label: Text('System')),
                        ButtonSegment(value: ThemeMode.light, label: Text('Jasny')),
                        ButtonSegment(value: ThemeMode.dark, label: Text('Ciemny')),
                      ],
                      selected: {mode},
                      onSelectionChanged: (s) =>
                          ref.read(themeModeProvider.notifier).setMode(s.first),
                    ),
                  ),
                ],
              ),
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
