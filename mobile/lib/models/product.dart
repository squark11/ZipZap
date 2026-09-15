/// Jedna sprzedażowa jednostka produktu (np. „kg" = 4,99, „szt" = 1,20).
class ProductUnitOption {
  final String unit;
  final double price;
  const ProductUnitOption(this.unit, this.price);

  factory ProductUnitOption.fromJson(Map<String, dynamic> j) => ProductUnitOption(
        (j['unit'] ?? 'szt').toString(),
        (j['price'] as num?)?.toDouble() ?? 0,
      );
}

class Product {
  final String id;
  final String storeId;
  final String? categoryId;
  final String name;
  final String? description;
  final double price;
  final String currency;
  final String unit;
  final int? stockQty;
  final bool isAvailable;
  final String? imageUrl;

  /// Zawsze ≥1 opcja jednostki (pierwsza = domyślna). Gdy >1 — klient wybiera.
  final List<ProductUnitOption> unitOptions;

  Product({
    required this.id,
    required this.storeId,
    required this.categoryId,
    required this.name,
    required this.description,
    required this.price,
    required this.currency,
    required this.unit,
    required this.stockQty,
    required this.isAvailable,
    required this.imageUrl,
    required this.unitOptions,
  });

  bool get hasMultipleUnits => unitOptions.length > 1;

  factory Product.fromJson(Map<String, dynamic> j) {
    final unit = j['unit'] ?? 'szt';
    final price = (j['price'] as num?)?.toDouble() ?? 0;
    final rawOpts = j['unitOptions'];
    final opts = (rawOpts is List && rawOpts.isNotEmpty)
        ? rawOpts.map((e) => ProductUnitOption.fromJson(e as Map<String, dynamic>)).toList()
        : <ProductUnitOption>[ProductUnitOption(unit, price)];
    return Product(
      id: j['id'].toString(),
      storeId: j['storeId'].toString(),
      categoryId: j['categoryId']?.toString(),
      name: j['name'] ?? '',
      description: j['description'],
      price: price,
      currency: j['currency'] ?? 'PLN',
      unit: unit,
      stockQty: (j['stockQty'] as num?)?.toInt(),
      isAvailable: j['isAvailable'] == true,
      imageUrl: j['imageUrl'],
      unitOptions: opts,
    );
  }
}
