class Address {
  final String id;
  final String label;
  final String street;
  final String buildingNo;
  final String? apartmentNo;
  final String postalCode;
  final String city;
  final String? notes;
  final double? latitude;
  final double? longitude;
  final bool isDefault;

  Address({
    required this.id,
    required this.label,
    required this.street,
    required this.buildingNo,
    this.apartmentNo,
    required this.postalCode,
    required this.city,
    this.notes,
    this.latitude,
    this.longitude,
    required this.isDefault,
  });

  factory Address.fromJson(Map<String, dynamic> j) => Address(
        id: j['id'].toString(),
        label: (j['label'] ?? '').toString(),
        street: (j['street'] ?? '').toString(),
        buildingNo: (j['buildingNo'] ?? '').toString(),
        apartmentNo: j['apartmentNo']?.toString(),
        postalCode: (j['postalCode'] ?? '').toString(),
        city: (j['city'] ?? '').toString(),
        notes: j['notes']?.toString(),
        latitude: (j['latitude'] as num?)?.toDouble(),
        longitude: (j['longitude'] as num?)?.toDouble(),
        isDefault: j['isDefault'] == true,
      );

  /// Adres w jednej linii (do listy i checkoutu).
  String get oneLine {
    final apt = (apartmentNo != null && apartmentNo!.isNotEmpty) ? '/$apartmentNo' : '';
    return '$street $buildingNo$apt, $postalCode $city';
  }
}
