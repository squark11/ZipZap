import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:intl/date_symbol_data_local.dart';

import 'core/router/app_router.dart';
import 'core/theme/zz_theme.dart';

Future<void> main() async {
  WidgetsFlutterBinding.ensureInitialized();
  await initializeDateFormatting('pl_PL');
  runApp(const ProviderScope(child: ZipZapApp()));
}

class ZipZapApp extends ConsumerWidget {
  const ZipZapApp({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final router = ref.watch(routerProvider);
    return MaterialApp.router(
      title: 'ZipZap',
      debugShowCheckedModeBanner: false,
      theme: ZzTheme.light(),
      routerConfig: router,
    );
  }
}
