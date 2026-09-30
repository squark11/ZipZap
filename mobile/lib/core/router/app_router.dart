import 'package:flutter/foundation.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../auth/auth_state.dart';
import '../navigation/last_store.dart';
import '../providers.dart';
import 'main_scaffold.dart';
import 'not_found_screen.dart';
import 'redirects.dart';
import '../../features/account/login_screen.dart';
import '../../features/account/forgot_password_screen.dart';
import '../../features/account/change_password_screen.dart';
import '../../features/account/addresses_screen.dart';
import '../../features/account/account_screen.dart';
import '../../features/cart/cart_screen.dart';
import '../../features/catalog/store_detail_screen.dart';
import '../../features/checkout/checkout_screen.dart';
import '../../features/feedback/feedback_screen.dart';
import '../../features/notifications/notifications_screen.dart';
import '../../features/orders/my_orders_screen.dart';
import '../../features/orders/order_track_screen.dart';
import '../../features/payment/payment_screen.dart';
import '../../features/splash/splash_screen.dart';
import '../../features/stores/stores_screen.dart';

bool _needsAuth(String loc) =>
    loc.startsWith('/checkout') ||
    loc.startsWith('/pay') ||
    loc.startsWith('/orders') ||
    loc.startsWith('/account') ||
    loc.startsWith('/notifications');

/// Parametr źródła w stałym linku do sklepu (np. kod QR: `/s/{slug}?src=qr`) — tylko do pomiaru wejść.
const sourceParam = 'src';

/// Karta sklepu: `/s/{slug}` (stały link z kodu QR) albo `/stores/{id}` (z listy). Zwraca slug/id albo null.
String? _storeRefOf(String path) {
  final m = RegExp(r'^/(?:s|stores)/([^/]+)$').firstMatch(path);
  return m == null ? null : Uri.decodeComponent(m.group(1)!);
}

final routerProvider = Provider<GoRouter>((ref) {
  final refresh = ValueNotifier<int>(0);
  ref.listen(authControllerProvider, (_, _) => refresh.value++);
  ref.onDispose(refresh.dispose);
  // Wczytaj zapamiętany sklep już przy starcie (z magazynu), żeby był gotowy, zanim klient się zaloguje.
  ref.read(lastStoreProvider);

  return GoRouter(
    initialLocation: '/splash',
    refreshListenable: refresh,
    redirect: (context, gstate) {
      final auth = ref.read(authControllerProvider);
      final loc = gstate.matchedLocation;
      final uri = gstate.uri;

      // Podczas bootstrapu pokazujemy splash, ale zapamiętujemy dokąd
      // użytkownik zmierzał (deep‑link do sklepu / z powiadomienia / z kodu QR).
      if (auth.status == AuthStatus.unknown) {
        if (loc == '/splash') return null;
        return '/splash?from=${Uri.encodeComponent(uri.toString())}';
      }
      // Po bootstrapie wracamy na zapamiętaną trasę (lub domyślnie /stores).
      if (loc == '/splash') {
        final from = uri.queryParameters['from'];
        if (from != null && from.isNotEmpty) {
          final decoded = Uri.decodeComponent(from);
          if (decoded.startsWith('/') && !decoded.startsWith('//') && !decoded.startsWith('/splash')) {
            return decoded;
          }
        }
        return '/stores';
      }

      // Wejście z kodu QR/kampanii: zliczamy źródło (raz na sesję) i czyścimy adres — odświeżenie strony, zakładka
      // czy udostępniony dalej link nie liczą się ponownie jako skan. Źródło niczego nie odblokowuje.
      final src = uri.queryParameters[sourceParam];
      if (src != null) {
        final storeRef = _storeRefOf(uri.path);
        if (storeRef != null) ref.read(storeEntryRecorderProvider).record(storeRef, src);
        final rest = Map<String, String>.of(uri.queryParameters)..remove(sourceParam);
        return rest.isEmpty ? uri.path : Uri(path: uri.path, queryParameters: rest).toString();
      }

      if (_needsAuth(loc) && !auth.isAuthenticated) {
        return '/login?redirect=${Uri.encodeComponent(uri.toString())}';
      }
      if (loc == '/login' && auth.isAuthenticated) {
        return safeInternalRedirect(uri.queryParameters['redirect'])
            ?? ref.read(lastStoreProvider)?.path
            ?? '/stores';
      }
      return null;
    },
    errorBuilder: (_, _) => const NotFoundScreen(),
    routes: [
      GoRoute(path: '/', redirect: (_, _) => '/stores'),
      GoRoute(path: '/splash', builder: (_, _) => const SplashScreen()),

      // Główne zakładki z dolną nawigacją.
      ShellRoute(
        builder: (context, state, child) => MainScaffold(child: child),
        routes: [
          GoRoute(path: '/stores', builder: (_, _) => const StoresScreen()),
          GoRoute(path: '/cart', builder: (_, _) => const CartScreen()),
          GoRoute(path: '/orders', builder: (_, _) => const MyOrdersScreen()),
          GoRoute(path: '/account', builder: (_, _) => const AccountScreen()),
        ],
      ),

      // Ekrany pełnoekranowe (bez dolnej nawigacji).
      // Stały link do oferty sklepu — ten adres jest w kodach QR (`/s/{slug}?src=qr`).
      GoRoute(
        path: '/s/:slug',
        builder: (_, s) => StoreDetailScreen(storeRef: s.pathParameters['slug']!),
      ),
      GoRoute(
        path: '/stores/:id',
        builder: (_, s) => StoreDetailScreen(storeRef: s.pathParameters['id']!),
      ),
      GoRoute(path: '/checkout', builder: (_, _) => const CheckoutScreen()),
      GoRoute(
        path: '/pay/:orderId',
        builder: (_, s) => PaymentScreen(orderId: s.pathParameters['orderId']!),
      ),
      GoRoute(
        path: '/orders/:id',
        builder: (_, s) => OrderTrackScreen(orderId: s.pathParameters['id']!),
      ),
      GoRoute(path: '/notifications', builder: (_, _) => const NotificationsScreen()),
      GoRoute(
        path: '/feedback',
        builder: (_, state) => FeedbackScreen(screen: state.uri.queryParameters['from']),
      ),
      GoRoute(
        path: '/login',
        builder: (_, s) => LoginScreen(redirect: s.uri.queryParameters['redirect']),
      ),
      GoRoute(
        path: '/forgot-password',
        // E-mail wpisany na ekranie logowania (przekazany w pamięci, nie w adresie).
        builder: (_, s) => ForgotPasswordScreen(initialEmail: s.extra is String ? s.extra as String : null),
      ),
      GoRoute(path: '/change-password', builder: (_, _) => const ChangePasswordScreen()),
      GoRoute(path: '/addresses', builder: (_, _) => const AddressesScreen()),
    ],
  );
});
