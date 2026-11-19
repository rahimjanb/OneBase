import 'package:dio/dio.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:onebase_mobile/core/api/api_client.dart';
import 'package:onebase_mobile/core/synchronization/outbox.dart';

import '../../helpers/fake_adapter.dart';
import '../../helpers/fake_server.dart';
import '../../helpers/test_db.dart';

void main() {
  late FakeServer server;
  late Outbox outbox;
  var user = 'u-1';

  setUp(() {
    final db = memoryDb();
    addTearDown(db.close);
    server = FakeServer();
    final dio = Dio(BaseOptions(baseUrl: 'http://onebase.test'))..httpClientAdapter = server.adapter;
    outbox = Outbox(db, ApiClient(dio, parseInBackground: false), () => user, () => DateTime(2026, 11, 20));
    addTearDown(outbox.dispose);
  });

  Future<SendResult> markRead(String id) =>
      outbox.send(kind: 'notification.read', entity: 'n:$id', method: 'POST', path: '/api/notifications/$id/read');

  test('есть сеть — уходит сразу, очередь пустая', () async {
    server.on('POST', '/api/notifications/1/read', (_) => FakeAdapter.empty());
    expect(await markRead('1'), SendResult.sent);
    expect(await outbox.list(), isEmpty);
  });

  test('нет сети — остаётся в очереди и уходит при следующем проходе, по порядку', () async {
    server
      ..offline('POST', '/api/notifications/1/read')
      ..offline('POST', '/api/notifications/2/read');
    expect(await markRead('1'), SendResult.queued);
    expect(await markRead('2'), SendResult.queued);
    expect((await outbox.list()).map((o) => o.entity), ['n:1', 'n:2']);

    server
      ..on('POST', '/api/notifications/1/read', (_) => FakeAdapter.empty())
      ..on('POST', '/api/notifications/2/read', (_) => FakeAdapter.empty());
    expect(await outbox.flush(), 2);
    expect(await outbox.list(), isEmpty);
  });

  test('новая операция над той же сущностью заменяет ждущую', () async {
    server.offline('PUT', '/api/notifications/preferences');
    Future<SendResult> prefs(List<String> muted) => outbox.send(
          kind: 'notification.prefs',
          entity: 'prefs',
          method: 'PUT',
          path: '/api/notifications/preferences',
          body: {'muted': muted},
        );
    await prefs(['ai']);
    await prefs(['ai', 'hr']);
    final ops = await outbox.list();
    expect(ops, hasLength(1));
    expect(ops.single.body, {'muted': ['ai', 'hr']});
  });

  test('сервер отказал — не повторяется, помечена для пользователя; очередь другого пользователя не видна', () async {
    server.json('POST', '/api/notifications/9/read', {'error': 'Уведомление не найдено'}, status: 404);
    expect(await markRead('9'), SendResult.rejected);
    final failed = (await outbox.list()).single;
    expect(failed.failed, isTrue);
    expect(failed.error, 'Уведомление не найдено');
    expect(await outbox.flush(), 0);
    expect(server.count('POST', '/api/notifications/9/read'), 1);
    user = 'u-2';
    expect(await outbox.list(), isEmpty);
    user = 'u-1';
  });
}
