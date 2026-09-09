class CartItem {
  final String productId;
  final String productName;
  final double unitPrice;
  final int quantity;
  final double lineTotal;

  CartItem({
    required this.productId,
    required this.productName,
    required this.unitPrice,
    required this.quantity,
    required this.lineTotal,
  });

  factory CartItem.fromJson(Map<String, dynamic> j) => CartItem(
        productId: j['productId'].toString(),
        productName: j['productName'] ?? '',
        unitPrice: (j['unitPrice'] as num?)?.toDouble() ?? 0,
        quantity: (j['quantity'] as num?)?.toInt() ?? 0,
        lineTotal: (j['lineTotal'] as num?)?.toDouble() ?? 0,
      );
}

class Cart {
  final String id;
  final String storeId;
  final String cartToken;
  final String status;
  final double subtotal;
  final List<CartItem> items;

  Cart({
    required this.id,
    required this.storeId,
    required this.cartToken,
    required this.status,
    required this.subtotal,
    required this.items,
  });

  int get totalQuantity => items.fold(0, (a, i) => a + i.quantity);

  factory Cart.fromJson(Map<String, dynamic> j) => Cart(
        id: j['id'].toString(),
        storeId: j['storeId'].toString(),
        cartToken: j['cartToken'] ?? '',
        status: j['status'] ?? 'Active',
        subtotal: (j['subtotal'] as num?)?.toDouble() ?? 0,
        items: (j['items'] as List?)
                ?.map((e) => CartItem.fromJson(e as Map<String, dynamic>))
                .toList() ??
            const [],
      );
}
