import 'dart:math' as math;

import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/api/api_client.dart';
import '../../../core/api/api_providers.dart';
import '../../../core/cache/paged.dart';
import 'production_models.dart';

/// Ключ формы операции: один на открытую форму; повтор с тем же ключом (обрыв связи) сервер не проводит дважды.
String newClientKey() {
  final r = math.Random.secure();
  String hex(int n) => List.generate(n, (_) => r.nextInt(16).toRadixString(16)).join();
  return '${hex(8)}-${hex(4)}-4${hex(3)}-${(8 + r.nextInt(4)).toRadixString(16)}${hex(3)}-${hex(12)}';
}

/// Складская операция: приход, выдача отделу, списание.
enum StockOp { receipt, issue, writeOff }

class ProductionApi {
  ProductionApi(this._api);

  final ApiClient _api;

  static Map<String, Object?> _map(Object? j) => j! as Map<String, Object?>;

  PageChunk<T> _page<T>(Object? j, int page, T Function(Map<String, Object?>) f) {
    final m = _map(j);
    final total = m['total'] as int? ?? 0;
    final size = m['pageSize'] as int? ?? 30;
    return PageChunk([for (final e in m['items']! as List<Object?>) f(_map(e))], hasMore: page * size < total);
  }

  // ───────────── Производство ─────────────

  Future<ProductionDashboard> technologist(CancelToken c) =>
      _api.get('/api/production/technologist', (j) => ProductionDashboard.fromJson(_map(j)), cancel: c);

  Future<ProductionOverview> overview(CancelToken c) => _api.get('/api/production/overview', (j) => ProductionOverview.fromJson(_map(j)), cancel: c);

  Future<PageChunk<ProductionOrderRow>> orders(String? status, int page, CancelToken c) => _api.get(
        '/api/production/orders',
        (j) => _page(j, page, ProductionOrderRow.fromJson),
        query: {'status': status, 'page': page, 'pageSize': 30},
        cancel: c,
      );

  Future<ProductionOrderDetail> order(String id, CancelToken c) =>
      _api.get('/api/production/orders/$id', (j) => ProductionOrderDetail.fromJson(_map(j)), cancel: c);

  Future<PageChunk<ShiftRow>> shifts(String? status, int page, CancelToken c) => _api.get(
        '/api/production/shifts',
        (j) => _page(j, page, ShiftRow.fromJson),
        query: {'status': status, 'page': page, 'pageSize': 30},
        cancel: c,
      );

  /// Выпустить задание (только черновик).
  Future<void> release(String id) => _api.post<void>('/api/production/orders/$id/release');

  /// Закрыть задание: выпуск и версия (чужое изменение → 409, экран перечитывает).
  Future<void> complete(String id, double outputQty, int version, String? comment) =>
      _api.post<void>('/api/production/orders/$id/complete', body: {'outputQty': outputQty, 'version': version, 'comment': comment});

  Future<void> cancel(String id, String comment) => _api.post<void>('/api/production/orders/$id/cancel', body: {'comment': comment});

  /// Оприходовать выпуск на склад (повтор возвращает уже созданный документ).
  Future<String?> receiveOutput(String id) =>
      _api.post('/api/production/orders/$id/receive-output', decode: (j) => _map(j)['number'] as String?);

  // ───────────── Склад ─────────────

  Future<StockSummary> stockSummary(CancelToken c) => _api.get('/api/procurement/stock/summary', (j) => StockSummary.fromJson(_map(j)), cancel: c);

  Future<PageChunk<StockRow>> stock(String q, String? filter, int page, CancelToken c) => _api.get(
        '/api/procurement/stock',
        (j) => _page(j, page, StockRow.fromJson),
        query: {'q': q.trim().isEmpty ? null : q.trim(), 'filter': filter, 'page': page, 'pageSize': 50},
        cancel: c,
      );

  Future<ItemCard> item(String id, CancelToken c) => _api.get('/api/procurement/items/$id', (j) => ItemCard.fromJson(_map(j)), cancel: c);

  Future<PageChunk<MovementRow>> movements(String? kind, int page, CancelToken c) => _api.get(
        '/api/procurement/movements',
        (j) => _page(j, page, MovementRow.fromJson),
        query: {'kind': kind, 'page': page, 'pageSize': 50},
        cancel: c,
      );

  Future<Warehouses> warehouses(CancelToken c) => _api.get('/api/procurement/warehouses', (j) => Warehouses.fromJson(_map(j)), cancel: c);

  Future<List<CatalogItem>> catalog(String q, String? warehouse, CancelToken c) => _api.get(
        '/api/procurement/catalog',
        (j) => [for (final e in j! as List<Object?>) CatalogItem.fromJson(_map(e))],
        query: {'q': q, 'warehouse': warehouse},
        cancel: c,
      );

  /// Проводка документа склада. ⚠ Остатки меняются — только с подтверждением и ответом сервера.
  Future<String?> postStock(StockOp op, Map<String, Object?> body) => _api.post(
        switch (op) {
          StockOp.receipt => '/api/procurement/stock/receipt',
          StockOp.issue => '/api/procurement/stock/issue',
          StockOp.writeOff => '/api/procurement/stock/write-off',
        },
        body: body,
        decode: (j) => j is Map<String, Object?> ? j['id']?.toString() : null,
      );

  // ───────────── Заявки ─────────────

  Future<PageChunk<RequestRow>> requests(String scope, int page, CancelToken c) => _api.get(
        '/api/procurement/requests',
        (j) => _page(j, page, RequestRow.fromJson),
        query: {'scope': scope, 'page': page, 'pageSize': 25, 'sort': 'created', 'desc': true},
        cancel: c,
      );
}

final productionApiProvider = Provider<ProductionApi>((ref) => ProductionApi(ref.watch(apiClientProvider)));
