import 'package:flutter_test/flutter_test.dart';
import 'package:onebase_mobile/core/cache/cache_store.dart';
import 'package:onebase_mobile/core/database/app_database.dart';

import '../../helpers/test_db.dart';

void main() {
  late AppDatabase db;
  late CacheStore cache;

  setUp(() {
    db = memoryDb();
    cache = CacheStore(db);
  });
  tearDown(() => db.close());

  test('записывает и читает срез с временем', () async {
    final at = DateTime(2026, 11, 19, 9, 30);
    await cache.write('dashboard/sales/2026-11', {'revenue': 1.5, 'akb': 7753}, userId: 'u1', savedAt: at);
    final read = await cache.read('dashboard/sales/2026-11', userId: 'u1');
    expect(read?.json, {'revenue': 1.5, 'akb': 7753});
    expect(read?.savedAt, at);
  });

  test('чужой кэш не читается', () async {
    await cache.write('k', {'a': 1}, userId: 'u1');
    expect(await cache.read('k', userId: 'u2'), isNull);
  });

  test('перезапись по тому же ключу', () async {
    await cache.write('k', {'a': 1}, userId: 'u1');
    await cache.write('k', {'a': 2}, userId: 'u1');
    expect((await cache.read('k', userId: 'u1'))?.json, {'a': 2});
    expect(await cache.count(), 1);
  });

  test('очистка: всё или всё, кроме текущего пользователя', () async {
    await cache.write('a', 1, userId: 'u1');
    await cache.write('b', 2, userId: 'u2');
    await cache.clear(keepUserId: 'u2');
    expect(await cache.read('a', userId: 'u1'), isNull);
    expect((await cache.read('b', userId: 'u2'))?.json, 2);
    await cache.clear();
    expect(await cache.count(), 0);
  });
}
