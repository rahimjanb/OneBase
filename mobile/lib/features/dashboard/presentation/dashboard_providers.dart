import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/auth/auth_controller.dart';
import '../../../core/auth/perm.dart';
import '../../../core/auth/user_profile.dart';
import '../../../core/cache/cached_block.dart';
import '../../../core/core_providers.dart';
import '../../../core/permissions/access.dart';
import '../data/dashboard_api.dart';
import '../data/dashboard_models.dart';
import '../data/home_models.dart';
import '../data/home_repository.dart';
import '../domain/dashboard_period.dart';
import '../domain/home_view.dart';

/// Какие блоки главной видит пользователь — по правам (без права блок не запрашивается вовсе).
class DashboardAccess {
  const DashboardAccess(this.profile);

  final UserProfile? profile;

  List<String> get _perms => Access.screenPermissions(profile);

  /// Продажи компании целиком — уровень организации (РМ, СВ и ТП видят своё в «Аналитике»).
  bool get sales => profile?.salesOrganization ?? false;
  bool get finance => profile?.can(Perm.financeRead) ?? false;
  bool get hr => profile?.hrAccess ?? false;
  bool get tasks => profile?.can(Perm.workTasks) ?? false;
  bool get ai => profile?.can(Perm.agentsRun) ?? false;
  bool get procurement => Access.canOpen(Access.requests, _perms) || Access.canOpen(Access.warehouse, _perms);

  /// Отделы фильтра — только доступные пользователю.
  List<String> get departments => [
        for (final code in homeDepartmentCodes)
          if (Access.departmentOpen(code, _perms, profile?.departments ?? const [])) code,
      ];
}

final dashboardAccessProvider = Provider<DashboardAccess>((ref) => DashboardAccess(ref.watch(currentProfileProvider)));

// ───────────── Фильтры главной: период (для вопроса консультанту и выгрузки) и отделы ─────────────

enum HomePeriod { d7, d30, quarter, year }

class HomeFilters {
  const HomeFilters({this.period = HomePeriod.d30, this.departments = const []});

  final HomePeriod period;
  final List<String> departments;

  bool includes(String code) => departments.isEmpty || departments.contains(code);

  HomeFilters copyWith({HomePeriod? period, List<String>? departments}) =>
      HomeFilters(period: period ?? this.period, departments: departments ?? this.departments);
}

class HomeFiltersController extends Notifier<HomeFilters> {
  @override
  HomeFilters build() => const HomeFilters();

  void setPeriod(HomePeriod p) => state = state.copyWith(period: p);

  void setDepartments(List<String> codes) => state = state.copyWith(departments: codes);
}

final homeFiltersProvider = NotifierProvider<HomeFiltersController, HomeFilters>(HomeFiltersController.new);

// ───────────── Блоки ─────────────

HomeRepository _repo(Ref ref) => ref.read(homeRepositoryProvider);

/// Продажи месяца: сводка и месяц по дням (для «к тем же дням прошлого месяца»).
class HomeSales {
  const HomeSales(this.overview, this.month);

  final SalesOverview overview;
  final SalesDaily? month;

  Map<String, Object?> toJson() => {'overview': overview.toJson(), 'month': month?.toJson()};

  factory HomeSales.fromJson(Map<String, Object?> j) => HomeSales(
        SalesOverview.fromJson(j['overview']! as Map<String, Object?>),
        j['month'] == null ? null : SalesDaily.fromJson(j['month']! as Map<String, Object?>),
      );

  SalesBrief get brief => SalesBrief.of(overview, month);
}

class HomeSalesBlock extends CachedBlock<HomeSales> {
  @override
  String get cacheKey => 'home/sales';

  @override
  Future<HomeSales> fetch(CancelToken cancel) async {
    final repo = _repo(ref);
    final overview = await repo.overview();
    final p = overview.period;
    SalesDaily? month;
    try {
      month = await repo.month(p.year, p.month, closed: p.closed);
    } on Object {
      month = null;
    }
    return HomeSales(overview, month);
  }

  @override
  HomeSales decode(Object? json) => HomeSales.fromJson(json! as Map<String, Object?>);

  @override
  Object? encode(HomeSales data) => data.toJson();
}

final homeSalesBlock = NotifierProvider.autoDispose<HomeSalesBlock, BlockState<HomeSales>>(HomeSalesBlock.new);

/// Явка: сегодня (Verifix) и с начала месяца (отчёт HR).
class HomeHr {
  const HomeHr(this.overview, this.monthRate);

  final HrOverview overview;
  final double? monthRate;

  Map<String, Object?> toJson() => {'overview': overview.toJson(), 'monthRate': monthRate};

  factory HomeHr.fromJson(Map<String, Object?> j) =>
      HomeHr(HrOverview.fromJson(j['overview']! as Map<String, Object?>), (j['monthRate'] as num?)?.toDouble());

  HrBrief get brief => HrBrief.of(overview, monthRate);
}

class HomeHrBlock extends CachedBlock<HomeHr> {
  @override
  String get cacheKey => 'home/hr';

  @override
  Future<HomeHr> fetch(CancelToken cancel) async {
    final repo = _repo(ref);
    final today = isoDate(tashkentNow(ref.read(clockProvider)()));
    final (overview, rate) = await (
      repo.hrOverview(cancel),
      repo.attendance('${today.substring(0, 8)}01', today, past: false).then<double?>((v) => v, onError: (_) => null),
    ).wait;
    return HomeHr(overview, rate);
  }

  @override
  HomeHr decode(Object? json) => HomeHr.fromJson(json! as Map<String, Object?>);

  @override
  Object? encode(HomeHr data) => data.toJson();
}

final homeHrBlock = NotifierProvider.autoDispose<HomeHrBlock, BlockState<HomeHr>>(HomeHrBlock.new);

class HomeTrendsBlock extends CachedBlock<HomeTrends> {
  @override
  String get cacheKey => 'home/trends';

  @override
  Future<HomeTrends> fetch(CancelToken cancel) {
    final access = ref.read(dashboardAccessProvider);
    return _repo(ref).trends(
      sales: access.sales,
      hr: access.hr,
      today: isoDate(tashkentNow(ref.read(clockProvider)())),
    );
  }

  @override
  HomeTrends decode(Object? json) => HomeTrends.fromJson(json! as Map<String, Object?>);

  @override
  Object? encode(HomeTrends data) => data.toJson();
}

final homeTrendsBlock = NotifierProvider.autoDispose<HomeTrendsBlock, BlockState<HomeTrends>>(HomeTrendsBlock.new);

class ProcurementHomeBlock extends CachedBlock<ProcurementHome> {
  @override
  String get cacheKey => 'home/procurement';

  @override
  Future<ProcurementHome> fetch(CancelToken cancel) => _repo(ref).procurement(cancel);

  @override
  ProcurementHome decode(Object? json) => ProcurementHome.fromJson(json! as Map<String, Object?>);

  @override
  Object? encode(ProcurementHome data) => data.toJson();
}

final procurementHomeBlock =
    NotifierProvider.autoDispose<ProcurementHomeBlock, BlockState<ProcurementHome>>(ProcurementHomeBlock.new);

DashboardApi _api(Ref ref) => ref.read(dashboardApiProvider);

/// Финансы 1С текущего месяца по всем организациям — дополнительный блок главной (выручка, расходы, прибыль).
class HomeFinanceBlock extends CachedBlock<FinanceOverview> {
  @override
  String get cacheKey => 'home/finance';

  @override
  Future<FinanceOverview> fetch(CancelToken cancel) {
    final now = tashkentNow(ref.read(clockProvider)());
    return _api(ref).finance(DashboardPeriod(now.year, now.month), null, cancel);
  }

  @override
  FinanceOverview decode(Object? json) => FinanceOverview.fromJson(json! as Map<String, Object?>);

  @override
  Object? encode(FinanceOverview data) => data.toJson();
}

final homeFinanceBlock = NotifierProvider.autoDispose<HomeFinanceBlock, BlockState<FinanceOverview>>(HomeFinanceBlock.new);

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
  Future<NotificationPage> fetch(CancelToken cancel) => _api(ref).notifications(cancel, take: 5);

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

/// Данные для «быстрых» блоков: продажи, HR и задачи — когда каждый ответил (или недоступен). Пока хоть один грузится впервые —
/// null: главная карточка не перескакивает с явки на продажи.
class HomeFast {
  const HomeFast({this.sales, this.hr, this.tasks});

  final SalesBrief? sales;
  final HrBrief? hr;
  final WorkOverview? tasks;
}

final homeFastProvider = Provider.autoDispose<HomeFast?>((ref) {
  final access = ref.watch(dashboardAccessProvider);
  final sales = access.sales ? ref.watch(homeSalesBlock) : null;
  final hr = access.hr ? ref.watch(homeHrBlock) : null;
  final work = access.tasks ? ref.watch(workBlock) : null;
  bool waiting(BlockState<Object?>? s) => s != null && s.data == null && s.failure == null;
  if (waiting(sales) || waiting(hr) || waiting(work)) return null;
  return HomeFast(sales: sales?.data?.brief, hr: hr?.data?.brief, tasks: work?.data);
});

/// Время последнего успешного обновления главной (самый старый из показанных блоков) и была ли ошибка.
final homeStatusProvider = Provider.autoDispose<({DateTime? at, bool failed, bool loading})>((ref) {
  final access = ref.watch(dashboardAccessProvider);
  final states = <BlockState<Object?>>[
    if (access.sales) ref.watch(homeSalesBlock),
    if (access.hr) ref.watch(homeHrBlock),
    if (access.tasks) ref.watch(workBlock),
    if (access.ai) ref.watch(alertsBlock),
  ];
  final times = states.map((s) => s.syncedAt).whereType<DateTime>().toList()..sort();
  return (
    at: times.isEmpty ? null : times.first,
    failed: states.any((s) => s.failure != null && !s.forbidden),
    loading: states.any((s) => s.loading),
  );
});

/// Обновить все блоки (потянуть вниз): текущие данные — заново, закрытые месяцы — из кэша. Жест ждёт самый долгий блок.
Future<void> refreshHome(WidgetRef ref) async {
  await ref.read(homeRepositoryProvider).invalidateLive();
  final access = ref.read(dashboardAccessProvider);
  await Future.wait([
    if (access.sales) ref.read(homeSalesBlock.notifier).refresh(),
    if (access.hr) ref.read(homeHrBlock.notifier).refresh(),
    if (access.sales || access.hr) ref.read(homeTrendsBlock.notifier).refresh(),
    if (access.tasks) ref.read(workBlock.notifier).refresh(),
    if (access.ai) ref.read(alertsBlock.notifier).refresh(),
    if (access.procurement) ref.read(procurementHomeBlock.notifier).refresh(),
    if (access.finance) ref.read(homeFinanceBlock.notifier).refresh(),
    ref.read(eventsBlock.notifier).refresh(),
  ]);
}
