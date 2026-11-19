import 'package:flutter_test/flutter_test.dart';
import 'package:onebase_mobile/core/cache/cache_store.dart';
import 'package:onebase_mobile/core/cache/repo_cache.dart';

import '../../helpers/test_db.dart';

void main() {
  test('ошибка запроса доходит до обоих ожидающих и не остаётся необработанной', () async {
    final db = memoryDb();
    addTearDown(db.close);
    final now = DateTime(2026, 11, 20, 10);
    final cache = RepoCache(CacheStore(db), () => now, () => 'u-1');
    var calls = 0;
    Future<int> failing() async {
      calls++;
      await Future<void>.delayed(const Duration(milliseconds: 5));
      throw StateError('нет');
    }

    final a = cache.get<int>('k', maxAge: const Duration(minutes: 1), fetch: failing, encode: (v) => v, decode: (j) => j! as int);
    final b = cache.get<int>('k', maxAge: const Duration(minutes: 1), fetch: failing, encode: (v) => v, decode: (j) => j! as int);
    await expectLater(a, throwsStateError);
    await expectLater(b, throwsStateError);
    expect(calls, 1);
  });

  test('свежий ответ — из кэша без сети; устаревший — заново; live-ключи сбрасываются', () async {
    final db = memoryDb();
    addTearDown(db.close);
    var now = DateTime(2026, 11, 20, 10);
    final cache = RepoCache(CacheStore(db), () => now, () => 'u-1');
    var calls = 0;
    Future<int> fetch() async => ++calls;
    Future<int> get(String key) => cache.get<int>(key, maxAge: const Duration(minutes: 1), fetch: fetch, encode: (v) => v, decode: (j) => j! as int);

    expect(await get('home/live/x'), 1);
    expect(await get('home/live/x'), 1);
    now = now.add(const Duration(minutes: 2));
    expect(await get('home/live/x'), 2);
    expect(await get('home/closed'), 3);
    await cache.invalidate('home/live/');
    expect(await get('home/live/x'), 4);
    expect(await get('home/closed'), 3);

    // Новая память (перезапуск) — свежая запись читается из SQLite.
    final restarted = RepoCache(CacheStore(db), () => now, () => 'u-1');
    expect(await restarted.get<int>('home/closed', maxAge: const Duration(days: 7), fetch: fetch, encode: (v) => v, decode: (j) => j! as int), 3);
    // Другой пользователь своё не видит.
    final other = RepoCache(CacheStore(db), () => now, () => 'u-2');
    expect(await other.get<int>('home/closed', maxAge: const Duration(days: 7), fetch: fetch, encode: (v) => v, decode: (j) => j! as int), 5);
  });
}
