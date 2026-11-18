import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/api/api_client.dart';
import '../../../core/api/api_providers.dart';
import '../domain/dashboard_period.dart';
import 'dashboard_models.dart';

/// Запросы Dashboard к существующим эндпоинтам OneBase (новых на сервере нет). Каждый блок — свой запрос.
class DashboardApi {
  DashboardApi(this._api);

  final ApiClient _api;

  static Map<String, Object?> _map(Object? j) => j! as Map<String, Object?>;

  static List<Map<String, Object?>> _list(Object? j) => (j! as List<Object?>).cast<Map<String, Object?>>();

  /// Продажи месяца: выручка, кг, АКБ, план и прогноз (`sales.read`, уровень организации).
  Future<SalesOverview> salesOverview(DashboardPeriod p, CancelToken cancel) =>
      _api.get('/api/sales/overview', (j) => SalesOverview.fromJson(_map(j)), query: p.salesQuery, cancel: cancel);

  /// Кг по дням — срез тяжёлого `/api/sales/republic`, разбирается в фоновом изоляте.
  Future<SalesDaily> salesDaily(DashboardPeriod p, CancelToken cancel) =>
      _api.getExtract('/api/sales/republic', salesDailyOf, query: p.salesQuery, cancel: cancel);

  Future<List<SalesMonth>> salesMonths(CancelToken cancel) =>
      _api.get('/api/sales/months', (j) => _list(j).map(SalesMonth.fromJson).toList(), cancel: cancel);

  /// Факт 1С за период (`finance.read`); [organization] — организация 1С или все.
  Future<FinanceOverview> finance(DashboardPeriod p, String? organization, CancelToken cancel) => _api.get(
        '/api/finance/overview',
        (j) => FinanceOverview.fromJson(_map(j)),
        query: {...p.financeQuery, 'organization': organization},
        cancel: cancel,
      );

  Future<List<FinanceOrganization>> financeOrganizations(CancelToken cancel) =>
      _api.get('/api/finance/organizations', (j) => _list(j).map(FinanceOrganization.fromJson).toList(), cancel: cancel);

  /// Явка сегодня (Verifix).
  Future<HrOverview> hr(CancelToken cancel) => _api.get('/api/hr/overview', (j) => HrOverview.fromJson(_map(j)), cancel: cancel);

  Future<WorkOverview> work(CancelToken cancel) =>
      _api.get('/api/work/overview', (j) => WorkOverview.fromJson(_map(j)), cancel: cancel);

  /// Последние события ленты.
  Future<NotificationPage> notifications(CancelToken cancel, {int take = 20}) =>
      _api.get('/api/notifications', (j) => NotificationPage.fromJson(_map(j)), query: {'take': take}, cancel: cancel);

  /// Находки AI-консультанта (проблемы, критичные, возможности) — без вызова модели: сервер уже посчитал.
  Future<AiAlerts> alerts(CancelToken cancel) => _api.get('/api/ai/alerts', (j) => AiAlerts.fromJson(_map(j)), cancel: cancel);
}

final dashboardApiProvider = Provider<DashboardApi>((ref) => DashboardApi(ref.watch(apiClientProvider)));
