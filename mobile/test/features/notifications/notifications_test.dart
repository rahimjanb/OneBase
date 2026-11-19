import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:onebase_mobile/core/api/api_client.dart';
import 'package:onebase_mobile/core/api/api_providers.dart';
import 'package:onebase_mobile/core/auth/auth_controller.dart';
import 'package:onebase_mobile/core/auth/user_profile.dart';
import 'package:onebase_mobile/core/core_providers.dart';
import 'package:onebase_mobile/core/navigation/web_links.dart';
import 'package:onebase_mobile/core/network/connectivity.dart';
import 'package:onebase_mobile/core/synchronization/outbox.dart';
import 'package:onebase_mobile/features/notifications/presentation/notifications_screen.dart';

import '../../helpers/fake_server.dart';
import '../../helpers/test_db.dart';

void main() {
  test('группы по дням Ташкента; ссылки уведомлений → экраны', () {
    final now = DateTime.utc(2026, 11, 20, 9);
    expect(dayGroup('2026-11-20T03:00:00+00:00', now), 0);
    expect(dayGroup('2026-11-19T20:00:00+00:00', now), 0); // 01:00 20-го по Ташкенту
    expect(dayGroup('2026-11-19T10:00:00+00:00', now), 1);
    expect(dayGroup('2026-11-10T10:00:00+00:00', now), 2);
    expect(appRouteForWeb('/tasks/task/abc'), '/tasks/abc');
    expect(appRouteForWeb('/sales/stores/29519'), '/home/sales/points/29519');
    expect(appRouteForWeb('/hr/recruitment/telegram?filter=need_hr'), '/home/hr');
    expect(appRouteForWeb('/settings/ai/usage'), '/profile');
  });

  test('прочитано без сети: сразу на экране, запрос ждёт в очереди и уходит при появлении сети', () async {
    final db = memoryDb();
    addTearDown(db.close);
    final server = FakeServer()
      ..json('GET', '/api/notifications', {
        'items': [{'id': 'n1', 'type': 'task_overdue', 'category': 'tasks', 'title': 'Просрочена задача', 'createdAt': '2026-11-20T03:00:00+00:00', 'read': false, 'severity': 'critical'}],
        'nextCursor': null,
        'counts': {'all': 1, 'unread': 1},
        'unread': 1,
      })
      ..json('GET', '/api/notifications/counts', {'all': 1, 'unread': 1, 'tasks': 1, 'ai': 0, 'hr': 0})
      ..offline('POST', '/api/notifications/n1/read');
    final dio = Dio(BaseOptions(baseUrl: 'http://onebase.test'))..httpClientAdapter = server.adapter;
    final c = ProviderContainer.test(overrides: [
      apiClientProvider.overrideWithValue(ApiClient(dio, parseInBackground: false)),
      appDatabaseProvider.overrideWithValue(db),
      currentProfileProvider.overrideWithValue(const UserProfile(id: 'u-1', login: 'u', name: 'Тест')),
      isOnlineProvider.overrideWith((ref) => Stream.value(true)),
    ]);
    c.listen(notificationsProvider('all'), (_, _) {});
    c.listen(unreadProvider, (_, _) {});
    await pumpEventQueue();
    final n = c.read(notificationsProvider('all')).items.single;
    expect(c.read(unreadProvider)?.unread, 1);

    final result = await c.read(notificationsProvider('all').notifier).setRead(n, true);
    expect(result, SendResult.queued);
    expect(c.read(notificationsProvider('all')).items.single.read, isTrue);
    expect(c.read(unreadProvider)?.unread, 0);
    expect((await c.read(outboxProvider).list()).single.path, '/api/notifications/n1/read');

    server.json('POST', '/api/notifications/n1/read', null, status: 204);
    expect(await c.read(outboxProvider).flush(), 1);
    expect(await c.read(outboxProvider).list(), isEmpty);
  });
}
