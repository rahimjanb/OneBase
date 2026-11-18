import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/auth/auth_controller.dart';
import '../../../core/auth/perm.dart';
import '../../../core/auth/user_profile.dart';
import '../../../core/cache/cached_block.dart';
import '../data/dashboard_api.dart';
import '../data/dashboard_models.dart';
import '../domain/dashboard_period.dart';

/// Фильтры Dashboard: период и организация 1С.
class DashboardFilterController extends Notifier<DashboardFilter> {
  @override
  DashboardFilter build() => const DashboardFilter();

  void setPeriod(DashboardPeriod period) => state = state.copyWith(period: period);

  void setOrganization(String? id) => state = state.copyWith(organizationId: () => id);
}

final dashboardFilterProvider = NotifierProvider<DashboardFilterController, DashboardFilter>(DashboardFilterController.new);

/// Какие блоки видит пользователь — по правам (без права блок не запрашивается вовсе).
class DashboardAccess {
  const DashboardAccess(this.profile);

  final UserProfile? profile;

  /// Продажи компании целиком — уровень организации (РМ, СВ и ТП видят своё в «Аналитике», этап 5).
  bool get sales => profile?.salesOrganization ?? false;
  bool get finance => profile?.can(Perm.financeRead) ?? false;
  bool get hr => profile?.hrAccess ?? false;
  bool get tasks => profile?.can(Perm.workTasks) ?? false;
  bool get ai => profile?.can(Perm.agentsRun) ?? false;
}

final dashboardAccessProvider = Provider<DashboardAccess>((ref) => DashboardAccess(ref.watch(currentProfileProvider)));

DashboardApi _api(Ref ref) => ref.read(dashboardApiProvider);

class SalesOverviewBlock extends CachedBlock<SalesOverview> {
  SalesOverviewBlock(this.period);

  final DashboardPeriod period;

  @override
  String get cacheKey => 'dashboard/sales/${period.key}';

  @override
  Future<SalesOverview> fetch(CancelToken cancel) => _api(ref).salesOverview(period, cancel);

  @override
  SalesOverview decode(Object? json) => SalesOverview.fromJson(json! as Map<String, Object?>);

  @override
  Object? encode(SalesOverview data) => data.toJson();
}

final salesOverviewBlock = NotifierProvider.autoDispose
    .family<SalesOverviewBlock, BlockState<SalesOverview>, DashboardPeriod>(SalesOverviewBlock.new);

class SalesDailyBlock extends CachedBlock<SalesDaily> {
  SalesDailyBlock(this.period);

  final DashboardPeriod period;

  @override
  String get cacheKey => 'dashboard/sales-daily/${period.key}';

  @override
  Future<SalesDaily> fetch(CancelToken cancel) => _api(ref).salesDaily(period, cancel);

  @override
  SalesDaily decode(Object? json) => SalesDaily.fromJson(json! as Map<String, Object?>);

  @override
  Object? encode(SalesDaily data) => data.toJson();
}

final salesDailyBlock =
    NotifierProvider.autoDispose.family<SalesDailyBlock, BlockState<SalesDaily>, DashboardPeriod>(SalesDailyBlock.new);

class SalesMonthsBlock extends CachedBlock<List<SalesMonth>> {
  @override
  String get cacheKey => 'dashboard/sales-months';

  @override
  Future<List<SalesMonth>> fetch(CancelToken cancel) => _api(ref).salesMonths(cancel);

  @override
  List<SalesMonth> decode(Object? json) =>
      (json! as List<Object?>).map((e) => SalesMonth.fromJson(e! as Map<String, Object?>)).toList();

  @override
  Object? encode(List<SalesMonth> data) => data.map((m) => m.toJson()).toList();
}

final salesMonthsBlock = NotifierProvider.autoDispose<SalesMonthsBlock, BlockState<List<SalesMonth>>>(SalesMonthsBlock.new);

typedef FinanceArgs = ({DashboardPeriod period, String? organization});

class FinanceBlock extends CachedBlock<FinanceOverview> {
  FinanceBlock(this.args);

  final FinanceArgs args;

  @override
  String get cacheKey => 'dashboard/finance/${args.period.key}/${args.organization ?? 'all'}';

  @override
  Future<FinanceOverview> fetch(CancelToken cancel) => _api(ref).finance(args.period, args.organization, cancel);

  @override
  FinanceOverview decode(Object? json) => FinanceOverview.fromJson(json! as Map<String, Object?>);

  @override
  Object? encode(FinanceOverview data) => data.toJson();
}

final financeBlock =
    NotifierProvider.autoDispose.family<FinanceBlock, BlockState<FinanceOverview>, FinanceArgs>(FinanceBlock.new);

class FinanceOrganizationsBlock extends CachedBlock<List<FinanceOrganization>> {
  @override
  String get cacheKey => 'dashboard/finance-organizations';

  @override
  Future<List<FinanceOrganization>> fetch(CancelToken cancel) => _api(ref).financeOrganizations(cancel);

  @override
  List<FinanceOrganization> decode(Object? json) =>
      (json! as List<Object?>).map((e) => FinanceOrganization.fromJson(e! as Map<String, Object?>)).toList();

  @override
  Object? encode(List<FinanceOrganization> data) => data.map((o) => o.toJson()).toList();
}

final financeOrganizationsBlock = NotifierProvider.autoDispose<FinanceOrganizationsBlock, BlockState<List<FinanceOrganization>>>(
  FinanceOrganizationsBlock.new,
);

class HrBlock extends CachedBlock<HrOverview> {
  @override
  String get cacheKey => 'dashboard/hr';

  @override
  Future<HrOverview> fetch(CancelToken cancel) => _api(ref).hr(cancel);

  @override
  HrOverview decode(Object? json) => HrOverview.fromJson(json! as Map<String, Object?>);

  @override
  Object? encode(HrOverview data) => data.toJson();
}

final hrBlock = NotifierProvider.autoDispose<HrBlock, BlockState<HrOverview>>(HrBlock.new);

class WorkBlock extends CachedBlock<WorkOverview> {
  @override
  String get cacheKey => 'dashboard/work';

  @override
  Future<WorkOverview> fetch(CancelToken cancel) => _api(ref).work(cancel);

  @override
  WorkOverview decode(Object? json) => WorkOverview.fromJson(json! as Map<String, Object?>);

  @override
  Object? encode(WorkOverview data) => data.toJson();
}

final workBlock = NotifierProvider.autoDispose<WorkBlock, BlockState<WorkOverview>>(WorkBlock.new);

class EventsBlock extends CachedBlock<NotificationPage> {
  @override
  String get cacheKey => 'dashboard/events';

  /// В текстах событий — имена сотрудников и кандидатов: на телефоне не сохраняются.
  @override
  bool get persist => false;

  @override
  Future<NotificationPage> fetch(CancelToken cancel) => _api(ref).notifications(cancel);

  @override
  NotificationPage decode(Object? json) => NotificationPage.fromJson(json! as Map<String, Object?>);

  @override
  Object? encode(NotificationPage data) => data.toJson();
}

final eventsBlock = NotifierProvider.autoDispose<EventsBlock, BlockState<NotificationPage>>(EventsBlock.new);

class AlertsBlock extends CachedBlock<AiAlerts> {
  @override
  String get cacheKey => 'dashboard/ai-alerts';

  @override
  Future<AiAlerts> fetch(CancelToken cancel) => _api(ref).alerts(cancel);

  @override
  AiAlerts decode(Object? json) => AiAlerts.fromJson(json! as Map<String, Object?>);

  @override
  Object? encode(AiAlerts data) => data.toJson();
}

final alertsBlock = NotifierProvider.autoDispose<AlertsBlock, BlockState<AiAlerts>>(AlertsBlock.new);

/// Обновить все видимые блоки (pull-to-refresh): блоки обновляются параллельно, жест ждёт самый долгий.
Future<void> refreshDashboard(WidgetRef ref) {
  final access = ref.read(dashboardAccessProvider);
  final filter = ref.read(dashboardFilterProvider);
  return Future.wait([
    if (access.sales) ...[
      ref.read(salesOverviewBlock(filter.period).notifier).refresh(),
      ref.read(salesDailyBlock(filter.period).notifier).refresh(),
    ],
    if (access.finance) ref.read(financeBlock((period: filter.period, organization: filter.organizationId)).notifier).refresh(),
    if (access.hr) ref.read(hrBlock.notifier).refresh(),
    if (access.tasks) ref.read(workBlock.notifier).refresh(),
    if (access.ai) ref.read(alertsBlock.notifier).refresh(),
    ref.read(eventsBlock.notifier).refresh(),
  ]);
}
