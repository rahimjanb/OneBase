import 'dart:async';
import 'dart:convert';
import 'dart:isolate';

import 'package:dio/dio.dart';

import '../logging/log.dart';
import 'api_exception.dart';

/// Разбор JSON ответа в модель. Бросает TypeError / FormatException на ответе не того вида — это [ApiErrorKind.invalidResponse].
typedef Decode<T> = T Function(Object? json);

/// Клиент OneBase API поверх Dio. Пути — `/api/...`; базовый адрес, токен, язык, повтор и журнал — в перехватчиках ([buildDio]).
/// Все ошибки — [ApiException].
///
/// - Одинаковые GET (путь + параметры), запущенные одновременно, идут одним запросом: второй блок получает тот же ответ.
/// - [CancelToken] вызывающего снимает его ожидание. Общий запрос отменяется, когда его больше никто не ждёт.
/// - [getExtract] — для тяжёлых ответов: JSON разбирается в фоновом изоляте, в интерфейс приходит только нужный срез.
class ApiClient {
  ApiClient(this.dio);

  final Dio dio;
  final Map<String, _Shared> _inflight = {};

  /// Сколько общих запросов сейчас в работе — для тестов.
  int get inflightCount => _inflight.length;

  Future<T> get<T>(String path, Decode<T> decode, {Map<String, Object?>? query, CancelToken? cancel}) async {
    final data = await _shared(
      'GET $path ${_queryKey(query)}',
      (token) async => (await dio.get<Object?>(path, queryParameters: _clean(query), cancelToken: token)).data,
      cancel,
    );
    return _decode(path, decode, data);
  }

  /// Тяжёлый GET (например, `/api/sales/republic` — 234 КБ): тело приходит текстом, `jsonDecode` и [extract] выполняются в фоновом
  /// изоляте. [extract] должен быть функцией верхнего уровня или статической — она пересылается в изолят.
  Future<T> getExtract<T>(String path, Decode<T> extract, {Map<String, Object?>? query, CancelToken? cancel}) async {
    final text = await _shared(
      'GET:text $path ${_queryKey(query)}',
      (token) async => (await dio.get<String>(
        path,
        queryParameters: _clean(query),
        cancelToken: token,
        options: Options(responseType: ResponseType.plain),
      ))
          .data,
      cancel,
    );
    if (text is! String) throw const ApiException(ApiErrorKind.invalidResponse);
    try {
      return await _parseInBackground(text, extract);
    } on Object catch (e, s) {
      Log.e('api', 'Ответ $path не того вида', e, s);
      throw ApiException(ApiErrorKind.invalidResponse, cause: e);
    }
  }

  Future<T> post<T>(String path, {Object? body, Decode<T>? decode, CancelToken? cancel}) =>
      _send('POST', path, body: body, decode: decode, cancel: cancel);

  Future<T> put<T>(String path, {Object? body, Decode<T>? decode, CancelToken? cancel}) =>
      _send('PUT', path, body: body, decode: decode, cancel: cancel);

  Future<T> delete<T>(String path, {Object? body, Decode<T>? decode, CancelToken? cancel}) =>
      _send('DELETE', path, body: body, decode: decode, cancel: cancel);

  Future<T> _send<T>(String method, String path, {Object? body, Decode<T>? decode, CancelToken? cancel}) async {
    final Response<Object?> r;
    try {
      r = await dio.request<Object?>(path, data: body, cancelToken: cancel, options: Options(method: method));
    } on DioException catch (e) {
      throw ApiException.fromDio(e);
    }
    if (decode == null) return null as T;
    return _decode(path, decode, r.data);
  }

  Future<Object?> _shared(String key, Future<Object?> Function(CancelToken token) run, CancelToken? cancel) {
    var shared = _inflight[key];
    if (shared == null) {
      final token = CancelToken();
      final future = _guard(run(token));
      final created = _Shared(future, token);
      shared = created;
      _inflight[key] = created;
      void remove() {
        if (identical(_inflight[key], created)) _inflight.remove(key);
      }

      unawaited(future.then<void>((_) => remove(), onError: (Object _) => remove()));
    }
    final s = shared;
    s.callers++;
    if (cancel != null) {
      if (cancel.isCancelled) {
        _release(key, s);
        return Future.error(const ApiException(ApiErrorKind.cancelled));
      }
      unawaited(cancel.whenCancel.then((_) => _release(key, s)));
      // Отменивший перестаёт ждать сразу, общий запрос продолжает работать для остальных.
      return Future.any([s.future, cancel.whenCancel.then<Object?>((_) => throw const ApiException(ApiErrorKind.cancelled))]);
    }
    return s.future;
  }

  void _release(String key, _Shared s) {
    s.callers--;
    if (s.callers <= 0 && !s.token.isCancelled) {
      if (identical(_inflight[key], s)) _inflight.remove(key);
      s.token.cancel('Запрос больше никто не ждёт');
    }
  }

  /// Отдельная статическая функция: замыкание для изолята должно захватывать только текст и [extract], а не клиент с Dio.
  static Future<T> _parseInBackground<T>(String text, Decode<T> extract) => Isolate.run(() => extract(jsonDecode(text)));

  static Future<Object?> _guard(Future<Object?> f) async {
    try {
      return await f;
    } on DioException catch (e) {
      throw ApiException.fromDio(e);
    }
  }

  static T _decode<T>(String path, Decode<T> decode, Object? data) {
    try {
      return decode(data);
    } on Object catch (e, s) {
      Log.e('api', 'Ответ $path не того вида', e, s);
      throw ApiException(ApiErrorKind.invalidResponse, cause: e);
    }
  }

  /// null-параметры не отправляются; порядок ключей не важен для ключа общего запроса.
  static Map<String, Object?>? _clean(Map<String, Object?>? query) {
    if (query == null) return null;
    return {
      for (final e in query.entries)
        if (e.value != null) e.key: e.value,
    };
  }

  static String _queryKey(Map<String, Object?>? query) {
    final q = _clean(query);
    if (q == null || q.isEmpty) return '';
    final keys = q.keys.toList()..sort();
    return keys.map((k) => '$k=${q[k]}').join('&');
  }
}

class _Shared {
  _Shared(this.future, this.token);

  final Future<Object?> future;
  final CancelToken token;
  int callers = 0;
}
