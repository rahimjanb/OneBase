import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/api/api_client.dart';
import '../../../core/api/api_providers.dart';
import '../../../core/cache/paged.dart';
import '../../dashboard/data/dashboard_models.dart';
import 'finance_models.dart';

/// Фильтр финансов: период (yyyy-MM-dd) и организация 1С (null — все, сводно).
typedef FinFilter = ({String from, String to, String? organization});

class FinanceApi {
  FinanceApi(this._api);

  final ApiClient _api;

  static Map<String, Object?> _map(Object? j) => j! as Map<String, Object?>;

  static Map<String, Object?> _q(FinFilter f) => {'from': f.from, 'to': f.to, 'organization': f.organization};

  Future<List<FinanceOrganization>> organizations(CancelToken c) =>
      _api.get('/api/finance/organizations', (j) => [for (final e in j! as List<Object?>) FinanceOrganization.fromJson(_map(e))], cancel: c);

  Future<FinOverview> overview(FinFilter f, CancelToken c) => _api.get('/api/finance/overview', (j) => FinOverview.fromJson(_map(j)), query: _q(f), cancel: c);

  Future<FinPnl> pnl(FinFilter f, CancelToken c) => _api.get('/api/finance/pnl', (j) => FinPnl.fromJson(_map(j)), query: _q(f), cancel: c);

  Future<FinCash> cash(FinFilter f, CancelToken c) => _api.get('/api/finance/cash', (j) => FinCash.fromJson(_map(j)), query: _q(f), cancel: c);

  Future<FinSettlements> settlements(FinFilter f, String kind, CancelToken c) => _api.get(
        '/api/finance/settlements',
        (j) => FinSettlements.fromJson(_map(j)),
        query: {..._q(f), 'kind': kind, 'top': 300},
        cancel: c,
      );

  /// Отклонения — тяжёлый расчёт на сервере: только при открытии вкладки, без автообновления.
  Future<FinAnomalies> anomalies(FinFilter f, CancelToken c) =>
      _api.get('/api/finance/anomalies', (j) => FinAnomalies.fromJson(_map(j)), query: _q(f), cancel: c);

  Future<FinAccounts> accounts(FinFilter f, CancelToken c) =>
      _api.get('/api/finance/accounts', (j) => FinAccounts.fromJson(_map(j)), query: _q(f), cancel: c);

  Future<FinDocument> document(String id, CancelToken c) => _api.get('/api/finance/documents/$id', (j) => FinDocument.fromJson(_map(j)), cancel: c);

  Future<PageChunk<EntryView>> entries(FinFilter f, Map<String, String> filter, int page, CancelToken c) => _api.get(
        '/api/finance/entries',
        (j) {
          final m = _map(j);
          final total = m['total'] as int? ?? 0;
          final size = m['pageSize'] as int? ?? 50;
          return PageChunk([for (final e in m['items']! as List<Object?>) EntryView.fromJson(_map(e))], hasMore: page * size < total);
        },
        query: {..._q(f), ...filter, 'page': page, 'pageSize': 50},
        cancel: c,
      );
}

final financeApiProvider = Provider<FinanceApi>((ref) => FinanceApi(ref.watch(apiClientProvider)));
