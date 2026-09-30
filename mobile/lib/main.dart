import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_web_plugins/url_strategy.dart';
import 'package:intl/date_symbol_data_local.dart';

import 'core/config/app_config.dart';
import 'core/router/app_router.dart';
import 'core/theme/theme_mode_controller.dart';
import 'core/theme/zz_theme.dart';
import 'core/widgets/connectivity_banner.dart';

Future<void> main() async {
  WidgetsFlutterBinding.ensureInitialized();
  // Web: zwykłe adresy (https://…/s/{slug}) zamiast „/#/…" — stałe linki z kodów QR, odświeżanie strony
  // i wklejanie linku działają przy SPA-fallbacku hostingu (index.html dla nieznanych ścieżek). Na mobile no-op.
  usePathUrlStrategy();
  await initializeDateFormatting('pl_PL');
  runApp(const ProviderScope(child: ZipZapApp()));
}

class ZipZapApp extends ConsumerWidget {
  const ZipZapApp({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final router = ref.watch(routerProvider);
    final themeMode = ref.watch(themeModeProvider);
    return MaterialApp.router(
      title: 'Dowózka.pl',
      debugShowCheckedModeBanner: false,
      theme: ZzTheme.light(),
      darkTheme: ZzTheme.dark(),
      themeMode: themeMode,
      routerConfig: router,
      builder: (context, child) {
        final app = Column(
          children: [
            const OfflineBar(),
            Expanded(child: child ?? const SizedBox.shrink()),
          ],
        );
        // Podgląd na testowym API (build z APP_ENV=preview) — widoczny znacznik w rogu każdego ekranu.
        return AppConfig.isPreview
            ? Banner(message: 'PODGLĄD', location: BannerLocation.topEnd, child: app)
            : app;
      },
    );
  }
}
