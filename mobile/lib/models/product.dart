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
  });

  factory Product.fromJson(Map<String, dynamic> j) => Product(
        id: j['id'].toString(),
        storeId: j['storeId'].toString(),
        categoryId: j['categoryId']?.toString(),
        name: j['name'] ?? '',
        description: j['description'],
        price: (j['price'] as num?)?.toDouble() ?? 0,
        currency: j['currency'] ?? 'PLN',
        unit: j['unit'] ?? 'szt',
        stockQty: (j['stockQty'] as num?)?.toInt(),
        isAvailable: j['isAvailable'] == true,
        imageUrl: j['imageUrl'],
      );
}
