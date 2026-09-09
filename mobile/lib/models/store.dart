class Store {
  final String id;
  final String name;
  final String slug;
  final String city;
  final String status; // Open | Closed | TemporarilyUnavailable
  final String? description;
  final String? address;
  final String? phone;
  final double minimumOrderValue;
  final bool isAcceptingOrders;

  Store({
    required this.id,
    required this.name,
    required this.slug,
    required this.city,
    required this.status,
    required this.description,
    required this.address,
    required this.phone,
    required this.minimumOrderValue,
    required this.isAcceptingOrders,
  });

  factory Store.fromJson(Map<String, dynamic> j) => Store(
        id: j['id'].toString(),
        name: j['name'] ?? '',
        slug: j['slug'] ?? '',
        city: j['city'] ?? '',
        status: j['status'] ?? 'Open',
        description: j['description'],
        address: j['address'],
        phone: j['phone'],
        minimumOrderValue: (j['minimumOrderValue'] as num?)?.toDouble() ?? 0,
        isAcceptingOrders: j['isAcceptingOrders'] == true,
      );
}
