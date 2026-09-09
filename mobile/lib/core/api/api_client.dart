import 'dart:math';
import 'package:dio/dio.dart';

import '../config/app_config.dart';
import 'api_exception.dart';

/// Zwraca true, jeśli udało się odświeżyć token (i można ponowić żądanie).
typedef RefreshCallback = Future<bool> Function();

/// Cienki klient HTTP nad dio:
///  - dokłada Bearer, gdy jest token,
///  - dokłada correlation id,
///  - na 401 próbuje jednorazowo odświeżyć token i ponowić żądanie,
///  - normalizuje błędy do [ApiException] (koperta {code,message,traceId}).
class ApiClient {
  final Dio _dio;
  String? _accessToken;
  RefreshCallback? _onRefresh;

  ApiClient({Dio? dio})
      : _dio = dio ??
            Dio(BaseOptions(
              baseUrl: AppConfig.apiBaseUrl,
              connectTimeout: const Duration(seconds: 15),
              receiveTimeout: const Duration(seconds: 20),
              headers: {'Content-Type': 'application/json'},
            )) {
    _dio.interceptors.add(InterceptorsWrapper(
      onRequest: (options, handler) {
        if (_accessToken != null && !options.headers.containsKey('Authorization')) {
          options.headers['Authorization'] = 'Bearer $_accessToken';
        }
        options.headers['X-Correlation-Id'] = _correlationId();
        handler.next(options);
      },
      onError: (e, handler) async {
        final req = e.requestOptions;
        // Nie odświeżamy na endpointach auth (uniknięcie rekurencji refresh→401→refresh).
        final isAuthEndpoint = req.path.contains('/identity/');
        final canRetry = e.response?.statusCode == 401 &&
            !isAuthEndpoint &&
            _onRefresh != null &&
            _accessToken != null &&
            req.extra['retried'] != true;
        if (canRetry) {
          final ok = await _onRefresh!.call();
          if (ok) {
            req.extra['retried'] = true;
            req.headers['Authorization'] = 'Bearer $_accessToken';
            try {
              final clone = await _dio.fetch(req);
              return handler.resolve(clone);
            } on DioException catch (err) {
              return handler.next(err);
            }
          }
        }
        handler.next(e);
      },
    ));
  }

  void setAccessToken(String? token) => _accessToken = token;
  void setRefreshCallback(RefreshCallback cb) => _onRefresh = cb;

  Future<dynamic> get(String path, {Map<String, dynamic>? query}) =>
      _send(() => _dio.get(path, queryParameters: query));

  Future<dynamic> post(String path,
          {Object? body, Map<String, dynamic>? query, Map<String, String>? headers}) =>
      _send(() => _dio.post(path,
          data: body, queryParameters: query, options: Options(headers: headers)));

  Future<dynamic> put(String path, {Object? body, Map<String, dynamic>? query}) =>
      _send(() => _dio.put(path, data: body, queryParameters: query));

  Future<dynamic> delete(String path, {Map<String, dynamic>? query}) =>
      _send(() => _dio.delete(path, queryParameters: query));

  Future<dynamic> _send(Future<Response> Function() run) async {
    try {
      final r = await run();
      return r.data;
    } on DioException catch (e) {
      throw _toApiException(e);
    }
  }

  ApiException _toApiException(DioException e) {
    final resp = e.response;
    final data = resp?.data;
    if (data is Map) {
      return ApiException(
        code: (data['code'] ?? data['title'] ?? 'error').toString(),
        message: (data['message'] ?? data['detail'] ?? _fallbackMessage(resp?.statusCode)).toString(),
        statusCode: resp?.statusCode,
        traceId: data['traceId']?.toString(),
      );
    }
    return ApiException(
      code: resp?.statusCode == null ? 'network' : 'error',
      message: _fallbackMessage(resp?.statusCode),
      statusCode: resp?.statusCode,
    );
  }

  String _fallbackMessage(int? status) {
    switch (status) {
      case 401:
        return 'Wymagane logowanie.';
      case 403:
        return 'Brak dostępu.';
      case 404:
        return 'Nie znaleziono zasobu.';
      case null:
        return 'Brak połączenia z serwerem. Sprawdź sieć i spróbuj ponownie.';
      default:
        return 'Wystąpił błąd ($status). Spróbuj ponownie.';
    }
  }

  static final _rnd = Random();
  String _correlationId() {
    const chars = '0123456789abcdef';
    return List.generate(16, (_) => chars[_rnd.nextInt(16)]).join();
  }
}
