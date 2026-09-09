import '../../core/api/api_client.dart';
import '../../models/product.dart';
import '../../models/store.dart';

class CatalogRepository {
  final ApiClient _api;
  CatalogRepository(this._api);

  Future<List<Store>> listStores({bool onlyActive = true}) async {
    final data = await _api.get('/catalog/stores', query: {'onlyActive': onlyActive});
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
