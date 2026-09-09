import '../../core/api/api_client.dart';
import '../../models/notification.dart';

class NotificationsRepository {
  final ApiClient _api;
  NotificationsRepository(this._api);

  Future<List<AppNotification>> listMine({int take = 50}) async {
    final data = await _api.get('/notifications/mine', query: {'take': take});
    return (data as List)
        .map((e) => AppNotification.fromJson(e as Map<String, dynamic>))
        .toList();
  }

  Future<void> markRead(String id) => _api.post('/notifications/$id/read');

  Future<void> registerDevice(String token, {String platform = 'web'}) =>
      _api.post('/notifications/devices', body: {'token': token, 'platform': platform});
}
