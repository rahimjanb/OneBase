import 'dart:async';

import 'package:dio/dio.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:onebase_mobile/core/api/api_client.dart';
import 'package:onebase_mobile/core/api/api_exception.dart';
import 'package:onebase_mobile/core/api/interceptors.dart';

import '../../helpers/fake_adapter.dart';

/// Клиент как в приложении, но с мгновенными повторами.
({ApiClient api, FakeAdapter server, List<String> unauthorized}) setup(
  FakeHandler handler, {
  String? token = 'T',
  String language = 'ru',
}) {
  final server = FakeAdapter(handler);
  final unauthorized = <String>[];
  final dio = Dio(BaseOptions(baseUrl: 'http://onebase.test'))..httpClientAdapter = server;
  dio.interceptors
    ..add(
      SessionInterceptor(
        token: () => token,
        language: () => language,
        userAgent: 'OneBase-Mobile/test',
        onUnauthorized: () => unauthorized.add('401'),
      ),
    )
    ..add(RetryInterceptor(dio, sleep: (_) async {}));
  return (api: ApiClient(dio), server: server, unauthorized: unauthorized);
}

Object? id(Object? json) => json;

void main() {
  test('ставит токен, язык и приложение в заголовки', () async {
    final s = setup((_) => FakeAdapter.json({'ok': true}), language: 'uz');
    await s.api.get('/api/auth/me', id);
    final h = s.server.requests.single.headers;
    expect(h['Authorization'], 'Bearer T');
    expect(h['Accept-Language'], 'uz');
    expect(h['User-Agent'], 'OneBase-Mobile/test');
  });

  test('вход отправляется без токена', () async {
    final s = setup((_) => FakeAdapter.json({'accessToken': 'x', 'expiresAt': '2036-01-01T00:00:00Z'}));
    await s.api.post('/api/auth/login', body: {'login': '1', 'password': '1'}, decode: id);
    expect(s.server.requests.single.headers.containsKey('Authorization'), isFalse);
  });

  test('одинаковые одновременные GET идут одним запросом, разные — разными', () async {
    final gate = Completer<void>();
    final s = setup((o) async {
      await gate.future;
      return FakeAdapter.json({'path': o.path, 'q': o.queryParameters});
    });
    final a = s.api.get('/api/sales/overview', id, query: {'year': 2026, 'month': 11});
    final b = s.api.get('/api/sales/overview', id, query: {'month': 11, 'year': 2026});
    final c = s.api.get('/api/sales/overview', id, query: {'year': 2026, 'month': 10});
    expect(s.api.inflightCount, 2);
    gate.complete();
    final results = await Future.wait([a, b, c]);
    expect(s.server.requests.length, 2);
    expect(results[0], results[1]);
    expect(s.api.inflightCount, 0);
  });

  test('null-параметры не отправляются', () async {
    final s = setup((_) => FakeAdapter.json({}));
    await s.api.get('/api/finance/overview', id, query: {'organization': null, 'from': '2026-11-01'});
    expect(s.server.requests.single.queryParameters, {'from': '2026-11-01'});
  });

  test('GET повторяется при 503 и при обрыве, затем успех', () async {
    var n = 0;
    final s = setup((o) {
      n++;
      if (n == 1) return FakeAdapter.json({'error': 'down'}, status: 503);
      if (n == 2) throw DioException.connectionError(requestOptions: o, reason: 'reset');
      return FakeAdapter.json({'ok': true});
    });
    expect(await s.api.get('/api/work/overview', id), {'ok': true});
    expect(n, 3);
  });

  test('после двух повторов — ошибка сети', () async {
    final s = setup((o) => throw DioException.connectionError(requestOptions: o, reason: 'offline'));
    await expectLater(
      s.api.get('/api/work/overview', id),
      throwsA(isA<ApiException>().having((e) => e.kind, 'kind', ApiErrorKind.network)),
    );
    expect(s.server.requests.length, 3);
  });

  test('POST не повторяется: операция могла пройти на сервере', () async {
    final s = setup((_) => FakeAdapter.json({'error': 'down'}, status: 503));
    await expectLater(s.api.post('/api/work/tasks', body: {'title': 'x'}), throwsA(isA<ApiException>()));
    expect(s.server.requests.length, 1);
  });

  test('500 не повторяется', () async {
    final s = setup((_) => FakeAdapter.json({'error': 'bug'}, status: 500));
    await expectLater(s.api.get('/api/x', id), throwsA(isA<ApiException>()));
    expect(s.server.requests.length, 1);
  });

  test('401 на запросе с токеном — сигнал «сеанс закрыт»; 401 входа — нет', () async {
    final s = setup((_) => FakeAdapter.json({}, status: 401));
    await expectLater(
      s.api.get('/api/auth/me', id),
      throwsA(isA<ApiException>().having((e) => e.kind, 'kind', ApiErrorKind.unauthorized)),
    );
    expect(s.unauthorized, ['401']);
    await expectLater(s.api.post('/api/auth/login', body: {}), throwsA(isA<ApiException>()));
    expect(s.unauthorized, ['401']);
  });

  test('без токена 401 не считается закрытым сеансом', () async {
    final s = setup((_) => FakeAdapter.json({}, status: 401), token: null);
    await expectLater(s.api.get('/api/auth/me', id), throwsA(isA<ApiException>()));
    expect(s.unauthorized, isEmpty);
  });

  test('ответ не того вида → invalidResponse', () async {
    final s = setup((_) => FakeAdapter.json({'items': 'не список'}));
    await expectLater(
      s.api.get('/api/notifications', (j) => ((j! as Map)['items'] as List).length),
      throwsA(isA<ApiException>().having((e) => e.kind, 'kind', ApiErrorKind.invalidResponse)),
    );
  });

  test('отмена одного ожидающего не отменяет общий запрос; отмена всех — отменяет', () async {
    final gate = Completer<void>();
    final s = setup((_) async {
      await gate.future;
      return FakeAdapter.json({'ok': true});
    });
    final t1 = CancelToken();
    final t2 = CancelToken();
    final first = s.api.get('/api/hr/overview', id, cancel: t1);
    final second = s.api.get('/api/hr/overview', id, cancel: t2);
    t1.cancel();
    await expectLater(first, throwsA(isA<ApiException>().having((e) => e.kind, 'kind', ApiErrorKind.cancelled)));
    gate.complete();
    expect(await second, {'ok': true});

    final gate2 = Completer<void>();
    s.server.handler = (_) async {
      await gate2.future;
      return FakeAdapter.json({'ok': true});
    };
    final t3 = CancelToken();
    final third = s.api.get('/api/hr/overview', id, cancel: t3);
    t3.cancel();
    await expectLater(third, throwsA(isA<ApiException>()));
    expect(s.api.inflightCount, 0);
    gate2.complete();
  });

  test('getExtract разбирает JSON в фоновом изоляте и возвращает срез', () async {
    final s = setup((_) => FakeAdapter.text('{"calendar":{"totalDays":[1,2,null,4]},"rows":[1,2,3]}'));
    final days = await s.api.getExtract('/api/sales/republic', _totalDays);
    expect(days, [1, 2, null, 4]);
  });
}

List<num?> _totalDays(Object? json) =>
    (((json! as Map)['calendar'] as Map)['totalDays'] as List).map((e) => e as num?).toList();
