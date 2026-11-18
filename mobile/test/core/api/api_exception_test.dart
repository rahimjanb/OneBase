import 'package:dio/dio.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:onebase_mobile/core/api/api_exception.dart';

void main() {
  final req = RequestOptions(path: '/api/x');

  DioException bad(int status, Object? data) => DioException(
        requestOptions: req,
        type: DioExceptionType.badResponse,
        response: Response<Object?>(requestOptions: req, statusCode: status, data: data),
      );

  group('serverMessage', () {
    test('берёт {error} контроллеров OneBase', () {
      expect(ApiException.serverMessage({'error': ' Вопрос — от 1 до 2000 символов. '}), 'Вопрос — от 1 до 2000 символов.');
    });

    test('берёт первую ошибку проверки полей ProblemDetails', () {
      expect(
        ApiException.serverMessage({
          'title': 'One or more validation errors occurred.',
          'errors': {
            'Login': ['Логин обязателен'],
          },
        }),
        'Логин обязателен',
      );
    });

    test('короткий текст — да, HTML и длинный — нет', () {
      expect(ApiException.serverMessage('Нет сессии'), 'Нет сессии');
      expect(ApiException.serverMessage('<html>502 Bad Gateway</html>'), isNull);
      expect(ApiException.serverMessage('x' * 400), isNull);
      expect(ApiException.serverMessage(null), isNull);
    });
  });

  group('fromDio', () {
    test('коды ответа → вид ошибки', () {
      expect(ApiException.fromDio(bad(400, null)).kind, ApiErrorKind.validation);
      expect(ApiException.fromDio(bad(401, null)).kind, ApiErrorKind.unauthorized);
      expect(ApiException.fromDio(bad(403, null)).kind, ApiErrorKind.forbidden);
      expect(ApiException.fromDio(bad(404, null)).kind, ApiErrorKind.notFound);
      expect(ApiException.fromDio(bad(409, null)).kind, ApiErrorKind.conflict);
      expect(ApiException.fromDio(bad(429, null)).kind, ApiErrorKind.rateLimited);
      expect(ApiException.fromDio(bad(503, null)).kind, ApiErrorKind.server);
    });

    test('сеть, таймаут, отмена', () {
      expect(ApiException.fromDio(DioException.connectionError(requestOptions: req, reason: 'x')).kind, ApiErrorKind.network);
      expect(
        ApiException.fromDio(DioException.receiveTimeout(timeout: Duration.zero, requestOptions: req)).kind,
        ApiErrorKind.timeout,
      );
      expect(ApiException.fromDio(DioException.requestCancelled(requestOptions: req, reason: null)).kind, ApiErrorKind.cancelled);
    });

    test('текст сервера сохраняется', () {
      final e = ApiException.fromDio(bad(409, {'error': 'Задача уже закрыта.'}));
      expect(e.message, 'Задача уже закрыта.');
      expect(e.status, 409);
    });
  });

  test('isTransient: сеть, таймаут и 502–504 — да; 500, 4xx — нет', () {
    expect(const ApiException(ApiErrorKind.network).isTransient, isTrue);
    expect(const ApiException(ApiErrorKind.timeout).isTransient, isTrue);
    expect(const ApiException(ApiErrorKind.server, status: 503).isTransient, isTrue);
    expect(const ApiException(ApiErrorKind.server, status: 500).isTransient, isFalse);
    expect(const ApiException(ApiErrorKind.forbidden, status: 403).isTransient, isFalse);
  });
}
