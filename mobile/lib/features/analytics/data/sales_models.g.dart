// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'sales_models.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

_AgentFlag _$AgentFlagFromJson(Map<String, dynamic> json) => _AgentFlag(
  kind: json['kind'] as String,
  severity: json['severity'] as String,
  label: json['label'] as String,
  title: json['title'] as String,
  explanation: json['explanation'] as String? ?? '',
);

Map<String, dynamic> _$AgentFlagToJson(_AgentFlag instance) =>
    <String, dynamic>{
      'kind': instance.kind,
      'severity': instance.severity,
      'label': instance.label,
      'title': instance.title,
      'explanation': instance.explanation,
    };

_UnitRow _$UnitRowFromJson(Map<String, dynamic> json) => _UnitRow(
  id: json['id'] as String,
  name: json['name'] as String,
  subtitle: json['subtitle'] as String?,
  planKg: (json['planKg'] as num?)?.toDouble(),
  factKg: (json['factKg'] as num?)?.toDouble() ?? 0,
  execution: (json['execution'] as num?)?.toDouble(),
  forecastKg: (json['forecastKg'] as num?)?.toDouble(),
  forecastExecution: (json['forecastExecution'] as num?)?.toDouble(),
  revenue: (json['revenue'] as num?)?.toDouble() ?? 0,
  akb: (json['akb'] as num?)?.toInt() ?? 0,
  strike: (json['strike'] as num?)?.toDouble(),
  visitsWithoutOrder: (json['visitsWithoutOrder'] as num?)?.toInt() ?? 0,
  agents: (json['agents'] as num?)?.toInt() ?? 0,
  regionCount: (json['regionCount'] as num?)?.toInt() ?? 0,
  flags: json['flags'] == null
      ? const FlagCounts()
      : FlagCounts.fromJson(json['flags'] as Map<String, dynamic>),
  kind: json['kind'] as String? ?? 'region',
  executionLevel: json['executionLevel'] as String?,
  forecastExecutionLevel: json['forecastExecutionLevel'] as String?,
  isChannel: json['isChannel'] as bool? ?? false,
);

Map<String, dynamic> _$UnitRowToJson(_UnitRow instance) => <String, dynamic>{
  'id': instance.id,
  'name': instance.name,
  'subtitle': ?instance.subtitle,
  'planKg': ?instance.planKg,
  'factKg': instance.factKg,
  'execution': ?instance.execution,
  'forecastKg': ?instance.forecastKg,
  'forecastExecution': ?instance.forecastExecution,
  'revenue': instance.revenue,
  'akb': instance.akb,
  'strike': ?instance.strike,
  'visitsWithoutOrder': instance.visitsWithoutOrder,
  'agents': instance.agents,
  'regionCount': instance.regionCount,
  'flags': instance.flags.toJson(),
  'kind': instance.kind,
  'executionLevel': ?instance.executionLevel,
  'forecastExecutionLevel': ?instance.forecastExecutionLevel,
  'isChannel': instance.isChannel,
};

_SameDaysRow _$SameDaysRowFromJson(Map<String, dynamic> json) => _SameDaysRow(
  id: json['id'] as String,
  name: json['name'] as String,
  kgBefore: (json['kgBefore'] as num?)?.toDouble() ?? 0,
  kgNow: (json['kgNow'] as num?)?.toDouble() ?? 0,
  kgDelta: (json['kgDelta'] as num?)?.toDouble(),
  sumBefore: (json['sumBefore'] as num?)?.toDouble() ?? 0,
  sumNow: (json['sumNow'] as num?)?.toDouble() ?? 0,
  sumDelta: (json['sumDelta'] as num?)?.toDouble(),
  akbNow: (json['akbNow'] as num?)?.toInt() ?? 0,
  akbDelta: (json['akbDelta'] as num?)?.toDouble(),
);

Map<String, dynamic> _$SameDaysRowToJson(_SameDaysRow instance) =>
    <String, dynamic>{
      'id': instance.id,
      'name': instance.name,
      'kgBefore': instance.kgBefore,
      'kgNow': instance.kgNow,
      'kgDelta': ?instance.kgDelta,
      'sumBefore': instance.sumBefore,
      'sumNow': instance.sumNow,
      'sumDelta': ?instance.sumDelta,
      'akbNow': instance.akbNow,
      'akbDelta': ?instance.akbDelta,
    };

_CategoryCard _$CategoryCardFromJson(Map<String, dynamic> json) =>
    _CategoryCard(
      id: json['id'] as String,
      name: json['name'] as String,
      skuSold: (json['skuSold'] as num?)?.toInt() ?? 0,
      skuTotal: (json['skuTotal'] as num?)?.toInt() ?? 0,
      factKg: (json['factKg'] as num?)?.toDouble() ?? 0,
      weightShare: (json['weightShare'] as num?)?.toDouble(),
      revenue: (json['revenue'] as num?)?.toDouble() ?? 0,
      akb: (json['akb'] as num?)?.toInt() ?? 0,
      distribution: (json['distribution'] as num?)?.toDouble(),
      forecastKg: (json['forecastKg'] as num?)?.toDouble(),
      vsPrevMonth: (json['vsPrevMonth'] as num?)?.toDouble(),
      silent: (json['silent'] as num?)?.toInt() ?? 0,
      lost: (json['lost'] as num?)?.toInt() ?? 0,
    );

Map<String, dynamic> _$CategoryCardToJson(_CategoryCard instance) =>
    <String, dynamic>{
      'id': instance.id,
      'name': instance.name,
      'skuSold': instance.skuSold,
      'skuTotal': instance.skuTotal,
      'factKg': instance.factKg,
      'weightShare': ?instance.weightShare,
      'revenue': instance.revenue,
      'akb': instance.akb,
      'distribution': ?instance.distribution,
      'forecastKg': ?instance.forecastKg,
      'vsPrevMonth': ?instance.vsPrevMonth,
      'silent': instance.silent,
      'lost': instance.lost,
    };

_GroupView _$GroupViewFromJson(Map<String, dynamic> json) => _GroupView(
  period: SalesPeriod.fromJson(json['period'] as Map<String, dynamic>),
  name: json['name'] as String,
  subtitle: json['subtitle'] as String?,
  kpi: SalesKpi.fromJson(json['kpi'] as Map<String, dynamic>),
  cards:
      (json['cards'] as List<dynamic>?)
          ?.map((e) => UnitRow.fromJson(e as Map<String, dynamic>))
          .toList() ??
      const <UnitRow>[],
  regions:
      (json['regions'] as List<dynamic>?)
          ?.map((e) => UnitRow.fromJson(e as Map<String, dynamic>))
          .toList() ??
      const <UnitRow>[],
  sameDays:
      (json['sameDays'] as List<dynamic>?)
          ?.map((e) => SameDaysRow.fromJson(e as Map<String, dynamic>))
          .toList() ??
      const <SameDaysRow>[],
  categoryCards:
      (json['categoryCards'] as List<dynamic>?)
          ?.map((e) => CategoryCard.fromJson(e as Map<String, dynamic>))
          .toList() ??
      const <CategoryCard>[],
  days:
      (json['days'] as List<dynamic>?)
          ?.map((e) => (e as num?)?.toDouble())
          .toList() ??
      const <double?>[],
);

Map<String, dynamic> _$GroupViewToJson(_GroupView instance) =>
    <String, dynamic>{
      'period': instance.period.toJson(),
      'name': instance.name,
      'subtitle': ?instance.subtitle,
      'kpi': instance.kpi.toJson(),
      'cards': instance.cards.map((e) => e.toJson()).toList(),
      'regions': instance.regions.map((e) => e.toJson()).toList(),
      'sameDays': instance.sameDays.map((e) => e.toJson()).toList(),
      'categoryCards': instance.categoryCards.map((e) => e.toJson()).toList(),
      'days': instance.days,
    };

_TeamRow _$TeamRowFromJson(Map<String, dynamic> json) => _TeamRow(
  agentId: (json['agentId'] as num).toInt(),
  name: json['name'] as String,
  planKg: (json['planKg'] as num?)?.toDouble(),
  factKg: (json['factKg'] as num?)?.toDouble() ?? 0,
  execution: (json['execution'] as num?)?.toDouble(),
  forecastKg: (json['forecastKg'] as num?)?.toDouble(),
  forecastExecution: (json['forecastExecution'] as num?)?.toDouble(),
  revenue: (json['revenue'] as num?)?.toDouble() ?? 0,
  visits: (json['visits'] as num?)?.toInt() ?? 0,
  orders: (json['orders'] as num?)?.toInt() ?? 0,
  strike: (json['strike'] as num?)?.toDouble(),
  isVacancy: json['isVacancy'] as bool? ?? false,
  flags:
      (json['flags'] as List<dynamic>?)
          ?.map((e) => AgentFlag.fromJson(e as Map<String, dynamic>))
          .toList() ??
      const <AgentFlag>[],
  executionLevel: json['executionLevel'] as String?,
  akb: (json['akb'] as num?)?.toInt() ?? 0,
);

Map<String, dynamic> _$TeamRowToJson(_TeamRow instance) => <String, dynamic>{
  'agentId': instance.agentId,
  'name': instance.name,
  'planKg': ?instance.planKg,
  'factKg': instance.factKg,
  'execution': ?instance.execution,
  'forecastKg': ?instance.forecastKg,
  'forecastExecution': ?instance.forecastExecution,
  'revenue': instance.revenue,
  'visits': instance.visits,
  'orders': instance.orders,
  'strike': ?instance.strike,
  'isVacancy': instance.isVacancy,
  'flags': instance.flags.map((e) => e.toJson()).toList(),
  'executionLevel': ?instance.executionLevel,
  'akb': instance.akb,
};

_PlanFactMonth _$PlanFactMonthFromJson(Map<String, dynamic> json) =>
    _PlanFactMonth(
      month: (json['month'] as num).toInt(),
      planKg: (json['planKg'] as num?)?.toDouble(),
      factKg: (json['factKg'] as num?)?.toDouble(),
    );

Map<String, dynamic> _$PlanFactMonthToJson(_PlanFactMonth instance) =>
    <String, dynamic>{
      'month': instance.month,
      'planKg': ?instance.planKg,
      'factKg': ?instance.factKg,
    };

_RevenueShare _$RevenueShareFromJson(Map<String, dynamic> json) =>
    _RevenueShare(
      name: json['name'] as String,
      revenue: (json['revenue'] as num?)?.toDouble() ?? 0,
      share: (json['share'] as num?)?.toDouble(),
    );

Map<String, dynamic> _$RevenueShareToJson(_RevenueShare instance) =>
    <String, dynamic>{
      'name': instance.name,
      'revenue': instance.revenue,
      'share': ?instance.share,
    };

_RegionView _$RegionViewFromJson(Map<String, dynamic> json) => _RegionView(
  period: SalesPeriod.fromJson(json['period'] as Map<String, dynamic>),
  id: json['id'] as String,
  name: json['name'] as String,
  directionId: json['directionId'] as String?,
  directionName: json['directionName'] as String?,
  supervisor: json['supervisor'] as String?,
  dealer: json['dealer'] as String?,
  kpi: SalesKpi.fromJson(json['kpi'] as Map<String, dynamic>),
  months:
      (json['months'] as List<dynamic>?)
          ?.map((e) => PlanFactMonth.fromJson(e as Map<String, dynamic>))
          .toList() ??
      const <PlanFactMonth>[],
  categories:
      (json['categories'] as List<dynamic>?)
          ?.map((e) => RevenueShare.fromJson(e as Map<String, dynamic>))
          .toList() ??
      const <RevenueShare>[],
  team:
      (json['team'] as List<dynamic>?)
          ?.map((e) => TeamRow.fromJson(e as Map<String, dynamic>))
          .toList() ??
      const <TeamRow>[],
  sameDays:
      (json['sameDays'] as List<dynamic>?)
          ?.map((e) => SameDaysRow.fromJson(e as Map<String, dynamic>))
          .toList() ??
      const <SameDaysRow>[],
  categoryCards:
      (json['categoryCards'] as List<dynamic>?)
          ?.map((e) => CategoryCard.fromJson(e as Map<String, dynamic>))
          .toList() ??
      const <CategoryCard>[],
  days:
      (json['days'] as List<dynamic>?)
          ?.map((e) => (e as num?)?.toDouble())
          .toList() ??
      const <double?>[],
);

Map<String, dynamic> _$RegionViewToJson(_RegionView instance) =>
    <String, dynamic>{
      'period': instance.period.toJson(),
      'id': instance.id,
      'name': instance.name,
      'directionId': ?instance.directionId,
      'directionName': ?instance.directionName,
      'supervisor': ?instance.supervisor,
      'dealer': ?instance.dealer,
      'kpi': instance.kpi.toJson(),
      'months': instance.months.map((e) => e.toJson()).toList(),
      'categories': instance.categories.map((e) => e.toJson()).toList(),
      'team': instance.team.map((e) => e.toJson()).toList(),
      'sameDays': instance.sameDays.map((e) => e.toJson()).toList(),
      'categoryCards': instance.categoryCards.map((e) => e.toJson()).toList(),
      'days': instance.days,
    };

_UnitRef _$UnitRefFromJson(Map<String, dynamic> json) =>
    _UnitRef(id: json['id'] as String, name: json['name'] as String);

Map<String, dynamic> _$UnitRefToJson(_UnitRef instance) => <String, dynamic>{
  'id': instance.id,
  'name': instance.name,
};

_AgentCardRow _$AgentCardRowFromJson(Map<String, dynamic> json) =>
    _AgentCardRow(
      agentId: (json['agentId'] as num).toInt(),
      name: json['name'] as String,
      regionId: json['regionId'] as String?,
      regionName: json['regionName'] as String? ?? '',
      directionName: json['directionName'] as String?,
      supervisor: json['supervisor'] as String?,
      isVacancy: json['isVacancy'] as bool? ?? false,
      isSalesRep: json['isSalesRep'] as bool? ?? true,
      planKg: (json['planKg'] as num?)?.toDouble(),
      factKg: (json['factKg'] as num?)?.toDouble() ?? 0,
      execution: (json['execution'] as num?)?.toDouble(),
      forecastKg: (json['forecastKg'] as num?)?.toDouble(),
      forecastExecution: (json['forecastExecution'] as num?)?.toDouble(),
      revenue: (json['revenue'] as num?)?.toDouble() ?? 0,
      akb: (json['akb'] as num?)?.toInt() ?? 0,
      visits: (json['visits'] as num?)?.toInt() ?? 0,
      visitsWithOrder: (json['visitsWithOrder'] as num?)?.toInt() ?? 0,
      orders: (json['orders'] as num?)?.toInt() ?? 0,
      conversion: (json['conversion'] as num?)?.toDouble(),
      sumPerVisit: (json['sumPerVisit'] as num?)?.toDouble(),
      categories: (json['categories'] as num?)?.toInt() ?? 0,
      tempo: (json['tempo'] as num?)?.toDouble(),
      flags:
          (json['flags'] as List<dynamic>?)
              ?.map((e) => AgentFlag.fromJson(e as Map<String, dynamic>))
              .toList() ??
          const <AgentFlag>[],
      executionLevel: json['executionLevel'] as String?,
    );

Map<String, dynamic> _$AgentCardRowToJson(_AgentCardRow instance) =>
    <String, dynamic>{
      'agentId': instance.agentId,
      'name': instance.name,
      'regionId': ?instance.regionId,
      'regionName': instance.regionName,
      'directionName': ?instance.directionName,
      'supervisor': ?instance.supervisor,
      'isVacancy': instance.isVacancy,
      'isSalesRep': instance.isSalesRep,
      'planKg': ?instance.planKg,
      'factKg': instance.factKg,
      'execution': ?instance.execution,
      'forecastKg': ?instance.forecastKg,
      'forecastExecution': ?instance.forecastExecution,
      'revenue': instance.revenue,
      'akb': instance.akb,
      'visits': instance.visits,
      'visitsWithOrder': instance.visitsWithOrder,
      'orders': instance.orders,
      'conversion': ?instance.conversion,
      'sumPerVisit': ?instance.sumPerVisit,
      'categories': instance.categories,
      'tempo': ?instance.tempo,
      'flags': instance.flags.map((e) => e.toJson()).toList(),
      'executionLevel': ?instance.executionLevel,
    };

_AgentTargets _$AgentTargetsFromJson(Map<String, dynamic> json) =>
    _AgentTargets(
      conversion: (json['conversion'] as num?)?.toDouble() ?? 0.7,
      revenuePerOutlet: (json['revenuePerOutlet'] as num?)?.toDouble() ?? 0,
      akbPerAgent: (json['akbPerAgent'] as num?)?.toDouble() ?? 0,
      categoriesPerOutlet:
          (json['categoriesPerOutlet'] as num?)?.toDouble() ?? 0,
    );

Map<String, dynamic> _$AgentTargetsToJson(_AgentTargets instance) =>
    <String, dynamic>{
      'conversion': instance.conversion,
      'revenuePerOutlet': instance.revenuePerOutlet,
      'akbPerAgent': instance.akbPerAgent,
      'categoriesPerOutlet': instance.categoriesPerOutlet,
    };

_AgentsView _$AgentsViewFromJson(Map<String, dynamic> json) => _AgentsView(
  period: SalesPeriod.fromJson(json['period'] as Map<String, dynamic>),
  targets: json['targets'] == null
      ? const AgentTargets()
      : AgentTargets.fromJson(json['targets'] as Map<String, dynamic>),
  agents:
      (json['agents'] as List<dynamic>?)
          ?.map((e) => AgentCardRow.fromJson(e as Map<String, dynamic>))
          .toList() ??
      const <AgentCardRow>[],
  regions:
      (json['regions'] as List<dynamic>?)
          ?.map((e) => UnitRef.fromJson(e as Map<String, dynamic>))
          .toList() ??
      const <UnitRef>[],
  directions:
      (json['directions'] as List<dynamic>?)
          ?.map((e) => UnitRef.fromJson(e as Map<String, dynamic>))
          .toList() ??
      const <UnitRef>[],
);

Map<String, dynamic> _$AgentsViewToJson(_AgentsView instance) =>
    <String, dynamic>{
      'period': instance.period.toJson(),
      'targets': instance.targets.toJson(),
      'agents': instance.agents.map((e) => e.toJson()).toList(),
      'regions': instance.regions.map((e) => e.toJson()).toList(),
      'directions': instance.directions.map((e) => e.toJson()).toList(),
    };

_MedianValue _$MedianValueFromJson(Map<String, dynamic> json) => _MedianValue(
  value: (json['value'] as num?)?.toDouble(),
  regionMedian: (json['regionMedian'] as num?)?.toDouble(),
);

Map<String, dynamic> _$MedianValueToJson(_MedianValue instance) =>
    <String, dynamic>{
      'value': ?instance.value,
      'regionMedian': ?instance.regionMedian,
    };

_MarketBrief _$MarketBriefFromJson(Map<String, dynamic> json) => _MarketBrief(
  marketId: (json['marketId'] as num).toInt(),
  name: json['name'] as String,
  kg: (json['kg'] as num?)?.toDouble() ?? 0,
  revenue: (json['revenue'] as num?)?.toDouble() ?? 0,
  prevKg: (json['prevKg'] as num?)?.toDouble() ?? 0,
  prevRevenue: (json['prevRevenue'] as num?)?.toDouble() ?? 0,
  categories: (json['categories'] as num?)?.toInt() ?? 0,
  positions: (json['positions'] as num?)?.toInt() ?? 0,
  share: (json['share'] as num?)?.toDouble(),
);

Map<String, dynamic> _$MarketBriefToJson(_MarketBrief instance) =>
    <String, dynamic>{
      'marketId': instance.marketId,
      'name': instance.name,
      'kg': instance.kg,
      'revenue': instance.revenue,
      'prevKg': instance.prevKg,
      'prevRevenue': instance.prevRevenue,
      'categories': instance.categories,
      'positions': instance.positions,
      'share': ?instance.share,
    };

_CategoryPlanFact _$CategoryPlanFactFromJson(Map<String, dynamic> json) =>
    _CategoryPlanFact(
      categoryId: (json['categoryId'] as num?)?.toInt(),
      name: json['name'] as String,
      planKg: (json['planKg'] as num?)?.toDouble(),
      factKg: (json['factKg'] as num?)?.toDouble() ?? 0,
      execution: (json['execution'] as num?)?.toDouble(),
      forecastExecution: (json['forecastExecution'] as num?)?.toDouble(),
      executionLevel: json['executionLevel'] as String?,
    );

Map<String, dynamic> _$CategoryPlanFactToJson(_CategoryPlanFact instance) =>
    <String, dynamic>{
      'categoryId': ?instance.categoryId,
      'name': instance.name,
      'planKg': ?instance.planKg,
      'factKg': instance.factKg,
      'execution': ?instance.execution,
      'forecastExecution': ?instance.forecastExecution,
      'executionLevel': ?instance.executionLevel,
    };

_AgentView _$AgentViewFromJson(Map<String, dynamic> json) => _AgentView(
  period: SalesPeriod.fromJson(json['period'] as Map<String, dynamic>),
  agentId: (json['agentId'] as num).toInt(),
  name: json['name'] as String,
  regionId: json['regionId'] as String?,
  regionName: json['regionName'] as String?,
  directionName: json['directionName'] as String?,
  isVacancy: json['isVacancy'] as bool? ?? false,
  planKg: (json['planKg'] as num?)?.toDouble(),
  execution: (json['execution'] as num?)?.toDouble(),
  factKg: (json['factKg'] as num?)?.toDouble() ?? 0,
  revenue: (json['revenue'] as num?)?.toDouble() ?? 0,
  visits: (json['visits'] as num?)?.toInt() ?? 0,
  visitsWithOrder: (json['visitsWithOrder'] as num?)?.toInt() ?? 0,
  conversion: json['conversion'] == null
      ? const MedianValue()
      : MedianValue.fromJson(json['conversion'] as Map<String, dynamic>),
  sumPerVisit: json['sumPerVisit'] == null
      ? const MedianValue()
      : MedianValue.fromJson(json['sumPerVisit'] as Map<String, dynamic>),
  avgCheck: json['avgCheck'] == null
      ? const MedianValue()
      : MedianValue.fromJson(json['avgCheck'] as Map<String, dynamic>),
  categories: (json['categories'] as num?)?.toInt() ?? 0,
  planCategories: (json['planCategories'] as num?)?.toInt(),
  categoryTarget: (json['categoryTarget'] as num?)?.toDouble() ?? 4,
  tempo: (json['tempo'] as num?)?.toDouble(),
  flags:
      (json['flags'] as List<dynamic>?)
          ?.map((e) => AgentFlag.fromJson(e as Map<String, dynamic>))
          .toList() ??
      const <AgentFlag>[],
  categoryPlan:
      (json['categoryPlan'] as List<dynamic>?)
          ?.map((e) => CategoryPlanFact.fromJson(e as Map<String, dynamic>))
          .toList() ??
      const <CategoryPlanFact>[],
  sameDays: json['sameDays'] == null
      ? null
      : SameDaysRow.fromJson(json['sameDays'] as Map<String, dynamic>),
  silentBase: (json['silentBase'] as num?)?.toInt() ?? 0,
  silent:
      (json['silent'] as List<dynamic>?)
          ?.map((e) => MarketBrief.fromJson(e as Map<String, dynamic>))
          .toList() ??
      const <MarketBrief>[],
  newMarkets:
      (json['newMarkets'] as List<dynamic>?)
          ?.map((e) => MarketBrief.fromJson(e as Map<String, dynamic>))
          .toList() ??
      const <MarketBrief>[],
  stores:
      (json['stores'] as List<dynamic>?)
          ?.map((e) => MarketBrief.fromJson(e as Map<String, dynamic>))
          .toList() ??
      const <MarketBrief>[],
  akb: (json['akb'] as num?)?.toInt() ?? 0,
  orders: (json['orders'] as num?)?.toInt() ?? 0,
  revenuePerOutlet: (json['revenuePerOutlet'] as num?)?.toDouble(),
  kgPerOutlet: (json['kgPerOutlet'] as num?)?.toDouble(),
  executionLevel: json['executionLevel'] as String?,
);

Map<String, dynamic> _$AgentViewToJson(_AgentView instance) =>
    <String, dynamic>{
      'period': instance.period.toJson(),
      'agentId': instance.agentId,
      'name': instance.name,
      'regionId': ?instance.regionId,
      'regionName': ?instance.regionName,
      'directionName': ?instance.directionName,
      'isVacancy': instance.isVacancy,
      'planKg': ?instance.planKg,
      'execution': ?instance.execution,
      'factKg': instance.factKg,
      'revenue': instance.revenue,
      'visits': instance.visits,
      'visitsWithOrder': instance.visitsWithOrder,
      'conversion': instance.conversion.toJson(),
      'sumPerVisit': instance.sumPerVisit.toJson(),
      'avgCheck': instance.avgCheck.toJson(),
      'categories': instance.categories,
      'planCategories': ?instance.planCategories,
      'categoryTarget': instance.categoryTarget,
      'tempo': ?instance.tempo,
      'flags': instance.flags.map((e) => e.toJson()).toList(),
      'categoryPlan': instance.categoryPlan.map((e) => e.toJson()).toList(),
      'sameDays': ?instance.sameDays?.toJson(),
      'silentBase': instance.silentBase,
      'silent': instance.silent.map((e) => e.toJson()).toList(),
      'newMarkets': instance.newMarkets.map((e) => e.toJson()).toList(),
      'stores': instance.stores.map((e) => e.toJson()).toList(),
      'akb': instance.akb,
      'orders': instance.orders,
      'revenuePerOutlet': ?instance.revenuePerOutlet,
      'kgPerOutlet': ?instance.kgPerOutlet,
      'executionLevel': ?instance.executionLevel,
    };

_StoreCategory _$StoreCategoryFromJson(Map<String, dynamic> json) =>
    _StoreCategory(
      name: json['name'] as String,
      kg: (json['kg'] as num?)?.toDouble() ?? 0,
      revenue: (json['revenue'] as num?)?.toDouble() ?? 0,
      share: (json['share'] as num?)?.toDouble(),
    );

Map<String, dynamic> _$StoreCategoryToJson(_StoreCategory instance) =>
    <String, dynamic>{
      'name': instance.name,
      'kg': instance.kg,
      'revenue': instance.revenue,
      'share': ?instance.share,
    };

_StoreProduct _$StoreProductFromJson(Map<String, dynamic> json) =>
    _StoreProduct(
      productId: (json['productId'] as num).toInt(),
      name: json['name'] as String,
      category: json['category'] as String?,
      kg: (json['kg'] as num?)?.toDouble() ?? 0,
      revenue: (json['revenue'] as num?)?.toDouble() ?? 0,
      isTop: json['isTop'] as bool? ?? false,
    );

Map<String, dynamic> _$StoreProductToJson(_StoreProduct instance) =>
    <String, dynamic>{
      'productId': instance.productId,
      'name': instance.name,
      'category': ?instance.category,
      'kg': instance.kg,
      'revenue': instance.revenue,
      'isTop': instance.isTop,
    };

_StoreView _$StoreViewFromJson(Map<String, dynamic> json) => _StoreView(
  period: SalesPeriod.fromJson(json['period'] as Map<String, dynamic>),
  marketId: (json['marketId'] as num).toInt(),
  name: json['name'] as String,
  regionName: json['regionName'] as String?,
  factKg: (json['factKg'] as num?)?.toDouble() ?? 0,
  revenue: (json['revenue'] as num?)?.toDouble() ?? 0,
  orders: (json['orders'] as num?)?.toInt() ?? 0,
  categories: (json['categories'] as num?)?.toInt() ?? 0,
  positions: (json['positions'] as num?)?.toInt() ?? 0,
  prevMonthKg: (json['prevMonthKg'] as num?)?.toDouble() ?? 0,
  prevMonthRevenue: (json['prevMonthRevenue'] as num?)?.toDouble() ?? 0,
  categoryRows:
      (json['categoryRows'] as List<dynamic>?)
          ?.map((e) => StoreCategory.fromJson(e as Map<String, dynamic>))
          .toList() ??
      const <StoreCategory>[],
  products:
      (json['products'] as List<dynamic>?)
          ?.map((e) => StoreProduct.fromJson(e as Map<String, dynamic>))
          .toList() ??
      const <StoreProduct>[],
  agents:
      (json['agents'] as List<dynamic>?)?.map((e) => e as String).toList() ??
      const <String>[],
);

Map<String, dynamic> _$StoreViewToJson(_StoreView instance) =>
    <String, dynamic>{
      'period': instance.period.toJson(),
      'marketId': instance.marketId,
      'name': instance.name,
      'regionName': ?instance.regionName,
      'factKg': instance.factKg,
      'revenue': instance.revenue,
      'orders': instance.orders,
      'categories': instance.categories,
      'positions': instance.positions,
      'prevMonthKg': instance.prevMonthKg,
      'prevMonthRevenue': instance.prevMonthRevenue,
      'categoryRows': instance.categoryRows.map((e) => e.toJson()).toList(),
      'products': instance.products.map((e) => e.toJson()).toList(),
      'agents': instance.agents,
    };
