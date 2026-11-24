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
  final String? logoUrl;
  final double? latitude;
  final double? longitude;
  final double? distanceKm;

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
    this.logoUrl,
    this.latitude,
    this.longitude,
    this.distanceKm,
  });

  bool get hasLocation => latitude != null && longitude != null;

  /// Odległość do sklepu w formie „2,3 km" / „850 m" (null gdy nieznana).
  String? get distanceLabel {
    final d = distanceKm;
    if (d == null) return null;
    if (d < 1) return '${(d * 1000).round()} m';
    return '${d.toStringAsFixed(1).replaceAll('.', ',')} km';
  }

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
        logoUrl: (j['logoUrl'] as String?)?.isNotEmpty == true ? j['logoUrl'] as String : null,
        latitude: (j['latitude'] as num?)?.toDouble(),
        longitude: (j['longitude'] as num?)?.toDouble(),
        distanceKm: (j['distanceKm'] as num?)?.toDouble(),
      );
}
