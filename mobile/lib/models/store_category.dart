/// Kategoria produktów w sklepie (np. „Pieczywo") — zakładki na ekranie sklepu.
class StoreCategory {
  final String id;
  final String name;
  final int sortOrder;

  const StoreCategory({required this.id, required this.name, this.sortOrder = 0});

  factory StoreCategory.fromJson(Map<String, dynamic> j) => StoreCategory(
        id: j['id'].toString(),
        name: (j['name'] ?? '').toString(),
        sortOrder: (j['sortOrder'] as num?)?.toInt() ?? 0,
      );
}
