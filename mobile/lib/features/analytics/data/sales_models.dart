import 'package:freezed_annotation/freezed_annotation.dart';

import '../../dashboard/data/dashboard_models.dart';

part 'sales_models.freezed.dart';
part 'sales_models.g.dart';

// Продажи OneBase (`/api/sales/*`) — только поля, которые показывает телефон. Доли — 0..1, суммы — сум, вес — кг. Тяжёлые ответы
// (республика 234 КБ, регион 153 КБ, агенты 124 КБ, ТП ≈ 100 КБ) разбираются в фоновом изоляте функциями `...Of` ниже: в модель идёт
// только нужное экрану (календарь визитов, артикулы и «молчащие» точки по агентам отбрасываются).

@freezed
abstract class AgentFlag with _$AgentFlag {
  const factory AgentFlag({
    required String kind,

    /// Critical | Risk | Info
    required String severity,
    required String label,
    required String title,
    @Default('') String explanation,
  }) = _AgentFlag;

  factory AgentFlag.fromJson(Map<String, Object?> json) => _$AgentFlagFromJson(json);
}

/// Направление или регион: план, факт, выполнение, выручка, АКБ, страйк, замечания.
@freezed
abstract class UnitRow with _$UnitRow {
  const factory UnitRow({
    required String id,
    required String name,
    String? subtitle,
    double? planKg,
    @Default(0) double factKg,
    double? execution,
    double? forecastKg,
    double? forecastExecution,
    @Default(0) double revenue,
    @Default(0) int akb,
    double? strike,
    @Default(0) int visitsWithoutOrder,
    @Default(0) int agents,
    @Default(0) int regionCount,
    @Default(FlagCounts()) FlagCounts flags,

    /// republic | direction | region
    @Default('region') String kind,
    String? executionLevel,
    String? forecastExecutionLevel,
    @Default(false) bool isChannel,
  }) = _UnitRow;

  factory UnitRow.fromJson(Map<String, Object?> json) => _$UnitRowFromJson(json);
}

/// «Те же дни прошлого месяца»: кг, сумма, АКБ сейчас и тогда.
@freezed
abstract class SameDaysRow with _$SameDaysRow {
  const factory SameDaysRow({
    required String id,
    required String name,
    @Default(0) double kgBefore,
    @Default(0) double kgNow,
    double? kgDelta,
    @Default(0) double sumBefore,
    @Default(0) double sumNow,
    double? sumDelta,
    @Default(0) int akbNow,
    double? akbDelta,
  }) = _SameDaysRow;

  factory SameDaysRow.fromJson(Map<String, Object?> json) => _$SameDaysRowFromJson(json);
}

/// Карточка категории (без артикулов).
@freezed
abstract class CategoryCard with _$CategoryCard {
  const factory CategoryCard({
    required String id,
    required String name,
    @Default(0) int skuSold,
    @Default(0) int skuTotal,
    @Default(0) double factKg,
    double? weightShare,
    @Default(0) double revenue,
    @Default(0) int akb,
    double? distribution,
    double? forecastKg,
    double? vsPrevMonth,
    @Default(0) int silent,
    @Default(0) int lost,
  }) = _CategoryCard;

  factory CategoryCard.fromJson(Map<String, Object?> json) => _$CategoryCardFromJson(json);
}

/// Республика, направление или «Мои регионы» (GroupView).
@freezed
abstract class GroupView with _$GroupView {
  const factory GroupView({
    required SalesPeriod period,
    required String name,
    String? subtitle,
    required SalesKpi kpi,
    @Default(<UnitRow>[]) List<UnitRow> cards,
    @Default(<UnitRow>[]) List<UnitRow> regions,
    @Default(<SameDaysRow>[]) List<SameDaysRow> sameDays,
    @Default(<CategoryCard>[]) List<CategoryCard> categoryCards,

    /// Кг по дням месяца (календарь всего уровня).
    @Default(<double?>[]) List<double?> days,
  }) = _GroupView;

  factory GroupView.fromJson(Map<String, Object?> json) => _$GroupViewFromJson(json);
}

@freezed
abstract class TeamRow with _$TeamRow {
  const factory TeamRow({
    required int agentId,
    required String name,
    double? planKg,
    @Default(0) double factKg,
    double? execution,
    double? forecastKg,
    double? forecastExecution,
    @Default(0) double revenue,
    @Default(0) int visits,
    @Default(0) int orders,
    double? strike,
    @Default(false) bool isVacancy,
    @Default(<AgentFlag>[]) List<AgentFlag> flags,
    String? executionLevel,
    @Default(0) int akb,
  }) = _TeamRow;

  factory TeamRow.fromJson(Map<String, Object?> json) => _$TeamRowFromJson(json);
}

@freezed
abstract class PlanFactMonth with _$PlanFactMonth {
  const factory PlanFactMonth({required int month, double? planKg, double? factKg}) = _PlanFactMonth;

  factory PlanFactMonth.fromJson(Map<String, Object?> json) => _$PlanFactMonthFromJson(json);
}

@freezed
abstract class RevenueShare with _$RevenueShare {
  const factory RevenueShare({required String name, @Default(0) double revenue, double? share}) = _RevenueShare;

  factory RevenueShare.fromJson(Map<String, Object?> json) => _$RevenueShareFromJson(json);
}

/// Регион (RegionView): показатели, план и факт по месяцам, состав выручки, команда ТП.
@freezed
abstract class RegionView with _$RegionView {
  const factory RegionView({
    required SalesPeriod period,
    required String id,
    required String name,
    String? directionId,
    String? directionName,
    String? supervisor,
    String? dealer,
    required SalesKpi kpi,
    @Default(<PlanFactMonth>[]) List<PlanFactMonth> months,
    @Default(<RevenueShare>[]) List<RevenueShare> categories,
    @Default(<TeamRow>[]) List<TeamRow> team,
    @Default(<SameDaysRow>[]) List<SameDaysRow> sameDays,
    @Default(<CategoryCard>[]) List<CategoryCard> categoryCards,
    @Default(<double?>[]) List<double?> days,
  }) = _RegionView;

  factory RegionView.fromJson(Map<String, Object?> json) => _$RegionViewFromJson(json);
}

@freezed
abstract class UnitRef with _$UnitRef {
  const factory UnitRef({required String id, required String name}) = _UnitRef;

  factory UnitRef.fromJson(Map<String, Object?> json) => _$UnitRefFromJson(json);
}

/// Строка списка ТП.
@freezed
abstract class AgentCardRow with _$AgentCardRow {
  const factory AgentCardRow({
    required int agentId,
    required String name,
    String? regionId,
    @Default('') String regionName,
    String? directionName,
    String? supervisor,
    @Default(false) bool isVacancy,
    @Default(true) bool isSalesRep,
    double? planKg,
    @Default(0) double factKg,
    double? execution,
    double? forecastKg,
    double? forecastExecution,
    @Default(0) double revenue,
    @Default(0) int akb,
    @Default(0) int visits,
    @Default(0) int visitsWithOrder,
    @Default(0) int orders,
    double? conversion,
    double? sumPerVisit,
    @Default(0) int categories,
    double? tempo,
    @Default(<AgentFlag>[]) List<AgentFlag> flags,
    String? executionLevel,
  }) = _AgentCardRow;

  factory AgentCardRow.fromJson(Map<String, Object?> json) => _$AgentCardRowFromJson(json);
}

@freezed
abstract class AgentTargets with _$AgentTargets {
  const factory AgentTargets({
    @Default(0.7) double conversion,
    @Default(0) double revenuePerOutlet,
    @Default(0) double akbPerAgent,
    @Default(0) double categoriesPerOutlet,
  }) = _AgentTargets;

  factory AgentTargets.fromJson(Map<String, Object?> json) => _$AgentTargetsFromJson(json);
}

@freezed
abstract class AgentsView with _$AgentsView {
  const factory AgentsView({
    required SalesPeriod period,
    @Default(AgentTargets()) AgentTargets targets,
    @Default(<AgentCardRow>[]) List<AgentCardRow> agents,
    @Default(<UnitRef>[]) List<UnitRef> regions,
    @Default(<UnitRef>[]) List<UnitRef> directions,
  }) = _AgentsView;

  factory AgentsView.fromJson(Map<String, Object?> json) => _$AgentsViewFromJson(json);
}

@freezed
abstract class MedianValue with _$MedianValue {
  const factory MedianValue({double? value, double? regionMedian}) = _MedianValue;

  factory MedianValue.fromJson(Map<String, Object?> json) => _$MedianValueFromJson(json);
}

@freezed
abstract class MarketBrief with _$MarketBrief {
  const factory MarketBrief({
    required int marketId,
    required String name,
    @Default(0) double kg,
    @Default(0) double revenue,

    /// Для «ещё не купили»: прошлый месяц.
    @Default(0) double prevKg,
    @Default(0) double prevRevenue,
    @Default(0) int categories,
    @Default(0) int positions,
    double? share,
  }) = _MarketBrief;

  factory MarketBrief.fromJson(Map<String, Object?> json) => _$MarketBriefFromJson(json);
}

@freezed
abstract class CategoryPlanFact with _$CategoryPlanFact {
  const factory CategoryPlanFact({
    int? categoryId,
    required String name,
    double? planKg,
    @Default(0) double factKg,
    double? execution,
    double? forecastExecution,
    String? executionLevel,
  }) = _CategoryPlanFact;

  factory CategoryPlanFact.fromJson(Map<String, Object?> json) => _$CategoryPlanFactFromJson(json);
}

/// Карточка ТП (AgentView) без ассортимента: точки — отдельным списком.
@freezed
abstract class AgentView with _$AgentView {
  const factory AgentView({
    required SalesPeriod period,
    required int agentId,
    required String name,
    String? regionId,
    String? regionName,
    String? directionName,
    @Default(false) bool isVacancy,
    double? planKg,
    double? execution,
    @Default(0) double factKg,
    @Default(0) double revenue,
    @Default(0) int visits,
    @Default(0) int visitsWithOrder,
    @Default(MedianValue()) MedianValue conversion,
    @Default(MedianValue()) MedianValue sumPerVisit,
    @Default(MedianValue()) MedianValue avgCheck,
    @Default(0) int categories,
    int? planCategories,
    @Default(4) double categoryTarget,
    double? tempo,
    @Default(<AgentFlag>[]) List<AgentFlag> flags,
    @Default(<CategoryPlanFact>[]) List<CategoryPlanFact> categoryPlan,
    SameDaysRow? sameDays,
    @Default(0) int silentBase,
    @Default(<MarketBrief>[]) List<MarketBrief> silent,
    @Default(<MarketBrief>[]) List<MarketBrief> newMarkets,
    @Default(<MarketBrief>[]) List<MarketBrief> stores,
    @Default(0) int akb,
    @Default(0) int orders,
    double? revenuePerOutlet,
    double? kgPerOutlet,
    String? executionLevel,
  }) = _AgentView;

  factory AgentView.fromJson(Map<String, Object?> json) => _$AgentViewFromJson(json);
}

@freezed
abstract class StoreCategory with _$StoreCategory {
  const factory StoreCategory({required String name, @Default(0) double kg, @Default(0) double revenue, double? share}) = _StoreCategory;

  factory StoreCategory.fromJson(Map<String, Object?> json) => _$StoreCategoryFromJson(json);
}

@freezed
abstract class StoreProduct with _$StoreProduct {
  const factory StoreProduct({
    required int productId,
    required String name,
    String? category,
    @Default(0) double kg,
    @Default(0) double revenue,
    @Default(false) bool isTop,
  }) = _StoreProduct;

  factory StoreProduct.fromJson(Map<String, Object?> json) => _$StoreProductFromJson(json);
}

/// Точка за месяц (`/api/sales/stores/{id}`).
@freezed
abstract class StoreView with _$StoreView {
  const factory StoreView({
    required SalesPeriod period,
    required int marketId,
    required String name,
    String? regionName,
    @Default(0) double factKg,
    @Default(0) double revenue,
    @Default(0) int orders,
    @Default(0) int categories,
    @Default(0) int positions,
    @Default(0) double prevMonthKg,
    @Default(0) double prevMonthRevenue,
    @Default(<StoreCategory>[]) List<StoreCategory> categoryRows,
    @Default(<StoreProduct>[]) List<StoreProduct> products,
    @Default(<String>[]) List<String> agents,
  }) = _StoreView;

  factory StoreView.fromJson(Map<String, Object?> json) => _$StoreViewFromJson(json);
}

// ───────────── Срезы тяжёлых ответов (функции верхнего уровня — выполняются в фоновом изоляте) ─────────────

List<double?> _days(Object? calendar) {
  final c = calendar as Map<String, Object?>?;
  return [for (final d in c?['totalDays'] as List<Object?>? ?? const []) (d as num?)?.toDouble()];
}

List<Map<String, Object?>> _list(Object? v) => (v as List<Object?>? ?? const []).cast<Map<String, Object?>>();

Map<String, Object?> _category(Map<String, Object?> c) => {...c}..remove('skus');

GroupView groupViewOf(Object? json) {
  final j = json! as Map<String, Object?>;
  return GroupView.fromJson({
    'period': j['period'],
    'name': j['name'],
    'subtitle': j['subtitle'],
    'kpi': j['kpi'],
    'cards': j['cards'],
    'regions': j['regions'],
    'sameDays': j['sameDays'],
    'categoryCards': [for (final c in _list(j['categoryCards'])) _category(c)],
    'days': _days(j['calendar']),
  });
}

RegionView regionViewOf(Object? json) {
  final j = json! as Map<String, Object?>;
  return RegionView.fromJson({
    'period': j['period'],
    'id': j['id'],
    'name': j['name'],
    'directionId': j['directionId'],
    'directionName': j['directionName'],
    'supervisor': j['supervisor'],
    'dealer': j['dealer'],
    'kpi': j['kpi'],
    'months': j['months'],
    'categories': j['categories'],
    'team': j['team'],
    'sameDays': j['sameDays'],
    'categoryCards': [for (final c in _list(j['categoryCards'])) _category(c)],
    'days': _days(j['calendar']),
  });
}

AgentsView agentsViewOf(Object? json) => AgentsView.fromJson(json! as Map<String, Object?>);

AgentView agentViewOf(Object? json) {
  final j = {...json! as Map<String, Object?>};
  final assortment = j.remove('assortment') as Map<String, Object?>?;
  j['stores'] = assortment?['stores'] ?? const <Object?>[];
  j.remove('akbMonths');
  j.remove('indicators');
  return AgentView.fromJson(j);
}
