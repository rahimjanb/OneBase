import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/auth/auth_controller.dart';
import '../../../core/auth/perm.dart';
import '../../../core/auth/user_profile.dart';
import '../../../core/cache/cached_block.dart';
import '../../../core/permissions/access.dart';
import '../../dashboard/data/dashboard_models.dart';
import '../data/analytics_api.dart';
import '../data/analytik_models.dart';
import '../data/sales_models.dart';

/// Что видно в «Аналитике» — по правам и охвату продаж (сервер всё равно проверяет: страницы организации дают 403 РМ/СВ/ТП).
class AnalyticsAccess {
  const AnalyticsAccess(this.profile);

  final UserProfile? profile;

  bool get sales => profile?.can(Perm.salesRead) ?? false;

  /// Республика и обзор — только охват «вся организация».
  bool get salesOrg => profile?.salesOrganization ?? false;

  /// Домашняя страница продаж пользователя (путь веба): `/sales/my`, `/sales/directions/…`, `/sales/regions/…`, `/sales/agents/…`.
  String? get salesHome => profile?.salesScope?.home;
  String get salesLevel => profile?.salesScope?.level ?? 'Organization';

  bool get finance => profile?.can(Perm.financeRead) ?? false;
  bool get analytik => profile?.can(Perm.analytikUse) ?? false;
  bool get production => Access.canOpen(Access.production, Access.screenPermissions(profile));
  bool get hr => profile?.hrAccess ?? false;

  /// Модули сравнения периодов, открытые пользователю.
  List<String> get compareModules => [
        if (analytik && sales) 'sales',
        if (analytik && finance) 'finance',
        if (analytik && production) 'production',
        if (analytik && (hr || (profile?.can('analytik.manage') ?? false))) 'hr',
      ];
}

final analyticsAccessProvider = Provider<AnalyticsAccess>((ref) => AnalyticsAccess(ref.watch(currentProfileProvider)));

/// Выбранный месяц продаж (null — последний с данными, как у сайта).
class SalesMonthController extends Notifier<SalesMonth?> {
  @override
  SalesMonth? build() => null;

  void set(SalesMonth? m) => state = m;
}

final salesMonthProvider = NotifierProvider<SalesMonthController, SalesMonth?>(SalesMonthController.new);

AnalyticsApi _api(Ref ref) => ref.read(analyticsApiProvider);
String _m(SalesMonth? m) => m == null ? 'latest' : '${m.year}-${m.month}';

class SalesMonthsBlock extends CachedBlock<List<SalesMonth>> {
  @override
  String get cacheKey => 'analytics/months';

  @override
  Future<List<SalesMonth>> fetch(CancelToken cancel) => _api(ref).months(cancel);

  @override
  List<SalesMonth> decode(Object? json) => [for (final e in json! as List<Object?>) SalesMonth.fromJson(e! as Map<String, Object?>)];

  @override
  Object? encode(List<SalesMonth> data) => [for (final m in data) m.toJson()];
}

final salesMonthsBlock = NotifierProvider.autoDispose<SalesMonthsBlock, BlockState<List<SalesMonth>>>(SalesMonthsBlock.new);

class OverviewBlock extends CachedBlock<SalesOverview> {
  OverviewBlock(this.month);

  final SalesMonth? month;

  @override
  String get cacheKey => 'analytics/overview/${_m(month)}';

  @override
  Future<SalesOverview> fetch(CancelToken cancel) => _api(ref).overview(month, cancel);

  @override
  SalesOverview decode(Object? json) => SalesOverview.fromJson(json! as Map<String, Object?>);

  @override
  Object? encode(SalesOverview data) => data.toJson();
}

final overviewBlock = NotifierProvider.autoDispose.family<OverviewBlock, BlockState<SalesOverview>, SalesMonth?>(OverviewBlock.new);

typedef GroupArgs = ({String path, SalesMonth? month});

class GroupBlock extends CachedBlock<GroupView> {
  GroupBlock(this.args);

  final GroupArgs args;

  @override
  String get cacheKey => 'analytics/group/${args.path}/${_m(args.month)}';

  @override
  Future<GroupView> fetch(CancelToken cancel) => _api(ref).group(args.path, args.month, cancel);

  @override
  GroupView decode(Object? json) => GroupView.fromJson(json! as Map<String, Object?>);

  @override
  Object? encode(GroupView data) => data.toJson();
}

final groupBlock = NotifierProvider.autoDispose.family<GroupBlock, BlockState<GroupView>, GroupArgs>(GroupBlock.new);

typedef IdArgs = ({String id, SalesMonth? month});

class RegionBlock extends CachedBlock<RegionView> {
  RegionBlock(this.args);

  final IdArgs args;

  @override
  String get cacheKey => 'analytics/region/${args.id}/${_m(args.month)}';

  @override
  Future<RegionView> fetch(CancelToken cancel) => _api(ref).region(args.id, args.month, cancel);

  @override
  RegionView decode(Object? json) => RegionView.fromJson(json! as Map<String, Object?>);

  @override
  Object? encode(RegionView data) => data.toJson();
}

final regionBlock = NotifierProvider.autoDispose.family<RegionBlock, BlockState<RegionView>, IdArgs>(RegionBlock.new);

class AgentsBlock extends CachedBlock<AgentsView> {
  AgentsBlock(this.month);

  final SalesMonth? month;

  @override
  String get cacheKey => 'analytics/agents/${_m(month)}';

  @override
  Future<AgentsView> fetch(CancelToken cancel) => _api(ref).agents(month, cancel);

  @override
  AgentsView decode(Object? json) => AgentsView.fromJson(json! as Map<String, Object?>);

  @override
  Object? encode(AgentsView data) => data.toJson();
}

final agentsBlock = NotifierProvider.autoDispose.family<AgentsBlock, BlockState<AgentsView>, SalesMonth?>(AgentsBlock.new);

class AgentBlock extends CachedBlock<AgentView> {
  AgentBlock(this.args);

  final IdArgs args;

  @override
  String get cacheKey => 'analytics/agent/${args.id}/${_m(args.month)}';

  @override
  Future<AgentView> fetch(CancelToken cancel) => _api(ref).agent(int.parse(args.id), args.month, cancel);

  @override
  AgentView decode(Object? json) => AgentView.fromJson(json! as Map<String, Object?>);

  @override
  Object? encode(AgentView data) => data.toJson();
}

final agentBlock = NotifierProvider.autoDispose.family<AgentBlock, BlockState<AgentView>, IdArgs>(AgentBlock.new);

class StoreBlock extends CachedBlock<StoreView> {
  StoreBlock(this.args);

  final IdArgs args;

  @override
  String get cacheKey => 'analytics/store/${args.id}/${_m(args.month)}';

  @override
  Future<StoreView> fetch(CancelToken cancel) => _api(ref).store(int.parse(args.id), args.month, cancel);

  @override
  StoreView decode(Object? json) => StoreView.fromJson(json! as Map<String, Object?>);

  @override
  Object? encode(StoreView data) => data.toJson();
}

final storeBlock = NotifierProvider.autoDispose.family<StoreBlock, BlockState<StoreView>, IdArgs>(StoreBlock.new);

typedef CompareArgs = ({String module, String period});

class CompareBlock extends CachedBlock<AnalytikBlock> {
  CompareBlock(this.args);

  final CompareArgs args;

  @override
  String get cacheKey => 'analytics/compare/${args.module}/${args.period}';

  @override
  Future<AnalytikBlock> fetch(CancelToken cancel) => _api(ref).compare(args.module, args.period, cancel);

  @override
  AnalytikBlock decode(Object? json) => AnalytikBlock.fromJson(json! as Map<String, Object?>);

  @override
  Object? encode(AnalytikBlock data) => data.toJson();
}

final compareBlock = NotifierProvider.autoDispose.family<CompareBlock, BlockState<AnalytikBlock>, CompareArgs>(CompareBlock.new);

class AnalytikNavBlock extends CachedBlock<AnalytikNav> {
  @override
  String get cacheKey => 'analytics/analytik-nav';

  @override
  Future<AnalytikNav> fetch(CancelToken cancel) => _api(ref).analytikNav(cancel);

  @override
  AnalytikNav decode(Object? json) => AnalytikNav.fromJson(json! as Map<String, Object?>);

  @override
  Object? encode(AnalytikNav data) => data.toJson();
}

final analytikNavBlock = NotifierProvider.autoDispose<AnalytikNavBlock, BlockState<AnalytikNav>>(AnalytikNavBlock.new);

class TechnologistBlock extends CachedBlock<TechnologistBrief> {
  @override
  String get cacheKey => 'analytics/technologist';

  @override
  Future<TechnologistBrief> fetch(CancelToken cancel) => _api(ref).technologist(cancel);

  @override
  TechnologistBrief decode(Object? json) => TechnologistBrief.fromJson(json! as Map<String, Object?>);

  @override
  Object? encode(TechnologistBrief data) => data.toJson();
}

final technologistBlock = NotifierProvider.autoDispose<TechnologistBlock, BlockState<TechnologistBrief>>(TechnologistBlock.new);

/// Экран приложения для домашней страницы продаж пользователя (путь веба из `salesScope.home`).
String salesHomeRoute(String? home) {
  if (home == null) return '/analytics/sales';
  final s = Uri.parse(home).pathSegments;
  if (s.length >= 3 && s[1] == 'regions') return '/analytics/sales/regions/${s[2]}';
  if (s.length >= 3 && s[1] == 'directions') return '/analytics/sales/directions/${s[2]}';
  if (s.length >= 3 && s[1] == 'agents') return '/analytics/sales/agents/${s[2]}';
  if (s.length >= 2 && s[1] == 'my') return '/analytics/sales/my';
  return '/analytics/sales';
}
