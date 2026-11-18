import 'dart:async';
import 'dart:io' show SocketException;

import 'package:dio/dio.dart';

import '../logging/log.dart';

/// Заголовки каждого запроса: токен входа, язык ответа, приложение. 401 на запросе с токеном — сигнал «сеанс закрыт» (кроме входа:
/// там 401 — неверный пароль, а не закрытый сеанс).
class SessionInterceptor extends Interceptor {
  SessionInterceptor({
    required this.token,
    required this.language,
    required this.userAgent,
    required this.onUnauthorized,
  });

  final String? Function() token;
  final String Function() language;
  final String userAgent;
  final void Function() onUnauthorized;

  static const loginPath = '/api/auth/login';

  @override
  void onRequest(RequestOptions options, RequestInterceptorHandler handler) {
    final t = token();
    if (t != null && t.isNotEmpty && options.path != loginPath) {
      options.headers['Authorization'] = 'Bearer $t';
    }
    options.headers['Accept-Language'] = language();
    options.headers['User-Agent'] = userAgent;
    handler.next(options);
  }

  @override
  void onError(DioException err, ErrorInterceptorHandler handler) {
    final o = err.requestOptions;
    if (err.response?.statusCode == 401 && o.path != loginPath && o.headers.containsKey('Authorization')) {
      onUnauthorized();
    }
    handler.next(err);
  }
}

/// Повтор идемпотентных запросов (GET/HEAD или `extra['retry'] = true`) при обрыве, таймауте и 502/503/504: две попытки,
/// через 0,5 и 1,5 с. POST/PUT/DELETE не повторяются: операция могла пройти на сервере. Потоки (SSE консультанта) не повторяются.
class RetryInterceptor extends Interceptor {
  RetryInterceptor(
    this._dio, {
    this.delays = const [Duration(milliseconds: 500), Duration(milliseconds: 1500)],
    Future<void> Function(Duration)? sleep,
  }) : _sleep = sleep ?? Future<void>.delayed;

  final Dio _dio;
  final List<Duration> delays;
  final Future<void> Function(Duration) _sleep;

  static const attemptKey = 'retry_attempt';

  @override
  Future<void> onError(DioException err, ErrorInterceptorHandler handler) async {
    final o = err.requestOptions;
    final attempt = (o.extra[attemptKey] as int?) ?? 0;
    if (!shouldRetry(err) || attempt >= delays.length) {
      handler.next(err);
      return;
    }
    await _sleep(delays[attempt]);
    if (o.cancelToken?.isCancelled ?? false) {
      handler.next(err);
      return;
    }
    o.extra[attemptKey] = attempt + 1;
    Log.d('api', '${o.method} ${o.path} — повтор ${attempt + 1}');
    try {
      handler.resolve(await _dio.fetch<Object?>(o));
    } on DioException catch (e) {
      handler.next(e);
    }
  }

  static bool shouldRetry(DioException e) {
    final o = e.requestOptions;
    final method = o.method.toUpperCase();
    final idempotent = method == 'GET' || method == 'HEAD' || o.extra['retry'] == true;
    if (!idempotent || o.responseType == ResponseType.stream) return false;
    switch (e.type) {
      case DioExceptionType.connectionTimeout:
      case DioExceptionType.receiveTimeout:
      case DioExceptionType.connectionError:
        return true;
      case DioExceptionType.badResponse:
        final s = e.response?.statusCode ?? 0;
        return s == 502 || s == 503 || s == 504;
      case DioExceptionType.unknown:
        return e.response == null && e.error is SocketException;
      case DioExceptionType.sendTimeout:
      case DioExceptionType.transformTimeout:
      case DioExceptionType.badCertificate:
      case DioExceptionType.cancel:
        return false;
    }
  }
}

/// Журнал запросов в debug: метод, путь, код и время. Без заголовков, тел и параметров с данными.
class LogInterceptor extends Interceptor {
  static const _startKey = 'log_start';

  @override
  void onRequest(RequestOptions options, RequestInterceptorHandler handler) {
    options.extra[_startKey] = DateTime.now().microsecondsSinceEpoch;
    handler.next(options);
  }

  @override
  void onResponse(Response<dynamic> response, ResponseInterceptorHandler handler) {
    _log(response.requestOptions, response.statusCode);
    handler.next(response);
  }

  @override
  void onError(DioException err, ErrorInterceptorHandler handler) {
    _log(err.requestOptions, err.response?.statusCode, err.type.name);
    handler.next(err);
  }

  void _log(RequestOptions o, int? status, [String? error]) {
    final start = o.extra[_startKey] as int?;
    final ms = start == null ? '?' : ((DateTime.now().microsecondsSinceEpoch - start) / 1000).round();
    Log.d('api', '${o.method} ${o.path} → ${status ?? error} · $ms мс');
  }
}
