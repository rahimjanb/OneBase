import 'dart:async';

import 'package:dio/dio.dart';
import 'package:flutter/foundation.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../api/api_exception.dart';
import '../auth/auth_controller.dart';
import '../core_providers.dart';
import '../errors/app_failure.dart';
import '../logging/log.dart';
import '../network/connectivity.dart';

/// Состояние блока экрана: данные (с сервера или из кэша), когда они пришли с сервера, идёт ли загрузка и последняя ошибка.
/// Ошибка не стирает данные: блок показывает последнее успешное состояние и пометку, что обновить не удалось.
@immutable
class BlockState<T> {
  const BlockState({this.data, this.syncedAt, this.loading = false, this.failure, this.fromCache = false});

  final T? data;
  final DateTime? syncedAt;
  final bool loading;
  final AppFailure? failure;

  /// Данные из кэша телефона, сервер в этот запуск их ещё не подтвердил.
  final bool fromCache;

  /// Первая загрузка: показать заготовку.
  bool get isInitialLoading => data == null && loading && failure == null;

  /// Нет права на раздел (403) — блок не показывается.
  bool get forbidden => failure?.kind == ApiErrorKind.forbidden;

  /// Показаны старые данные: последний запрос не удался или ещё идёт после кэша.
  bool get stale => data != null && (failure != null || fromCache);

  BlockState<T> copyWith({bool? loading}) => BlockState<T>(
        data: data,
        syncedAt: syncedAt,
        loading: loading ?? this.loading,
        failure: failure,
        fromCache: fromCache,
      );
}

/// Блок с кэшем последнего успешного ответа:
/// 1. Сразу показывает кэш (если есть) со временем синхронизации.
/// 2. Параллельно спрашивает сервер.
/// 3. Успех — новые данные в экран и в кэш. Ошибка — кэш остаётся, блок показывает ошибку и «Повторить».
///
/// Запрос отменяется, когда блок больше не нужен. Повторный [refresh] во время запроса не создаёт второй запрос.
/// Сеть вернулась — блок обновляется сам.
abstract class CachedBlock<T> extends Notifier<BlockState<T>> {
  /// Ключ кэша: блок и параметры («dashboard/sales/2026-11»). Пользователь добавляется сам.
  String get cacheKey;

  Future<T> fetch(CancelToken cancel);

  T decode(Object? json);

  Object? encode(T data);

  /// Сохранять ли ответ на телефоне. Нет — для данных с персональными сведениями (тексты событий с именами сотрудников и
  /// кандидатов): без сети такой блок не показывается.
  bool get persist => true;

  CancelToken? _cancel;
  Future<void>? _inflight;

  @override
  BlockState<T> build() {
    ref.onDispose(() => _cancel?.cancel('Блок закрыт'));
    ref.listen<AsyncValue<bool>>(isOnlineProvider, (prev, next) {
      if (prev?.value == false && next.value == true) unawaited(refresh());
    });
    Future.microtask(_start);
    return BlockState<T>(loading: true);
  }

  String? get _userId => ref.read(currentProfileProvider)?.id;

  Future<void> _start() async {
    if (!ref.mounted) return;
    final userId = _userId;
    if (userId != null && persist) {
      final cached = await ref.read(cacheStoreProvider).read(cacheKey, userId: userId);
      if (!ref.mounted) return;
      if (cached != null && state.data == null) {
        try {
          state = BlockState<T>(data: decode(cached.json), syncedAt: cached.savedAt, loading: true, fromCache: true);
        } on Object catch (e) {
          Log.w('cache', 'Кэш $cacheKey не того вида — пропущен', e);
        }
      }
    }
    await refresh();
  }

  /// Обновить с сервера (pull-to-refresh, «Повторить», возвращение сети).
  Future<void> refresh() => _inflight ??= _refresh().whenComplete(() => _inflight = null);

  Future<void> _refresh() async {
    if (!ref.mounted) return;
    _cancel?.cancel('Новый запрос');
    final cancel = _cancel = CancelToken();
    state = state.copyWith(loading: true);
    try {
      final data = await fetch(cancel);
      if (!ref.mounted || cancel.isCancelled) return;
      final now = ref.read(clockProvider)();
      state = BlockState<T>(data: data, syncedAt: now);
      final userId = _userId;
      if (userId != null && persist) await ref.read(cacheStoreProvider).write(cacheKey, encode(data), userId: userId, savedAt: now);
    } on Object catch (e) {
      if (!ref.mounted || cancel.isCancelled) return;
      final failure = AppFailure.from(e);
      if (failure.kind == ApiErrorKind.cancelled) return;
      if (e is! ApiException) Log.e('block', 'Блок $cacheKey не загрузился', e);
      state = BlockState<T>(data: state.data, syncedAt: state.syncedAt, failure: failure, fromCache: state.fromCache);
    }
  }
}
