import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/api/api_client.dart';
import '../../../core/api/api_providers.dart';
import '../../dashboard/data/dashboard_models.dart';
import 'analytik_models.dart';
import 'sales_models.dart';

/// Запросы аналитики к существующим эндпоинтам. Не вызываются: `/api/analytik/dashboard` и `/readiness` (запускают синхронизацию
/// источников), всё с AI (`/api/analytik/chat`, `research`), `/api/sales/visit-sequence` (7,6 с).
class AnalyticsApi {
  AnalyticsApi(this._api);

  final ApiClient _api;

  static Map<String, Object?> _map(Object? j) => j! as Map<String, Object?>;

  static Map<String, Object?> _month(SalesMonth? m) => m == null ? const {} : {'year': m.year, 'month': m.month};

  Future<List<SalesMonth>> months(CancelToken c) =>
      _api.get('/api/sales/months', (j) => [for (final e in j! as List<Object?>) SalesMonth.fromJson(_map(e))], cancel: c);

  Future<SalesOverview> overview(SalesMonth? m, CancelToken c) =>
      _api.get('/api/sales/overview', (j) => SalesOverview.fromJson(_map(j)), query: _month(m), cancel: c);

  /// Республика (`republic`), направление (`directions/{id}`) или «Мои регионы» (`my`).
  Future<GroupView> group(String path, SalesMonth? m, CancelToken c) => _api.getExtract('/api/sales/$path', groupViewOf, query: _month(m), cancel: c);

  Future<RegionView> region(String id, SalesMonth? m, CancelToken c) =>
      _api.getExtract('/api/sales/regions/$id', regionViewOf, query: _month(m), cancel: c);

  Future<AgentsView> agents(SalesMonth? m, CancelToken c) => _api.getExtract('/api/sales/agents', agentsViewOf, query: _month(m), cancel: c);

  Future<AgentView> agent(int id, SalesMonth? m, CancelToken c) =>
      _api.getExtract('/api/sales/agents/$id', agentViewOf, query: _month(m), cancel: c);

  Future<StoreView> store(int id, SalesMonth? m, CancelToken c) =>
      _api.get('/api/sales/stores/$id', (j) => StoreView.fromJson(_map(j)), query: _month(m), cancel: c);

  /// Сравнение периодов модуля: sales | finance | production | hr.
  Future<AnalytikBlock> compare(String module, String period, CancelToken c) =>
      _api.get('/api/analytik/$module', analytikOf, query: {'period': period}, cancel: c);

  Future<AnalytikNav> analytikNav(CancelToken c) => _api.get('/api/analytik/nav', (j) => AnalytikNav.fromJson(_map(j)), cancel: c);

  Future<TechnologistBrief> technologist(CancelToken c) =>
      _api.get('/api/production/technologist', (j) => TechnologistBrief.fromJson(_map(j)), cancel: c);
}

final analyticsApiProvider = Provider<AnalyticsApi>((ref) => AnalyticsApi(ref.watch(apiClientProvider)));
