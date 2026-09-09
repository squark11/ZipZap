import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'api/api_client.dart';
import 'auth/auth_controller.dart';
import 'auth/auth_repository.dart';
import 'auth/auth_state.dart';
import 'auth/token_store.dart';
import '../features/catalog/catalog_repository.dart';
import '../features/cart/ordering_repository.dart';
import '../features/payment/payments_repository.dart';
import '../features/notifications/notifications_repository.dart';

// --- Infrastruktura ---
final tokenStoreProvider = Provider<TokenStore>((ref) => TokenStore());
final apiClientProvider = Provider<ApiClient>((ref) => ApiClient());

// --- Auth ---
final authRepositoryProvider =
    Provider<AuthRepository>((ref) => AuthRepository(ref.read(apiClientProvider)));
final authControllerProvider =
    NotifierProvider<AuthController, AuthState>(AuthController.new);

// --- Repozytoria domenowe ---
final catalogRepositoryProvider =
    Provider<CatalogRepository>((ref) => CatalogRepository(ref.read(apiClientProvider)));
final orderingRepositoryProvider =
    Provider<OrderingRepository>((ref) => OrderingRepository(ref.read(apiClientProvider)));
final paymentsRepositoryProvider =
    Provider<PaymentsRepository>((ref) => PaymentsRepository(ref.read(apiClientProvider)));
final notificationsRepositoryProvider =
    Provider<NotificationsRepository>((ref) => NotificationsRepository(ref.read(apiClientProvider)));
