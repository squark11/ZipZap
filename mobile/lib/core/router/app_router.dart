import 'package:flutter/foundation.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../auth/auth_state.dart';
import '../providers.dart';
import '../../features/account/login_screen.dart';
import '../../features/account/forgot_password_screen.dart';
import '../../features/account/account_screen.dart';
import '../../features/cart/cart_screen.dart';
import '../../features/catalog/store_detail_screen.dart';
import '../../features/checkout/checkout_screen.dart';
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

final routerProvider = Provider<GoRouter>((ref) {
  final refresh = ValueNotifier<int>(0);
  ref.listen(authControllerProvider, (_, _) => refresh.value++);
  ref.onDispose(refresh.dispose);

  return GoRouter(
    initialLocation: '/splash',
    refreshListenable: refresh,
    redirect: (context, gstate) {
      final auth = ref.read(authControllerProvider);
      final loc = gstate.matchedLocation;

      if (auth.status == AuthStatus.unknown) {
        return loc == '/splash' ? null : '/splash';
      }
      if (loc == '/splash') return '/stores';

      if (_needsAuth(loc) && !auth.isAuthenticated) {
        return '/login?redirect=${Uri.encodeComponent(gstate.uri.toString())}';
      }
      if (loc == '/login' && auth.isAuthenticated) return '/stores';
      return null;
    },
    routes: [
      GoRoute(path: '/splash', builder: (_, _) => const SplashScreen()),
      GoRoute(path: '/stores', builder: (_, _) => const StoresScreen()),
      GoRoute(
        path: '/stores/:id',
        builder: (_, s) => StoreDetailScreen(storeId: s.pathParameters['id']!),
      ),
      GoRoute(path: '/cart', builder: (_, _) => const CartScreen()),
      GoRoute(path: '/checkout', builder: (_, _) => const CheckoutScreen()),
      GoRoute(
        path: '/pay/:orderId',
        builder: (_, s) => PaymentScreen(orderId: s.pathParameters['orderId']!),
      ),
      GoRoute(path: '/orders', builder: (_, _) => const MyOrdersScreen()),
      GoRoute(
        path: '/orders/:id',
        builder: (_, s) => OrderTrackScreen(orderId: s.pathParameters['id']!),
      ),
      GoRoute(path: '/notifications', builder: (_, _) => const NotificationsScreen()),
      GoRoute(path: '/account', builder: (_, _) => const AccountScreen()),
      GoRoute(
        path: '/login',
        builder: (_, s) => LoginScreen(redirect: s.uri.queryParameters['redirect']),
      ),
      GoRoute(path: '/forgot-password', builder: (_, _) => const ForgotPasswordScreen()),
    ],
  );
});
