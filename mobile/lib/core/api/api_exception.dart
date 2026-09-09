/// Znormalizowany błąd API, zbudowany z koperty backendu {code, message, traceId}.
class ApiException implements Exception {
  final String code;
  final String message;
  final int? statusCode;
  final String? traceId;

  ApiException({
    required this.code,
    required this.message,
    this.statusCode,
    this.traceId,
  });

  bool get isUnauthorized => statusCode == 401;
  bool get isForbidden => statusCode == 403;
  bool get isNotFound => statusCode == 404;
  bool get isNetwork => statusCode == null;

  @override
  String toString() => message;
}
