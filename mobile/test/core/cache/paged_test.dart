import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:onebase_mobile/core/api/api_exception.dart';
import 'package:onebase_mobile/core/cache/paged.dart';
import 'package:onebase_mobile/core/utils/debouncer.dart';

var _calls = <int>[];
var _failNext = false;

class _Numbers extends PagedController<int> {
  @override
  Future<PageChunk<int>> fetchPage(int page, String? cursor, CancelToken cancel) async {
    _calls.add(page);
    if (_failNext) {
      _failNext = false;
      throw const ApiException(ApiErrorKind.network);
    }
    final items = [for (var i = 0; i < 10; i++) (page - 1) * 10 + i];
    return PageChunk(items, hasMore: page < 3);
  }
}

final _numbers = NotifierProvider.autoDispose<_Numbers, PagedState<int>>(_Numbers.new);

void main() {
  setUp(() {
    _calls = [];
    _failNext = false;
  });

  test('первая страница при открытии, следующие — по запросу, конец списка — без лишних запросов', () async {
    final c = ProviderContainer.test();
    c.listen(_numbers, (_, _) {});
    await pumpEventQueue();
    expect(c.read(_numbers).items, hasLength(10));
    // Двойной вызов во время загрузки — один запрос.
    await Future.wait([c.read(_numbers.notifier).loadMore(), c.read(_numbers.notifier).loadMore()]);
    expect(c.read(_numbers).items, hasLength(20));
    await c.read(_numbers.notifier).loadMore();
    expect(c.read(_numbers).hasMore, isFalse);
    await c.read(_numbers.notifier).loadMore();
    expect(_calls, [1, 2, 3]);
  });

  test('ошибка — список остаётся, причина видна; refresh — с начала', () async {
    final c = ProviderContainer.test();
    c.listen(_numbers, (_, _) {});
    await pumpEventQueue();
    _failNext = true;
    await c.read(_numbers.notifier).loadMore();
    expect(c.read(_numbers).items, hasLength(10));
    expect(c.read(_numbers).failure?.kind, ApiErrorKind.network);
    await c.read(_numbers.notifier).refresh();
    expect(c.read(_numbers).items.first, 0);
    expect(c.read(_numbers).failure, isNull);
  });

  test('поиск ждёт паузы во вводе', () async {
    final d = Debouncer(const Duration(milliseconds: 30));
    var runs = 0;
    d(() => runs++);
    d(() => runs++);
    d(() => runs++);
    expect(runs, 0);
    await Future<void>.delayed(const Duration(milliseconds: 60));
    expect(runs, 1);
  });
}
