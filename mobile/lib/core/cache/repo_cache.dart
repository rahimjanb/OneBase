import 'dart:async';

import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../auth/auth_controller.dart';
import '../core_providers.dart';
import '../logging/log.dart';
import 'cache_store.dart';

/// Кэш на уровне репозитория: ответ живёт в памяти и в SQLite со сроком годности.
/// - Свежий ответ (моложе [maxAge]) отдаётся без сети — так закрытые месяцы продаж и прошлые недели HR не качаются повторно.
/// - Одинаковые запросы, начатые одновременно, идут одним (блоки главной просят один и тот же месяц).
/// - Кэш привязан к пользователю; [invalidate] сбрасывает его по началу ключа после изменения данных.
/// - [persist] = false — только в памяти (данные с персональными сведениями не пишутся на диск).
class RepoCache {
  RepoCache(this._store, this._clock, this._userId);

  final CacheStore _store;
  final DateTime Function() _clock;
  final String? Function() _userId;

  static const _memoryLimit = 64;
  final _memory = <String, ({Object? value, DateTime at})>{};
  final _inflight = <String, Future<Object?>>{};

  Future<T> get<T>(
    String key, {
    required Duration maxAge,
    required Future<T> Function() fetch,
    required Object? Function(T) encode,
    required T Function(Object?) decode,
    bool persist = true,
    bool force = false,
  }) async {
    final uid = _userId();
    final full = '${uid ?? '-'}|$key';
    final now = _clock();
    if (!force) {
      final mem = _memory[full];
      if (mem != null && now.difference(mem.at) < maxAge) return mem.value as T;
      if (persist && uid != null && mem == null) {
        final row = await _store.read(key, userId: uid);
        if (row != null && now.difference(row.savedAt) < maxAge) {
          try {
            final value = decode(row.json);
            _remember(full, value, row.savedAt);
            return value;
          } on Object catch (e) {
            Log.w('cache', 'Кэш $key не того вида — запрашиваю заново', e);
          }
        }
      }
    }
    final running = _inflight[full];
    if (running != null) return await running as T;
    final future = fetch();
    _inflight[full] = future;
    try {
      final value = await future;
      final at = _clock();
      _remember(full, value, at);
      if (persist && uid != null) await _store.write(key, encode(value), userId: uid, savedAt: at);
      return value;
    } finally {
      unawaited(Future<void>.microtask(() {
        _inflight.remove(full);
      }));
    }
  }

  void _remember(String key, Object? value, DateTime at) {
    _memory.remove(key);
    _memory[key] = (value: value, at: at);
    while (_memory.length > _memoryLimit) {
      _memory.remove(_memory.keys.first);
    }
  }

  /// Сбросить кэш по началу ключа (в памяти и на диске).
  Future<void> invalidate(String prefix) async {
    _memory.removeWhere((k, _) => k.substring(k.indexOf('|') + 1).startsWith(prefix));
    await _store.invalidate(prefix);
  }

  void clearMemory() => _memory.clear();
}

final repoCacheProvider = Provider<RepoCache>((ref) {
  final cache = RepoCache(ref.watch(cacheStoreProvider), ref.watch(clockProvider), () => ref.read(currentProfileProvider)?.id);
  // Другой пользователь — память кэша не переносится.
  ref.listen(currentProfileProvider.select((p) => p?.id), (prev, next) {
    if (prev != next) cache.clearMemory();
  });
  return cache;
});
