import 'dart:async';

import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:onebase_mobile/core/api/api_exception.dart';
import 'package:onebase_mobile/core/auth/auth_controller.dart';
import 'package:onebase_mobile/core/auth/user_profile.dart';
import 'package:onebase_mobile/core/cache/cache_store.dart';
import 'package:onebase_mobile/core/cache/cached_block.dart';
import 'package:onebase_mobile/core/database/app_database.dart';

import '../../helpers/fake_server.dart';
import '../../helpers/harness.dart';
import '../../helpers/test_db.dart';

/// Ответы сервера для тестового блока: по очереди, каждый — значение или ошибка.
final _responses = <FutureOr<int> Function()>[];
var _fetches = 0;
final _cancels = <CancelToken>[];

class _CounterBlock extends CachedBlock<int> {
  @override
  String get cacheKey => 'test/counter';

  @override
  Future<int> fetch(CancelToken cancel) async {
    _fetches++;
    _cancels.add(cancel);
    return _responses.removeAt(0)();
  }

  @override
  int decode(Object? json) => json! as int;

  @override
  Object? encode(int data) => data;
}

final _block = NotifierProvider.autoDispose<_CounterBlock, BlockState<int>>(_CounterBlock.new);

void main() {
  late AppDatabase db;
  late StreamController<bool> online;
  final profile = UserProfile.fromJson(profileJson());

  setUp(() {
    db = memoryDb();
    online = StreamController<bool>.broadcast();
    _responses.clear();
    _cancels.clear();
    _fetches = 0;
  });
  tearDown(() async {
    await online.close();
    await db.close();
  });

  ProviderContainer make() => ProviderContainer.test(
        retry: (_, _) => null,
        overrides: [
          ...baseOverrides(db: db, clock: () => DateTime(2026, 11, 19, 10), onlineStream: online.stream),
          currentProfileProvider.overrideWithValue(profile),
        ],
      );

  Future<void> flush() async {
    for (var i = 0; i < 20; i++) {
      await Future<void>.delayed(Duration.zero);
    }
  }

  test('первая загрузка: заготовка → данные с сервера → в кэш пользователя', () async {
    _responses.add(() => 42);
    final c = make();
    final sub = c.listen(_block, (_, _) {});
    expect(c.read(_block).isInitialLoading, isTrue);
    await flush();
    final s = c.read(_block);
    expect(s.data, 42);
    expect(s.loading, isFalse);
    expect(s.syncedAt, DateTime(2026, 11, 19, 10));
    expect((await CacheStore(db).read('test/counter', userId: profile.id))?.json, 42);
    sub.close();
  });

  test('кэш показывается сразу, затем заменяется ответом сервера', () async {
    await CacheStore(db).write('test/counter', 7, userId: profile.id, savedAt: DateTime(2026, 11, 18));
    final gate = Completer<int>();
    _responses.add(() => gate.future);
    final c = make();
    final sub = c.listen(_block, (_, _) {});
    await flush();
    expect(c.read(_block).data, 7);
    expect(c.read(_block).fromCache, isTrue);
    expect(c.read(_block).stale, isTrue);
    gate.complete(8);
    await flush();
    expect(c.read(_block).data, 8);
    expect(c.read(_block).stale, isFalse);
    sub.close();
  });

  test('сервер недоступен — данные из кэша остаются, ошибка рядом', () async {
    await CacheStore(db).write('test/counter', 7, userId: profile.id, savedAt: DateTime(2026, 11, 18));
    _responses.add(() => throw const ApiException(ApiErrorKind.network));
    final c = make();
    final sub = c.listen(_block, (_, _) {});
    await flush();
    final s = c.read(_block);
    expect(s.data, 7);
    expect(s.failure?.kind, ApiErrorKind.network);
    expect(s.syncedAt, DateTime(2026, 11, 18));
    expect(s.stale, isTrue);
    sub.close();
  });

  test('нет права (403) — блок скрывается', () async {
    _responses.add(() => throw const ApiException(ApiErrorKind.forbidden, status: 403));
    final c = make();
    final sub = c.listen(_block, (_, _) {});
    await flush();
    expect(c.read(_block).forbidden, isTrue);
    sub.close();
  });

  test('ошибка без кэша, затем «Повторить» — данные', () async {
    _responses
      ..add(() => throw const ApiException(ApiErrorKind.server, status: 503))
      ..add(() => 5);
    final c = make();
    final sub = c.listen(_block, (_, _) {});
    await flush();
    expect(c.read(_block).failure?.kind, ApiErrorKind.server);
    expect(c.read(_block).data, isNull);
    await c.read(_block.notifier).refresh();
    expect(c.read(_block).data, 5);
    expect(c.read(_block).failure, isNull);
    sub.close();
  });

  test('повторное обновление во время запроса не создаёт второй запрос', () async {
    final gate = Completer<int>();
    _responses.add(() => gate.future);
    final c = make();
    final sub = c.listen(_block, (_, _) {});
    await flush();
    final a = c.read(_block.notifier).refresh();
    final b = c.read(_block.notifier).refresh();
    gate.complete(1);
    await Future.wait([a, b]);
    expect(_fetches, 1);
    sub.close();
  });

  test('сеть вернулась — блок обновляется сам', () async {
    _responses
      ..add(() => throw const ApiException(ApiErrorKind.network))
      ..add(() => 9);
    final c = make();
    final sub = c.listen(_block, (_, _) {});
    online.add(false);
    await flush();
    expect(c.read(_block).failure, isNotNull);
    online.add(true);
    await flush();
    expect(c.read(_block).data, 9);
    sub.close();
  });

  test('блок закрыт — запрос отменяется', () async {
    final gate = Completer<int>();
    _responses.add(() => gate.future);
    final c = make();
    final sub = c.listen(_block, (_, _) {});
    await flush();
    sub.close();
    await flush();
    expect(_cancels.single.isCancelled, isTrue);
    gate.complete(1);
  });
}
