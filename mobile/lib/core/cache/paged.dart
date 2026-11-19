import 'dart:async';

import 'package:dio/dio.dart';
import 'package:flutter/foundation.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../errors/app_failure.dart';

/// Страница списка с сервера: элементы и есть ли продолжение (номер страницы или курсор).
@immutable
class PageChunk<T> {
  const PageChunk(this.items, {required this.hasMore, this.cursor});

  final List<T> items;
  final bool hasMore;

  /// Курсор следующей страницы (уведомления); у постраничных списков — null.
  final String? cursor;
}

@immutable
class PagedState<T> {
  const PagedState({this.items = const [], this.hasMore = true, this.loading = false, this.failure, this.total});

  final List<T> items;
  final bool hasMore;
  final bool loading;
  final AppFailure? failure;
  final int? total;

  bool get isInitialLoading => items.isEmpty && loading && failure == null;
  bool get isEmpty => items.isEmpty && !loading && failure == null && !hasMore;

  PagedState<T> copyWith({List<T>? items, bool? hasMore, bool? loading, AppFailure? failure, bool clearFailure = false, int? total}) =>
      PagedState<T>(
        items: items ?? this.items,
        hasMore: hasMore ?? this.hasMore,
        loading: loading ?? this.loading,
        failure: clearFailure ? null : (failure ?? this.failure),
        total: total ?? this.total,
      );
}

/// Ленивый список с подгрузкой: первая страница при открытии, следующая — когда список докрутили до конца ([loadMore] вызывает
/// экран). Повторный вызов во время загрузки не создаёт второй запрос; закрыли экран — запрос отменяется; [refresh] — с начала.
/// Длинные списки (уведомления, задачи, сотрудники, движения склада) не держат в памяти больше, чем пользователь посмотрел.
abstract class PagedController<T> extends Notifier<PagedState<T>> {
  /// Загрузить страницу [page] (с 1) или по [cursor].
  Future<PageChunk<T>> fetchPage(int page, String? cursor, CancelToken cancel);

  int _page = 0;
  String? _cursor;
  CancelToken? _cancel;
  Future<void>? _inflight;

  @override
  PagedState<T> build() {
    // Пересборка (сменился фильтр) — список с начала.
    _cancel?.cancel('Новый запрос');
    _page = 0;
    _cursor = null;
    _inflight = null;
    ref.onDispose(() => _cancel?.cancel('Список закрыт'));
    Future.microtask(loadMore);
    return PagedState<T>(loading: true);
  }

  Future<void> loadMore() => _inflight ??= _load().whenComplete(() => _inflight = null);

  Future<void> _load() async {
    if (!ref.mounted || (!state.hasMore && state.items.isNotEmpty)) return;
    final cancel = _cancel = CancelToken();
    state = state.copyWith(loading: true, clearFailure: true);
    try {
      final chunk = await fetchPage(_page + 1, _cursor, cancel);
      if (!ref.mounted || cancel.isCancelled) return;
      _page++;
      _cursor = chunk.cursor;
      state = PagedState<T>(items: [...state.items, ...chunk.items], hasMore: chunk.hasMore, total: state.total);
    } on Object catch (e) {
      if (!ref.mounted || cancel.isCancelled) return;
      state = state.copyWith(loading: false, failure: AppFailure.from(e));
    }
  }

  /// Сначала (потянуть вниз, смена фильтра).
  Future<void> refresh() async {
    _cancel?.cancel('Новый запрос');
    _inflight = null;
    _page = 0;
    _cursor = null;
    state = PagedState<T>(loading: true, total: state.total);
    await loadMore();
  }

  /// Заменить элемент на экране (после действия) без перезагрузки списка.
  @protected
  void replaceWhere(bool Function(T) test, T Function(T) update) {
    state = state.copyWith(items: [for (final i in state.items) test(i) ? update(i) : i]);
  }

  @protected
  void setTotal(int? total) => state = state.copyWith(total: total);
}
