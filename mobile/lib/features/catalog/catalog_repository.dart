import '../../core/api/api_client.dart';
import '../../models/product.dart';
import '../../models/store.dart';

class CatalogRepository {
  final ApiClient _api;
  CatalogRepository(this._api);

  Future<List<Store>> listStores({bool onlyActive = true, double? lat, double? lng, String? postalCode}) async {
    final digits = (postalCode ?? '').replaceAll(RegExp(r'\D'), '');
    // Z kodem pocztowym: tylko sklepy dowożące pod ten kod (endpoint zasięgu).
    if (digits.length == 5) {
      final q = <String, dynamic>{'postalCode': postalCode};
      if (lat != null && lng != null) { q['lat'] = lat; q['lng'] = lng; }
      final data = await _api.get('/stores/serving', query: q);
      return (data as List).map((e) => Store.fromJson(e as Map<String, dynamic>)).toList();
    }
    final query = <String, dynamic>{'onlyActive': onlyActive};
    if (lat != null && lng != null) { query['lat'] = lat; query['lng'] = lng; }
    final data = await _api.get('/catalog/stores', query: query);
    return (data as List).map((e) => Store.fromJson(e as Map<String, dynamic>)).toList();
  }

  Future<Store> getStore(String idOrSlug) async {
    final data = await _api.get('/catalog/stores/$idOrSlug');
    return Store.fromJson(data as Map<String, dynamic>);
  }

  Future<List<Product>> listProducts(String storeId, {String? categoryId}) async {
    final data = await _api.get('/catalog/stores/$storeId/products',
        query: categoryId == null ? null : {'categoryId': categoryId});
    return (data as List).map((e) => Product.fromJson(e as Map<String, dynamic>)).toList();
  }
}
