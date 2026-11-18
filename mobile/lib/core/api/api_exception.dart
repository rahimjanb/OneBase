import 'package:dio/dio.dart';

/// Что пошло не так с запросом — для текста на экране и решения «повторять ли».
enum ApiErrorKind {
  /// Нет соединения или сервер недоступен.
  network,
  timeout,

  /// 401: сеанс закрыт (выход, смена пароля, отключённый пользователь) или неверный логин и пароль при входе.
  unauthorized,

  /// 403: нет права на раздел.
  forbidden,
  notFound,

  /// 400 / 422: сервер не принял данные — его текст показывается как есть.
  validation,
  conflict,

  /// 429: лимит частоты (вход — 10 попыток в минуту, AI — 20 вопросов в минуту).
  rateLimited,
  server,
  cancelled,

  /// Ответ не того вида (не JSON, не те поля).
  invalidResponse,
  unknown,
}

/// Ошибка запроса к OneBase API. [message] — текст сервера на языке пользователя (Accept-Language), если сервер его прислал.
class ApiException implements Exception {
  const ApiException(this.kind, {this.status, this.message, this.cause});

  factory ApiException.fromDio(DioException e) {
    final status = e.response?.statusCode;
    final message = serverMessage(e.response?.data);
    switch (e.type) {
      case DioExceptionType.cancel:
        return ApiException(ApiErrorKind.cancelled, cause: e);
      case DioExceptionType.connectionTimeout:
      case DioExceptionType.sendTimeout:
      case DioExceptionType.receiveTimeout:
      case DioExceptionType.transformTimeout:
        return ApiException(ApiErrorKind.timeout, cause: e);
      case DioExceptionType.connectionError:
        return ApiException(ApiErrorKind.network, cause: e);
      case DioExceptionType.badCertificate:
        return ApiException(ApiErrorKind.network, message: message, cause: e);
      case DioExceptionType.badResponse:
        return ApiException(kindOf(status), status: status, message: message, cause: e);
      case DioExceptionType.unknown:
        // SocketException и подобные без ответа сервера — это сеть.
        return ApiException(
          e.response == null ? ApiErrorKind.network : ApiErrorKind.unknown,
          status: status,
          message: message,
          cause: e,
        );
    }
  }

  final ApiErrorKind kind;
  final int? status;
  final String? message;
  final Object? cause;

  /// Стоит ли повторить запрос позже (сеть, таймаут, 5xx) — для «Повторить» и автоповтора GET.
  bool get isTransient =>
      kind == ApiErrorKind.network || kind == ApiErrorKind.timeout || (kind == ApiErrorKind.server && (status ?? 500) >= 502);

  static ApiErrorKind kindOf(int? status) => switch (status) {
        400 || 422 => ApiErrorKind.validation,
        401 => ApiErrorKind.unauthorized,
        403 => ApiErrorKind.forbidden,
        404 => ApiErrorKind.notFound,
        409 => ApiErrorKind.conflict,
        429 => ApiErrorKind.rateLimited,
        final s? when s >= 500 => ApiErrorKind.server,
        _ => ApiErrorKind.unknown,
      };

  /// Текст ошибки из ответа OneBase: `{error}` (контроллеры), `{detail|title, errors}` (ProblemDetails, проверка полей) или короткий
  /// текст. HTML-страницы прокси и длинные ответы — не показываем.
  static String? serverMessage(Object? data) {
    if (data is Map) {
      final errors = data['errors'];
      if (errors is Map && errors.isNotEmpty) {
        final first = errors.values.first;
        if (first is List && first.isNotEmpty && first.first is String) return first.first as String;
        if (first is String) return first;
      }
      for (final key in const ['error', 'message', 'detail', 'title']) {
        final v = data[key];
        if (v is String && v.trim().isNotEmpty) return v.trim();
      }
      return null;
    }
    if (data is String) {
      final t = data.trim();
      if (t.isEmpty || t.length > 300 || t.startsWith('<')) return null;
      return t;
    }
    return null;
  }

  @override
  String toString() => 'ApiException($kind, status: $status)';
}
