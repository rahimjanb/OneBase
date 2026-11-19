import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/api/api_client.dart';
import '../../../core/api/api_providers.dart';
import '../../../core/cache/repo_cache.dart';
import '../domain/home_view.dart';
import 'dashboard_models.dart';
import 'home_models.dart';

/// Данные главной с кэшем на уровне репозитория:
/// - текущий месяц продаж и сегодняшние отчёты HR живут минуту (блоки главной просят их одновременно — запрос один);
/// - закрытые месяцы продаж (тяжёлые, ≈ 200 КБ) и прошлые недели HR — неделю и сутки: при следующем открытии не качаются.
class HomeRepository {
  HomeRepository(this._api, this._cache);

  final ApiClient _api;
  final RepoCache _cache;

  static Map<String, Object?> _map(Object? j) => j! as Map<String, Object?>;

  Future<SalesOverview> overview({bool force = false}) => _cache.get(
        'home/live/sales-overview',
        maxAge: const Duration(minutes: 1),
        force: force,
        fetch: () => _api.get('/api/sales/overview', (j) => SalesOverview.fromJson(_map(j))),
        encode: (v) => v.toJson(),
        decode: (j) => SalesOverview.fromJson(_map(j)),
      );

  /// Месяц продаж по дням: [sum] — выручка (`metric=sum`), иначе кг с планом и фактом.
  Future<SalesDaily> month(int year, int month, {required bool closed, bool sum = false, bool force = false}) => _cache.get(
        'home/${closed ? '' : 'live/'}sales-${sum ? 'sum' : 'kg'}/$year-$month',
        maxAge: closed ? const Duration(days: 7) : const Duration(minutes: 1),
        force: force,
        fetch: () => _api.getExtract(
          '/api/sales/republic',
          salesDailyOf,
          query: {'year': year, 'month': month, if (sum) 'metric': 'sum'},
        ),
        encode: (v) => v.toJson(),
        decode: (j) => SalesDaily.fromJson(_map(j)),
      );

  /// Явка за период (%). Прошедшие недели — сутки, текущая — минута.
  Future<double?> attendance(String from, String to, {required bool past}) => _cache.get(
        'home/${past ? '' : 'live/'}hr-rate/$from/$to',
        maxAge: past ? const Duration(days: 1) : const Duration(minutes: 1),
        fetch: () => _api.getExtract('/api/hr/reports', attendanceRateOf, query: {'from': from, 'to': to}),
        encode: (v) => v,
        decode: (j) => (j as num?)?.toDouble(),
      );

  /// Обновить текущие данные (потянуть вниз): закрытые месяцы и прошлые недели остаются в кэше.
  Future<void> invalidateLive() => _cache.invalidate('home/live/');

  Future<HrOverview> hrOverview(CancelToken cancel) =>
      _api.get('/api/hr/overview', (j) => HrOverview.fromJson(_map(j)), cancel: cancel);

  Future<ProcurementHome> procurement(CancelToken cancel) =>
      _api.get('/api/procurement/home', (j) => ProcurementHome.fromJson(_map(j)), cancel: cancel);

  /// 12 недель «Динамики»: продажи к плану и выручка — из дневных итогов месяцев, явка — из отчётов HR по неделям.
  /// [sales] / [hr] — есть ли доступ к разделу.
  Future<HomeTrends> trends({required bool sales, required bool hr, required String today}) async {
    SalesOverview? overview;
    if (sales) {
      try {
        overview = await this.overview();
      } on Object {
        overview = null;
      }
    }
    final end = overview?.period.today ?? today;
    final weeks = homeWeeks(end);

    Future<({List<double?> execution, List<double?> revenue})?> salesPart() async {
      final o = overview;
      if (o == null) return null;
      final months = monthsFor(weeks, o.period.year, o.period.month);
      final loaded = await Future.wait([
        for (final (y, m) in months)
          () async {
            final current = y == o.period.year && m == o.period.month;
            final closed = !current || o.period.closed;
            try {
              final results = await Future.wait([
                month(y, m, closed: closed),
                month(y, m, closed: closed, sum: true).then<SalesDaily?>((v) => v, onError: (_) => null),
              ]);
              return MapEntry('$y-$m', MonthDays(kg: results[0]!, sum: results[1]));
            } on Object {
              return null;
            }
          }(),
      ]);
      return salesWeeks(weeks, Map.fromEntries(loaded.whereType<MapEntry<String, MonthDays>>()));
    }

    Future<List<double?>?> hrPart() async {
      if (!hr) return null;
      final rates = await Future.wait([
        for (final w in weeks)
          attendance(w.start, w.end.compareTo(end) < 0 ? w.end : end, past: w.end.compareTo(end) < 0)
              .then<double?>((v) => v, onError: (_) => null),
      ]);
      return rates.every((r) => r == null) ? null : rates;
    }

    final (s, a) = await (salesPart(), hrPart()).wait;
    return HomeTrends(weeks: weeks, currentDays: currentDaysOf(end), sales: s?.execution, revenue: s?.revenue, attendance: a);
  }
}

final homeRepositoryProvider = Provider<HomeRepository>(
  (ref) => HomeRepository(ref.watch(apiClientProvider), ref.watch(repoCacheProvider)),
);
