import 'dart:async';
import 'dart:convert';
import 'dart:typed_data';

import 'package:dio/dio.dart';

/// Ответ фейкового сервера на запрос.
typedef FakeHandler = FutureOr<ResponseBody> Function(RequestOptions options);

/// HTTP-адаптер Dio для тестов: отвечает обработчиком, запоминает запросы, честно отменяется по CancelToken.
class FakeAdapter implements HttpClientAdapter {
  FakeAdapter(this.handler);

  FakeHandler handler;
  final List<RequestOptions> requests = [];

  @override
  Future<ResponseBody> fetch(RequestOptions options, Stream<Uint8List>? requestStream, Future<void>? cancelFuture) {
    requests.add(options);
    final response = Future.sync(() => handler(options));
    if (cancelFuture == null) return response;
    return Future.any([
      response,
      cancelFuture.then<ResponseBody>(
        (_) => throw DioException(requestOptions: options, type: DioExceptionType.cancel),
      ),
    ]);
  }

  @override
  void close({bool force = false}) {}

  /// JSON-ответ.
  static ResponseBody json(Object? data, {int status = 200}) => ResponseBody.fromString(
        jsonEncode(data),
        status,
        headers: {
          Headers.contentTypeHeader: [Headers.jsonContentType],
        },
      );

  static ResponseBody empty({int status = 204}) => ResponseBody.fromString('', status);

  static ResponseBody text(String body, {int status = 200}) => ResponseBody.fromString(body, status);
}
