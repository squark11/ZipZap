class AppUser {
  final String id;
  final String email;
  final String fullName;
  final List<String> roles;

  AppUser({
    required this.id,
    required this.email,
    required this.fullName,
    required this.roles,
  });

  factory AppUser.fromJson(Map<String, dynamic> j) => AppUser(
        id: j['id'].toString(),
        email: j['email'] ?? '',
        fullName: j['fullName'] ?? '',
        roles: (j['roles'] as List?)?.map((e) => e.toString()).toList() ?? const [],
      );
}
