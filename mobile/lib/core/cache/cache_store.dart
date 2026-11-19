import 'dart:convert';

import 'package:drift/drift.dart';
import 'package:flutter/foundation.dart';

import '../database/app_database.dart';
import '../logging/log.dart';

/// Сохранённый ответ: JSON и время, когда он пришёл с сервера.
@immutable
class CachedJson {
  const CachedJson(this.json, this.savedAt);

  final Object? json;
  final DateTime savedAt;
}

/// Кэш последних успешных ответов в SQLite. Сбой кэша не ломает экран: чтение вернёт null, запись промолчит (в журнал).
class CacheStore {
  CacheStore(this._db);

  final AppDatabase _db;

  Future<CachedJson?> read(String key, {required String userId}) async {
    try {
      final row = await (_db.select(_db.cacheEntries)..where((t) => t.key.equals(key) & t.userId.equals(userId)))
          .getSingleOrNull();
      if (row == null) return null;
      return CachedJson(jsonDecode(row.json), row.savedAt);
    } on Object catch (e, s) {
      Log.e('cache', 'Не прочитан $key', e, s);
      return null;
    }
  }

  Future<void> write(String key, Object? json, {required String userId, DateTime? savedAt}) async {
    try {
      await _db.into(_db.cacheEntries).insertOnConflictUpdate(
            CacheEntriesCompanion.insert(
              key: key,
              userId: userId,
              json: jsonEncode(json),
              savedAt: savedAt ?? DateTime.now(),
            ),
          );
    } on Object catch (e, s) {
      Log.e('cache', 'Не записан $key', e, s);
    }
  }

  /// Удалить всё, кроме кэша пользователя [keepUserId] (вход другого пользователя), или всё целиком (выход).
  Future<void> clear({String? keepUserId}) async {
    final q = _db.delete(_db.cacheEntries);
    if (keepUserId != null) q.where((t) => t.userId.equals(keepUserId).not());
    await q.go();
  }

  /// Сбросить кэш по началу ключа (`dashboard/` …) — после изменения данных, которые в нём лежат.
  Future<void> invalidate(String prefix) async {
    try {
      await (_db.delete(_db.cacheEntries)..where((t) => t.key.like('${_escape(prefix)}%', escapeChar: r'\'))).go();
    } on Object catch (e, s) {
      Log.e('cache', 'Не сброшен $prefix*', e, s);
    }
  }

  /// Удалить записи старше [maxAge] — кэш не растёт бесконечно (вызывается при запуске).
  Future<int> trim(Duration maxAge, {DateTime? now}) async {
    try {
      final edge = (now ?? DateTime.now()).subtract(maxAge);
      return await (_db.delete(_db.cacheEntries)..where((t) => t.savedAt.isSmallerThanValue(edge))).go();
    } on Object catch (e, s) {
      Log.e('cache', 'Не очищен старый кэш', e, s);
      return 0;
    }
  }

  static String _escape(String s) => s.replaceAll(r'\', r'\\').replaceAll('%', r'\%').replaceAll('_', r'\_');

  Future<int> count() async {
    final c = _db.cacheEntries.key.count();
    return (await (_db.selectOnly(_db.cacheEntries)..addColumns([c])).getSingle()).read(c) ?? 0;
  }
}
